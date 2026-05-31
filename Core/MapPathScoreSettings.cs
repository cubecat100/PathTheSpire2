#nullable enable
using Godot;
using MegaCrit.Sts2.Core.Logging;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace PathTheSpire2;

/// <summary>
/// 경로 점수 가중치 묶음이다.
/// </summary>
public readonly record struct MapPathScoreTuning(
    double PreferBonus,
    double AvoidPenalty,
    double BranchWeight,
    double EliteChildBonus,
    double ActWeight,
    double RestEliteWeight,
    double ShopWeight,
    double HealthWeight);

/// <summary>
/// 점수 설정의 현재값, 로드, 저장을 관리한다.
/// </summary>
public static class MapPathScoreSettings
{
    // 설정 파일 이름과 섹션 이름이다.
    private const string SettingsFileName = "path_the_spire2_score_settings.cfg";
    private const string SettingsSection = "score";

    public static readonly MapPathScoreTuning Defaults = new(
        PreferBonus: 7.0,
        AvoidPenalty: 9.0,
        BranchWeight: 1.0,
        EliteChildBonus: 1.0,
        ActWeight: 1.0,
        RestEliteWeight: 1.0,
        ShopWeight: 1.0,
        HealthWeight: 1.0);

    private static MapPathScoreTuning _current = Defaults;

    public static MapPathScoreTuning Current => _current;
    public static string SettingsPath => ResolveSettingsPath();

    /// <summary>
    /// 설정 파일을 읽어 현재값을 갱신한다.
    /// </summary>
    public static void Load()
    {
        // 설정 파일을 읽고 현재값을 갱신한다.
        var config = new ConfigFile();
        var settingsPath = ResolveSettingsPath();
        var error = config.Load(settingsPath);
        if (error != Error.Ok)
        {
            _current = Defaults;
            Log.Warn($"[PathTheSpire2] Score settings load skipped: path={settingsPath}, error={error}, using defaults");
            return;
        }

        _current = new MapPathScoreTuning(
            PreferBonus: config.GetValue(SettingsSection, "prefer_bonus", Defaults.PreferBonus).AsDouble(),
            AvoidPenalty: config.GetValue(SettingsSection, "avoid_penalty", Defaults.AvoidPenalty).AsDouble(),
            BranchWeight: config.GetValue(SettingsSection, "branch_weight", Defaults.BranchWeight).AsDouble(),
            EliteChildBonus: config.GetValue(SettingsSection, "elite_child_bonus", Defaults.EliteChildBonus).AsDouble(),
            ActWeight: config.GetValue(SettingsSection, "act_weight", Defaults.ActWeight).AsDouble(),
            RestEliteWeight: config.GetValue(SettingsSection, "rest_elite_weight", Defaults.RestEliteWeight).AsDouble(),
            ShopWeight: config.GetValue(SettingsSection, "shop_weight", Defaults.ShopWeight).AsDouble(),
            HealthWeight: config.GetValue(SettingsSection, "health_weight", Defaults.HealthWeight).AsDouble());

        Log.Warn($"[PathTheSpire2] Score settings loaded: path={settingsPath} >> {Describe(_current)}");
    }

    /// <summary>
    /// 메모리상의 현재 설정값을 갱신한다.
    /// </summary>
    public static void Update(MapPathScoreTuning tuning)
    {
        // 현재 메모리상의 점수 설정을 갱신한다.
        _current = tuning;
    }

    /// <summary>
    /// 현재 설정값을 파일에 저장한다.
    /// </summary>
    public static void Save()
    {
        // 현재 점수 설정을 파일에 저장한다.
        var settingsPath = ResolveSettingsPath();
        var config = new ConfigFile();
        config.SetValue(SettingsSection, "prefer_bonus", _current.PreferBonus);
        config.SetValue(SettingsSection, "avoid_penalty", _current.AvoidPenalty);
        config.SetValue(SettingsSection, "branch_weight", _current.BranchWeight);
        config.SetValue(SettingsSection, "elite_child_bonus", _current.EliteChildBonus);
        config.SetValue(SettingsSection, "act_weight", _current.ActWeight);
        config.SetValue(SettingsSection, "rest_elite_weight", _current.RestEliteWeight);
        config.SetValue(SettingsSection, "shop_weight", _current.ShopWeight);
        config.SetValue(SettingsSection, "health_weight", _current.HealthWeight);

        var error = config.Save(settingsPath);
        if (error != Error.Ok)
        {
            Log.Warn($"[PathTheSpire2] Score settings save failed: path={settingsPath}, error={error}");
            return;
        }

        Log.Warn($"[PathTheSpire2] Score settings saved: path={settingsPath} >> {Describe(_current)}");
    }

    /// <summary>
    /// 설정값을 로그용 문자열로 변환한다.
    /// </summary>
    public static string Describe(MapPathScoreTuning tuning)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"prefer={tuning.PreferBonus:0.##}, avoid={tuning.AvoidPenalty:0.##}, branch={tuning.BranchWeight:0.##}, eliteChild={tuning.EliteChildBonus:0.##}, act={tuning.ActWeight:0.##}, rest={tuning.RestEliteWeight:0.##}, shop={tuning.ShopWeight:0.##}, hp={tuning.HealthWeight:0.##}");
    }

    private static string ResolveSettingsPath()
    {
        // 실행 중인 모드 DLL 폴더를 기준으로 설정 파일 경로를 만든다.
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var baseDirectory = Path.GetDirectoryName(assemblyPath);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return SettingsFileName;
        }

        return Path.Combine(baseDirectory, SettingsFileName);
    }
}
