#nullable enable
using Godot;
using MegaCrit.Sts2.Core.Logging;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace PathTheSpire2;

public readonly record struct MapPathScoreTuning(
    double PreferBonus,
    double AvoidPenalty,
    double BranchWeight,
    double EliteChildBonus,
    double ActWeight,
    double RestEliteWeight,
    double ShopWeight,
    double HealthWeight);

public static class MapPathScoreSettings
{
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

    public static void Load()
    {
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

    public static void Update(MapPathScoreTuning tuning)
    {
        _current = tuning;
    }

    public static void Save()
    {
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

    public static string Describe(MapPathScoreTuning tuning)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"prefer={tuning.PreferBonus:0.##}, avoid={tuning.AvoidPenalty:0.##}, branch={tuning.BranchWeight:0.##}, eliteChild={tuning.EliteChildBonus:0.##}, act={tuning.ActWeight:0.##}, rest={tuning.RestEliteWeight:0.##}, shop={tuning.ShopWeight:0.##}, hp={tuning.HealthWeight:0.##}");
    }

    private static string ResolveSettingsPath()
    {
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var baseDirectory = Path.GetDirectoryName(assemblyPath);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return SettingsFileName;
        }

        return Path.Combine(baseDirectory, SettingsFileName);
    }
}
