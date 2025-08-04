using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace S4.ImTryingToSaveYou
{
    [StaticConstructorOnStartup]
    public static class Patch_TryUnequipSomething
    {
        static Patch_TryUnequipSomething()
        {
            var harmony = new Harmony("net.S4.ImTryingToSaveYou");
            var unequipMethod = typeof(JobDriver_ForceTargetWear)
                .GetMethod("TryUnequipSomething", BindingFlags.Instance | BindingFlags.NonPublic);
            harmony.Patch(unequipMethod,
                prefix: new HarmonyMethod(typeof(Patch_TryUnequipSomething), nameof(TryUnequipSomething_Prefix)));
        }

        // Intercept the built-in unequip so that any apparel conflicting with the new one
        // goes into inventory (if non-hostile), rather than dropping to ground.
        static bool TryUnequipSomething_Prefix(JobDriver_ForceTargetWear __instance)
        {
            var job = __instance.job;
            var targetPawn = job.GetTarget(TargetIndex.A).Thing as Pawn;
            var newApparel = job.GetTarget(TargetIndex.B).Thing as Apparel;
            if (targetPawn == null || newApparel == null)
                return true; // nothing we can do, fallback

            // if hostile, leave default drop-to-ground behavior
            if (targetPawn.Faction != null && targetPawn.Faction.HostileTo(Faction.OfPlayer))
                return true;

            // look for any worn apparel that conflicts with the new one
            var bodyDef = targetPawn.RaceProps.body;
            foreach (var old in targetPawn.apparel.WornApparel.ToList())
            {
                // conflict = cannot wear together
                if (!ApparelUtility.CanWearTogether(newApparel.def, old.def, bodyDef))
                {
                    // remove from pawn
                    targetPawn.apparel.Remove(old);

                    // try to stash in inventory
                    if (!targetPawn.inventory.innerContainer.TryAdd(old))
                    {
                        // if full, drop nearby
                        GenPlace.TryPlaceThing(old, targetPawn.PositionHeld, targetPawn.Map, ThingPlaceMode.Near);
                    }
                    break; // handle only the first conflict each tick
                }
            }

            // skip the original which would drop it on the ground again
            return false;
        }
    }
}
