using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;

namespace NoMoreDispenserLoops
{
    /// <summary>Loads the mod and installs its Harmony patches.</summary>
    public sealed class ModEntry : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            PUtil.InitLibrary(false);
            new POptions().RegisterOptions(this, typeof(ModOptions));
            (POptions.ReadSettings<ModOptions>() ?? new ModOptions()).OnOptionsChanged();
            Strings.Add(NoMoreDispenserLoopsComponent.TitleKey, "Drop Tile Exclusion");
        }
    }
}
