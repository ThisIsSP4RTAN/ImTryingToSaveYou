using HarmonyLib;
using Hospital.Utilities;
using Hospital;
using RimWorld;
using Verse;
using Verse.AI;

namespace ImTryingToSaveYou
{
    [StaticConstructorOnStartup]
    public static class HospitalForceWear_Patch
    {
        static HospitalForceWear_Patch()
        {
            var harmony = new Harmony("net.S4.ImTryingToSaveYou.HospitalForceWear");

            // (1) Widen the built-in targeter used by the current option
            harmony.Patch(
                original: AccessTools.Method(typeof(TargetingParameters), "ForForceWear", new[] { typeof(Pawn) }),
                postfix: new HarmonyMethod(typeof(HospitalForceWear_Patch), nameof(ForForceWear_Postfix))
            );

            // (2) Make worn apparel “forced” for patients
            harmony.Patch(
                original: AccessTools.Method(typeof(JobDriver_ForceTargetWear), nameof(JobDriver_ForceTargetWear.Notify_Starting)),
                postfix: new HarmonyMethod(typeof(HospitalForceWear_Patch), nameof(Notify_Starting_Postfix))
            );

            // (3) Avoid relations penalty fired at job end
            harmony.Patch(
                original: AccessTools.Method(typeof(Faction), nameof(Faction.Notify_MemberStripped), new[] { typeof(Pawn), typeof(Faction) }),
                prefix: new HarmonyMethod(typeof(HospitalForceWear_Patch), nameof(Notify_MemberStripped_Prefix))
            );
        }

        private static void ForForceWear_Postfix(Pawn selectedPawnForJob, ref TargetingParameters __result)
        {
            if (__result == null) return;

            var oldValidator = __result.validator;
            __result.canTargetPawns = true;
            __result.validator = delegate (TargetInfo t)
            {
                bool baseOk = oldValidator != null ? oldValidator(t) : true;

                var p = t.Thing as Pawn;
                if (p != null)
                {
                    HospitalMapComponent hospital;
                    // false => include dismissed-but-present patients; set to true to exclude dismissed
                    if (p.IsPatient(out hospital, false))
                        return true;
                }
                return baseOk;
            };
        }

        private static void Notify_Starting_Postfix(JobDriver_ForceTargetWear __instance)
        {
            if (__instance == null || __instance.job == null) return;

            var target = __instance.job.GetTarget(TargetIndex.A).Thing as Pawn;
            if (target == null) return;

            HospitalMapComponent hospital;
            if (target.IsPatient(out hospital, false))
                __instance.job.playerForced = true;
        }

        private static bool Notify_MemberStripped_Prefix(Faction __instance, Pawn member, Faction violator)
        {
            if (member != null)
            {
                HospitalMapComponent hospital;
                if (member.IsPatient(out hospital, false))
                    return false; // skip goodwill hit for Hospital patients
            }
            return true;
        }
    }
}