using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
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
                AccessTools.Method(typeof(TargetingParameters), "ForForceWear", new[] { typeof(Pawn) }),
                postfix: new HarmonyMethod(typeof(HospitalForceWear_Patch), nameof(ForForceWear_Postfix))
            );

            // (2) Make worn apparel “forced” for patients
            harmony.Patch(
                AccessTools.Method(typeof(JobDriver_ForceTargetWear), nameof(JobDriver_ForceTargetWear.Notify_Starting)),
                postfix: new HarmonyMethod(typeof(HospitalForceWear_Patch), nameof(Notify_Starting_Postfix))
            );

            // (3) Avoid relations penalty fired at job end
            harmony.Patch(
                AccessTools.Method(typeof(Faction), nameof(Faction.Notify_MemberStripped), new[] { typeof(Pawn), typeof(Faction) }),
                prefix: new HarmonyMethod(typeof(HospitalForceWear_Patch), nameof(Notify_MemberStripped_Prefix))
            );
        }

        private static void ForForceWear_Postfix(Pawn selectedPawnForJob, ref TargetingParameters __result)
        {
            if (__result == null) return;

            var old = __result.validator;
            __result.canTargetPawns = true;
            __result.validator = delegate (TargetInfo t)
            {
                bool baseOk = old != null ? old(t) : true;

                if (!HospitalCompatUtil.IsHospitalPresent) return baseOk;

                var p = t.Thing as Pawn;
                if (p != null && HospitalCompatUtil.IsPatient(p, /*excludeDismissed*/ false))
                    return true;

                return baseOk;
            };
        }

        private static void Notify_Starting_Postfix(JobDriver_ForceTargetWear __instance)
        {
            if (__instance == null || __instance.job == null || !HospitalCompatUtil.IsHospitalPresent) return;

            var target = __instance.job.GetTarget(TargetIndex.A).Thing as Pawn;
            if (target != null && HospitalCompatUtil.IsPatient(target, false))
                __instance.job.playerForced = true;
        }

        private static bool Notify_MemberStripped_Prefix(Faction __instance, Pawn member, Faction violator)
        {
            if (!HospitalCompatUtil.IsHospitalPresent) return true;

            if (member != null && HospitalCompatUtil.IsPatient(member, false))
                return false; // suppress vanilla goodwill hit for Hospital patients
            return true;
        }
    }

    // Soft-compat layer: reflect Hospital.Utilities.PatientUtility.IsPatient(Pawn, out HospitalMapComponent, bool)
    internal static class HospitalCompatUtil
    {
        private static bool _resolved;
        private static bool _present;
        private static MethodInfo _isPatientMI; // static bool IsPatient(Pawn, out ?, bool)

        internal static bool IsHospitalPresent
        {
            get { EnsureResolved(); return _present && _isPatientMI != null; }
        }

        internal static bool IsPatient(Pawn pawn, bool excludeDismissed)
        {
            EnsureResolved();
            if (!IsHospitalPresent || pawn == null) return false;

            // args: (Pawn, out HospitalMapComponent, bool). We don't need the out value.
            object[] args = new object[] { pawn, null, excludeDismissed };
            try
            {
                object result = _isPatientMI.Invoke(null, args);
                return result is bool && (bool)result;
            }
            catch { return false; }
        }

        private static void EnsureResolved()
        {
            if (_resolved) return;
            _resolved = true;

            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Hospital");
                if (asm == null) { _present = false; return; }

                Type util = null;
                foreach (var t in asm.GetTypes())
                    if (t.FullName == "Hospital.Utilities.PatientUtility") { util = t; break; }

                if (util == null) { _present = false; return; }

                // Pick overload: (Pawn, out ?, bool)
                foreach (var m in util.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name != "IsPatient") continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 3 && ps[0].ParameterType == typeof(Pawn) && ps[2].ParameterType == typeof(bool))
                    {
                        _isPatientMI = m;
                        break;
                    }
                }

                _present = _isPatientMI != null;
            }
            catch
            {
                _present = false;
                _isPatientMI = null;
            }
        }
    }
}