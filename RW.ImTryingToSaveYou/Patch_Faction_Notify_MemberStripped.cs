using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;
using Verse.AI;

namespace ImTryingToSaveYou
{
    [StaticConstructorOnStartup]
    public static class Patch_Faction_Notify_MemberStripped
    {
        // Keep track of which pawns are being forcibly dressed
        public static readonly HashSet<Pawn> JustForceDressed = new HashSet<Pawn>();

        static Patch_Faction_Notify_MemberStripped()
        {
            var harmony = new Harmony("ImTryingToSaveYou");
            harmony.Patch(
                AccessTools.Method(typeof(Faction), nameof(Faction.Notify_MemberStripped)),
                prefix: new HarmonyMethod(typeof(Patch_Faction_Notify_MemberStripped), nameof(Notify_MemberStripped_Prefix)),
                postfix: new HarmonyMethod(typeof(Patch_Faction_Notify_MemberStripped), nameof(Notify_MemberStripped_Postfix))
            );

            harmony.Patch(
                AccessTools.Method(typeof(JobDriver_ForceTargetWear), "MakeNewToils"),
                postfix: new HarmonyMethod(typeof(Patch_Faction_Notify_MemberStripped), nameof(MakeNewToils_Postfix))
            );
        }

        // Postfix for MakeNewToils: attach a cleanup action to the final toil to track status
        public static void MakeNewToils_Postfix(JobDriver_ForceTargetWear __instance, ref IEnumerable<Toil> __result)
        {
            // Defensive: convert to list so we can attach to the final Toil
            var list = __result as List<Toil> ?? new List<Toil>(__result);
            if (list.Count == 0) return;

            // Find the last Toil (should be the one that completes the dress job)
            var finalToil = list[list.Count - 1];

            finalToil.AddPreInitAction(() =>
            {
                // Remove flag in case it's hanging around from failed jobs
                var pawn = __instance.job?.GetTarget(TargetIndex.A).Thing as Pawn;
                if (pawn != null)
                    JustForceDressed.Remove(pawn);
            });

            finalToil.AddFinishAction(() =>
            {
                // Only set the flag if the job finished normally
                var pawn = __instance.job?.GetTarget(TargetIndex.A).Thing as Pawn;
                if (pawn != null && __instance.pawn.jobs.curDriver == __instance)
                {
                    JustForceDressed.Add(pawn);
                    // Log.Message($"[ImTryingToSaveYou] Marked {pawn} as forcibly dressed (job finished normally).");
                }
            });

            __result = list;
        }

        // Prefix for Notify_MemberStripped - skip penalty if we just forcibly dressed this pawn
        public static bool Notify_MemberStripped_Prefix(Pawn member, Faction violator)
        {
            if (JustForceDressed.Contains(member))
            {
                // Log.Message($"[ImTryingToSaveYou] Suppressing penalty for {member} stripped by {violator}.");
                JustForceDressed.Remove(member);
                return false; // skip original, do NOT apply penalty
            }
            return true; // run vanilla
        }

        // Postfix to clean up state (for any edge case)
        public static void Notify_MemberStripped_Postfix(Pawn member, Faction violator)
        {
            JustForceDressed.Remove(member);
        }
    }
}