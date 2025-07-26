using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace S4.ImTryingToSaveYou
{
    [StaticConstructorOnStartup]
    public static class Patch_ForceTargetWear_SkipStripFinishAction
    {
        static Patch_ForceTargetWear_SkipStripFinishAction()
        {
            var harmony = new Harmony("net.S4.ImTryingToSaveYou");
            // target the compiler-generated "finish" lambda inside ForceTargetWear.MakeNewToils
            var original = typeof(JobDriver_ForceTargetWear)
                           .GetMethod("<MakeNewToils>b__16_1",
                                      BindingFlags.Instance | BindingFlags.NonPublic);
            var prefix = new HarmonyMethod(typeof(Patch_ForceTargetWear_SkipStripFinishAction),
                                            nameof(Prefix));
            harmony.Patch(original, prefix: prefix);
        }

        // this will run *instead* of the original finish‑action, so we
        // only do the EndCurrentJob snippet and *never* call Notify_MemberStripped
        static bool Prefix(JobDriver_ForceTargetWear __instance)
        {
            var targetPawn = __instance.job.GetTarget(TargetIndex.A).Thing as Pawn;
            if (targetPawn != null && targetPawn.CurJobDef == JobDefOf.Wait_MaintainPosture)
                targetPawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            return false; // skip the original (which would have called Notify_MemberStripped)
        }
    }
}