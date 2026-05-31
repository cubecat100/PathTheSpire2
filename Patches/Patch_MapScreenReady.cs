#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace PathTheSpire2;

/// <summary>
/// 맵 화면 준비 시 시스템 노드와 UI 패널을 연결한다.
/// </summary>
[HarmonyPatch(typeof(NMapScreen), "_Ready")]
public static class Patch_MapScreenReady
{
    /// <summary>
    /// 맵 화면에 경로 시스템과 선호도 패널을 붙인다.
    /// </summary>
    public static void Postfix(NMapScreen __instance)
    {
        // 맵 화면에 MapPathSystem을 붙인다.
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

            // 이미 있는 패널을 현재 범례에 다시 연결한다.
            __instance.GetNode<MapPathPreferencePanel>(MapPathPreferencePanel.NodeName).AttachToLegend(__instance);
            return;
        }

        var panel = new MapPathPreferencePanel();
        __instance.AddChild(panel);
        panel.AttachToLegend(__instance);
        Log.Warn("[PathTheSpire2] MapPathPreferencePanel attached to map screen");
    }
}
