using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace ImTryingToSaveYou
{
    // Holds and persists the OriginalApparelTracker._originalApparel dictionary across saves/loads.
    public class OriginalApparelGameComponent : GameComponent
    {
        // these hold your data for Scribe
        private List<Pawn> _keys;
        private List<List<int>> _values;

        public OriginalApparelGameComponent(Game game) : base() { }

        public override void ExposeData()
        {
            base.ExposeData();

            // On saving, build the parallel lists from your in-memory dictionary
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                _keys = OriginalApparelTracker.Records.Keys.ToList();
                _values = OriginalApparelTracker.Records.Values
                           .Select(set => set.ToList())
                           .ToList();
            }

            // Always look the lists
            Scribe_Collections.Look(ref _keys, "OriginalApparelRecords_keys", LookMode.Reference);
            Scribe_Collections.Look(ref _values, "OriginalApparelRecords_values", LookMode.Value);

            // On load, rebuild the dictionary
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                OriginalApparelTracker.RecordsClear();
                if (_keys != null && _values != null && _keys.Count == _values.Count)
                {
                    for (int i = 0; i < _keys.Count; i++)
                    {
                        var pawn = _keys[i];
                        var list = _values[i];
                        if (pawn != null && !pawn.Destroyed && list != null)
                            OriginalApparelTracker.RecordsAdd(pawn, new HashSet<int>(list));
                    }
                }

                // prune any entries whose pawn is gone
                var toRemove = OriginalApparelTracker.Records.Keys
                    .Where(p => p == null || p.Destroyed)
                    .ToList();
                foreach (var pawn in toRemove)
                    OriginalApparelTracker.RemoveRecord(pawn);
            }
        }
    }

    [StaticConstructorOnStartup]
    static class OriginalApparelGameComponentRegistrar
    {
        static OriginalApparelGameComponentRegistrar()
        {
            LongEventHandler.ExecuteWhenFinished(Register);
        }

        private static void Register()
        {
            if (Current.Game == null) return;
            if (Current.Game.components.OfType<OriginalApparelGameComponent>().Any()) return;
            Current.Game.components.Add(new OriginalApparelGameComponent(Current.Game));
        }
    }
}
