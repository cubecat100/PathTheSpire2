#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace PathTheSpire2;

/// <summary>
/// 留?吏꾪뻾 ?붾젰 異붽?瑜?濡쒓렇濡?湲곕줉?쒕떎.
/// </summary>
[HarmonyPatch(typeof(RunState), nameof(RunState.AppendToMapPointHistory))]
public static class Patch_RunStateMapProgress
{
    /// <summary>
    /// ?꾩옱 吏꾪뻾 ?몃뱶??愿??媛믪쓣 濡쒓렇濡?湲곕줉?쒕떎.
    /// </summary>
    public static void Postfix(RunState __instance, MapPointType mapPointType, RoomType initialRoomType, ModelId roomModelId)
    {
        var currentPoint = MapPathSystem.DescribePointForLog(__instance.CurrentMapPoint);
        var currentCoord = __instance.CurrentMapCoord?.ToString() ?? "<null>";
        var historyCount = __instance.MapPointHistory?.Count ?? 0;
        Log.Warn(
            $"[PathTheSpire2] Map progress history appended >> current={currentPoint}, coord={currentCoord}, pointType={mapPointType}, roomType={initialRoomType}, roomModel={roomModelId}, act={__instance.CurrentActIndex + 1}, history={historyCount}");
    }
}
