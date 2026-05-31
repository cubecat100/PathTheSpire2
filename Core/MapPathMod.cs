#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace PathTheSpire2;

/// <summary>
/// 모드 초기화와 패치 적용을 담당한다.
/// </summary>
[ModInitializer("ModInit")]
public static class PathTheSpire2Entry
{
    private static Harmony? _harmony;

    /// <summary>
    /// 설정을 읽고 Harmony 패치를 적용한다.
    /// </summary>
    public static void ModInit()
    {
        // 점수 설정을 먼저 읽는다.
        MapPathScoreSettings.Load();

        // Harmony 인스턴스는 재사용한다.
        _harmony ??= new Harmony("paththespire2.mod");
        _harmony.PatchAll();
        Log.Warn("[PathTheSpire2] ModInit");
    }
}
