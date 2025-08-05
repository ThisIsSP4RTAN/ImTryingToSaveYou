using UnityEngine;
using Verse;

namespace ImTryingToSaveYou
{
    public class ImTryingToSaveYouSettings : ModSettings
    {
        public static bool ShowLogWarnings = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ShowLogWarnings, "ITSY_ShowLogWarnings", false);
            base.ExposeData();
        }
    }

    public class ImTryingToSaveYouMod : Mod
    {
        public ImTryingToSaveYouMod(ModContentPack content) : base(content)
        {
            GetSettings<ImTryingToSaveYouSettings>();
        }

        public override string SettingsCategory() => "I'm Trying To Save You!";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("Show debug/warning log messages", ref ImTryingToSaveYouSettings.ShowLogWarnings,
                "Enable or disable warning/debug messages from this mod in the log.");
            listing.End();
            base.DoSettingsWindowContents(inRect);
        }
    }
}
