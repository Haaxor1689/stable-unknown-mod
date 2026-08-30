using HarmonyLib;
using HarmonyLib.Tools;
using Ignitron.Loader;

namespace StableUnknown;

public sealed class Mod : IModEntrypoint
{
    public const string ModId = "stable_unknown_mod";

    public void Main(ModBox box)
    {
#if DEBUG
        HarmonyFileLog.Enabled = true;
#endif
        // Apply harmony patches
        new Harmony($"{box.Metadata.Contributors.First().Name}.{box.Metadata.Id}").PatchAll();
    }
}
