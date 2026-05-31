#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace PathTheSpire2;

[HarmonyPatch(typeof(NMapScreen), "OnMapPointSelectedLocally")]
public static class Patch_MapPointSelected
{
    public static void Postfix(NMapScreen __instance, NMapPoint point)
    {
        if (point?.Point == null)
        {
            Log.Warn("[PathTheSpire2] Map point refresh skipped: selected point is null");
            return;
        }

        var system = __instance.GetNodeOrNull<MapPathSystem>(MapPathSystem.NodeName);
        if (system == null)
        {
            Log.Warn("[PathTheSpire2] Map point refresh skipped: MapPathSystem not found");
            return;
        }

        system.HandleMapPointSelected(point.Point);
    }
}
