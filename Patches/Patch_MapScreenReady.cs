#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace PathTheSpire2;

[HarmonyPatch(typeof(NMapScreen), "_Ready")]
public static class Patch_MapScreenReady
{
    public static void Postfix(NMapScreen __instance)
    {
        if (__instance.GetNodeOrNull<MapPathSystem>(MapPathSystem.NodeName) != null)
        {
            Log.Warn("[PathTheSpire2] MapPathSystem already attached to map screen");
        }
        else
        {
            var system = new MapPathSystem();
            __instance.AddChild(system);
            Log.Warn("[PathTheSpire2] MapPathSystem attached to map screen");
        }

        if (__instance.GetNodeOrNull<MapPathPreferencePanel>(MapPathPreferencePanel.NodeName) != null)
        {
            Log.Warn("[PathTheSpire2] MapPathPreferencePanel already attached to map screen");
            __instance.GetNode<MapPathPreferencePanel>(MapPathPreferencePanel.NodeName).AttachToLegend(__instance);
            return;
        }

        var panel = new MapPathPreferencePanel();
        __instance.AddChild(panel);
        panel.AttachToLegend(__instance);
        Log.Warn("[PathTheSpire2] MapPathPreferencePanel attached to map screen");
    }
}
