#nullable enable
using System.Globalization;

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
    public static readonly MapPathScoreTuning Defaults = new(
        PreferBonus: 7.0,
        AvoidPenalty: 9.0,
        BranchWeight: 4.0,
        EliteChildBonus: 4.0,
        ActWeight: 2.0,
        RestEliteWeight: 1.0,
        ShopWeight: 2.0,
        HealthWeight: 1.0);

    private static MapPathScoreTuning _current = Defaults;

    public static MapPathScoreTuning Current => _current;

    public static void Update(MapPathScoreTuning tuning)
    {
        _current = tuning;
    }

    public static string Describe(MapPathScoreTuning tuning)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"prefer={tuning.PreferBonus:0.##}, avoid={tuning.AvoidPenalty:0.##}, branch={tuning.BranchWeight:0.##}, eliteChild={tuning.EliteChildBonus:0.##}, act={tuning.ActWeight:0.##}, rest={tuning.RestEliteWeight:0.##}, shop={tuning.ShopWeight:0.##}, hp={tuning.HealthWeight:0.##}");
    }
}
