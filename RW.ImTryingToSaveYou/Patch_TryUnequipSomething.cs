using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ImTryingToSaveYou
{
    // 1) TRACKER: remember each pawns original apparel on spawn, forget it on despawn
    [StaticConstructorOnStartup]
    static class OriginalApparelTracker
    {
        // pawn → set of ThingDef they started with
        static readonly Dictionary<Pawn, HashSet<ThingDef>> _originalApparel = new Dictionary<Pawn, HashSet<ThingDef>>();

        static OriginalApparelTracker()
        {
            var harmony = new Harmony("net.S4.ImTryingToSaveYou.originalapparel");
            // After Pawn.SpawnSetup(Map, bool), record what they're wearing
            harmony.Patch(
                AccessTools.Method(typeof(Pawn), nameof(Pawn.SpawnSetup), new[] { typeof(Map), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(OriginalApparelTracker), nameof(SpawnSetup_Postfix))
            );
            // Before Pawn.DeSpawn(...), drop our record
            harmony.Patch(
                AccessTools.Method(typeof(Pawn), nameof(Pawn.DeSpawn)),
                prefix: new HarmonyMethod(typeof(OriginalApparelTracker), nameof(DeSpawn_Prefix))
            );
        }

        static void SpawnSetup_Postfix(Pawn __instance, Map map, bool respawningAfterLoad)
        {
            // don't overwrite on load
            if (respawningAfterLoad) return;
            // skip player pawns
            if (__instance.Faction == Faction.OfPlayer) return;
            // skip hostile pawns — no need to track their originals
            if (__instance.Faction != null && __instance.Faction.HostileTo(Faction.OfPlayer)) return;
            // only track those capable of wearing apparel
            if (!__instance.RaceProps.Humanlike) return;

            var defs = __instance.apparel?.WornApparel.Select(a => a.def)
                       ?? Enumerable.Empty<ThingDef>();
            _originalApparel[__instance] = new HashSet<ThingDef>(defs);

            Log.Warning(
                $"[OriginalApparelTracker] Pawn “{__instance.LabelShort}” appeared on map “{map?.Tile}” " +
                $"tracking original apparel: {string.Join(", ", defs.Select(d => d.defName))}"
            );
        }

        static void DeSpawn_Prefix(Pawn __instance)
        {
            bool removed = _originalApparel.Remove(__instance);
            Log.Warning($"[OriginalApparelTracker] Pawn “{__instance.LabelShort}” despawned, died or became hostile — original‐apparel record removed: {removed}");
        }

        public static bool WasOriginallyWearing(Pawn pawn, Apparel app)
        {
            if (pawn == null || app == null) return false;
            return _originalApparel.TryGetValue(pawn, out var set) && set.Contains(app.def);
        }
    }

    // 2) PATCH: intercept the private TryUnequipSomething
    [StaticConstructorOnStartup]
    public static class Patch_ForceTargetWear_TryUnequipSomething
    {
        static Patch_ForceTargetWear_TryUnequipSomething()
        {
            var harmony = new Harmony("net.S4.ImTryingToSaveYou.tryunequip");
            var jobType = typeof(JobDriver_ForceTargetWear);
            // find the private method via reflection
            var tryUnequip = AccessTools.Method(jobType, "TryUnequipSomething");
            if (tryUnequip == null)
            {
                Log.Warning("[ImTryingToSaveYou] TryUnequipSomething not found → skipping patch.");
                return;
            }

            harmony.Patch(
                tryUnequip,
                prefix: new HarmonyMethod(typeof(Patch_ForceTargetWear_TryUnequipSomething), nameof(Prefix))
            );

            Log.Message("[ImTryingToSaveYou] Patched JobDriver_ForceTargetWear.TryUnequipSomething");
        }

        // our replacement for TryUnequipSomething
        public static bool Prefix(JobDriver_ForceTargetWear __instance)
        {
            // get the job targets
            var job = __instance.job;
            var targetPawn = job.GetTarget(TargetIndex.A).Thing as Pawn;
            var newApparel = job.GetTarget(TargetIndex.B).Thing as Apparel;
            if (targetPawn == null || newApparel == null)
                return true; // fallback to vanilla

            // find the first conflicting piece of apparel
            var worn = targetPawn.apparel.WornApparel;
            for (int i = worn.Count - 1; i >= 0; i--)
            {
                var oldApparel = worn[i];
                if (!ApparelUtility.CanWearTogether(newApparel.def, oldApparel.def, targetPawn.RaceProps.body))
                {
                    // remove it from their body
                    targetPawn.apparel.Remove(oldApparel);

                    // if it was one of their originals, stash it in their inventory
                    if (OriginalApparelTracker.WasOriginallyWearing(targetPawn, oldApparel) && targetPawn.inventory != null)
                    {
                        // try to add to inventory
                        if (!targetPawn.inventory.innerContainer.TryAdd(oldApparel))
                            Log.Warning($"[ImTryingToSaveYou] Couldn't add {oldApparel} to {targetPawn}'s inventory");
                    }
                    else
                    {
                        // otherwise, drop to ground near them
                        if (!GenPlace.TryPlaceThing(oldApparel, targetPawn.PositionHeld, targetPawn.Map, ThingPlaceMode.Near))
                            Log.Error($"[ImTryingToSaveYou] Could not drop {oldApparel} at {targetPawn.PositionHeld}");

                        // and forbid it if they're hostile like vanilla does
                        if (targetPawn.Faction != null && targetPawn.Faction.HostileTo(Faction.OfPlayer))
                            oldApparel.SetForbidden(true);
                    }

                    break;
                }
            }

            // skip original TryUnequipSomething entirely
            return false;
        }
    }
}