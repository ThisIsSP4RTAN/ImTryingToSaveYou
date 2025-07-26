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
            // Log.Message("[ImTryingToSaveYou] Loader loaded, all patches applied!");
        }

        // Called when MakeNewToils for ForceTargetWear completes; mark pawn as "just forcibly dressed"
        public static void MakeNewToils_Postfix(JobDriver_ForceTargetWear __instance)
        {
            if (__instance?.job != null)
            {
                Pawn targetPawn = __instance.job.GetTarget(TargetIndex.A).Thing as Pawn;
                if (targetPawn != null)
                {
                    JustForceDressed.Add(targetPawn);
                   // Log.Message($"[ImTryingToSaveYou] Marking {targetPawn} as just forcibly dressed.");
                }
            }
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