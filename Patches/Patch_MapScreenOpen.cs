#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace PathTheSpire2;

[HarmonyPatch(typeof(NMapScreen), "Open")]
public static class Patch_MapScreenOpen
{
    public static void Postfix(NMapScreen __instance)
    {
        var system = __instance.GetNodeOrNull<MapPathSystem>(MapPathSystem.NodeName);
        if (system == null)
        {
            Log.Warn("[PathTheSpire2] Map open refresh skipped: MapPathSystem not found");
            return;
        }

        system.CallDeferred(nameof(MapPathSystem.HandleMapOpened));
    }
}
