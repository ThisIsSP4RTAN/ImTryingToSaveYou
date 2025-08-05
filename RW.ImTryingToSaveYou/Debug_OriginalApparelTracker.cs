using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ImTryingToSaveYou
{
    [StaticConstructorOnStartup]
    static class Debug_OriginalApparelTracker
    {
        public static void RebuildAll()
        {
            var originalApparel = OriginalApparelTracker._originalApparel;

            originalApparel.Clear();
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (pawn.Faction == null || pawn.Faction == Faction.OfPlayer ||
                        pawn.Faction.HostileTo(Faction.OfPlayer) || !pawn.RaceProps.Humanlike)
                        continue;

                    var ids = pawn.apparel?.WornApparel.Select(a => a.thingIDNumber)
                              ?? Enumerable.Empty<int>();
                    originalApparel[pawn] = new HashSet<int>(ids);
                }
            }
            Log.Warning($"[ImTryingToSaveYou] OriginalApparelTracker rebuilt for {originalApparel.Count} pawn(s)");
        }

        public static void Clear()
        {
            var originalApparel = OriginalApparelTracker._originalApparel;

            int count = originalApparel.Count;
            originalApparel.Clear();
            Log.Warning($"[ImTryingToSaveYou] OriginalApparelTracker cleared ({count} records removed)");
        }

        [DebugAction("ImTryingToSaveYou", "Rebuild Original Apparel Tracker")]
        public static void DebugRebuildTracker()
        {
            Debug_OriginalApparelTracker.RebuildAll();
        }

        [DebugAction("ImTryingToSaveYou", "Clear Original Apparel Tracker")]
        public static void DebugClearTracker()

        {
            Debug_OriginalApparelTracker.Clear();
        }

        [DebugAction("ImTryingToSaveYou", "List Original Apparel Records")]
        public static void DebugListTracker()
        {
            var records = OriginalApparelTracker.Records;
            Log.Warning($"[ImTryingToSaveYou] Tracker has {records.Count} pawn(s):");
            foreach (var kvp in records)
            {
                var pawn = kvp.Key;
                var ids = kvp.Value;
                Log.Warning($"  • {pawn.LabelShort}: {string.Join(", ", ids)}");
            }
        }
    }
}
