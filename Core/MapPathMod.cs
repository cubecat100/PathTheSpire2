#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace PathTheSpire2;

[ModInitializer("ModInit")]
public static class PathTheSpire2Entry
{
    private static Harmony? _harmony;

    public static void ModInit()
    {
        _harmony ??= new Harmony("paththespire2.mod");
        _harmony.PatchAll();
        Log.Warn("[PathTheSpire2] ModInit");
    }
}
