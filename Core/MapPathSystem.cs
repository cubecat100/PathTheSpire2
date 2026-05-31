#nullable enable
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace PathTheSpire2;

/// <summary>
/// 맵 경로 계산, 추천, 마커 표시를 관리한다.
/// </summary>
public partial class MapPathSystem : Node
{
    public const string NodeName = "MapPathSystem";

    // 맵 화면과 맵 좌표 관련 private 필드 참조다.
    private static readonly FieldInfo? MapField = AccessTools.Field(typeof(NMapScreen), "_map");
    private static readonly FieldInfo? RunStateField = AccessTools.Field(typeof(NMapScreen), "_runState");
    private static readonly FieldInfo? MapPointCoordField = AccessTools.Field(typeof(MapPoint), "coord");
    private static readonly FieldInfo? MapCoordColField = AccessTools.Field(typeof(MapCoord), "col");
    private static readonly FieldInfo? MapCoordRowField = AccessTools.Field(typeof(MapCoord), "row");

    private MapPointType _activeDisplayType = MapPointType.Unassigned;
    private readonly Dictionary<MapPoint, int> _activePathRanks = [];
    private MapPoint? _pendingSelectedRouteStartPoint;
    private MapPoint? _lastObservedCurrentPoint;

    public MapPathSystem()
    {
        Name = NodeName;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        SetProcess(true);
        var mapScreen = GetParentOrNull<NMapScreen>();
        _lastObservedCurrentPoint = GetCurrentMapPoint(mapScreen);
        Log.Warn($"[PathTheSpire2] MapPathSystem ready: parent={(mapScreen != null ? mapScreen.Name.ToString() : "<null>")}");
        DumpMapSnapshot();
        RefreshMarkers();
    }

    public override void _Process(double delta)
    {
        ObserveMapProgress();

        // 최초 추천 경로는 _Process에서 한 번 적용한다.
    }

    /// <summary>
    /// 맵 노드의 타입 마커와 경로 하이라이트를 갱신한다.
    /// </summary>
    private void RefreshMarkers()
    {
        var mapScreen = GetParentOrNull<NMapScreen>();
        if (mapScreen == null)
        {
            Log.Warn("[PathTheSpire2] RefreshMarkers skipped: parent map screen is null");
            return;
        }

        var allPoints = new List<MapVisualNode>();
        CollectMapPoints(mapScreen, allPoints);

        // 각 맵 노드에 연결된 마커 상태를 갱신한다.
        var affectedCount = 0;
        var createdMarkerCount = 0;
        foreach (var point in allPoints)
        {
            var marker = GetOrCreateMarker(point.Host, out var wasCreated);
            var pointType = point.Point.PointType;
            var shouldShow = _activeDisplayType != MapPointType.Unassigned && pointType == _activeDisplayType;
            _activePathRanks.TryGetValue(point.Point, out var pathRank);

            marker.SetTypeMarker(shouldShow ? pointType : MapPointType.Unassigned);
            marker.SetPathHighlightRank(pathRank);

            if (shouldShow == true)
            {
                affectedCount++;
            }

            if (wasCreated == true)
            {
                createdMarkerCount++;
            }
        }

        Log.Warn(
            $"[PathTheSpire2] RefreshMarkers completed: activeType={_activeDisplayType}, totalPoints={allPoints.Count}, affectedPoints={affectedCount}, pathPoints={_activePathRanks.Count}, createdMarkers={createdMarkerCount}");
    }

    private void SetHighlightedPaths(IReadOnlyList<PathEvaluation> rankedEvaluations)
    {
        _activePathRanks.Clear();

        // 상위 2개 경로의 노드에 순위를 기록
        for (var rankIndex = 0; rankIndex < rankedEvaluations.Count && rankIndex < 2; rankIndex++)
        {
            var rank = rankIndex + 1;
            foreach (var point in rankedEvaluations[rankIndex].Path)
            {
                if (_activePathRanks.TryGetValue(point, out var existingRank) == false || rank < existingRank)
                {
                    _activePathRanks[point] = rank;
                }
            }
        }

        Log.Warn($"[PathTheSpire2] Highlight paths updated: rankedPoints={_activePathRanks.Count}");
    }

    private static void CollectMapPoints(Node node, List<MapVisualNode> points)
    {
        // 맵 화면 트리를 재귀 순회하면서 표시 노드를 수집
        foreach (Node child in node.GetChildren())
        {
            if (child is NMapPoint mapPoint)
            {
                points.Add(new MapVisualNode(mapPoint, mapPoint.Point));
            }
            else if (child is NBossMapPoint bossPoint)
            {
                points.Add(new MapVisualNode(bossPoint, bossPoint.Point));
            }

            CollectMapPoints(child, points);
        }
    }

    /// <summary>
    /// 현재 점수 기준으로 추천 경로를 선택한다.
    /// </summary>
    public void SelectPreferredPathToBoss()
    {
        if (TrySelectPreferredPathToBoss(logFailure: true) == false)
        {
            return;
        }

        RefreshMarkers();
    }

    public void HandleMapOpened()
    {
        ObserveMapProgress();
        Log.Warn("[PathTheSpire2] Preferred path refresh requested >> reason=map_opened");
        SelectPreferredPathToBoss();
    }

    public void HandleMapPointSelected(MapPoint point)
    {
        _pendingSelectedRouteStartPoint = point;
        Log.Warn($"[PathTheSpire2] Preferred path refresh requested >> reason=map_point_selected, point={DescribeMapPoint(point)}");
        SelectPreferredPathToBoss();
    }

    /// <summary>
    /// 留??좏깮??寃쎈줈 ?쒖옉 ?몃뱶濡?湲곗뼱?쒕떎.
    /// </summary>
    private bool TrySelectPreferredPathToBoss(bool logFailure)
    {
        var mapScreen = GetParentOrNull<NMapScreen>();
        if (mapScreen == null)
        {
            if (logFailure == true)
            {
                Log.Warn("[PathTheSpire2] SelectPreferredPathToBoss failed: parent map screen is null");
            }
            _activePathRanks.Clear();
            return false;
        }

        var map = MapField?.GetValue(mapScreen) as ActMap;
        if (map == null)
        {
            if (logFailure == true)
            {
                Log.Warn("[PathTheSpire2] SelectPreferredPathToBoss failed: map field is null");
            }
            _activePathRanks.Clear();
            return false;
        }

        var bossPoint = map.BossMapPoint ?? map.SecondBossMapPoint;
        if (bossPoint == null)
        {
            if (logFailure == true)
            {
                Log.Warn("[PathTheSpire2] SelectPreferredPathToBoss failed: boss point is null");
            }
            _activePathRanks.Clear();
            return false;
        }

        var runState = RunStateField?.GetValue(mapScreen) as RunState;
        var startPoint = GetRouteStartPoint(map, runState);
        if (startPoint == null)
        {
            if (logFailure == true)
            {
                Log.Warn("[PathTheSpire2] SelectPreferredPathToBoss failed: route start point is null");
            }
            _activePathRanks.Clear();
            return false;
        }

        var player = runState?.Players.FirstOrDefault();
        var preferences = GetPreferenceSnapshot(mapScreen);
        var tuning = MapPathScoreSettings.Current;
        var context = CreateScoreContext(runState, player, preferences, bossPoint, tuning);

        Log.Warn($"[PathTheSpire2] Path scoring start >> start={DescribeMapPoint(startPoint)}, boss={DescribeMapPoint(bossPoint)}");
        Log.Warn($"[PathTheSpire2] Route start source >> selected={DescribeOptionalMapPoint(_pendingSelectedRouteStartPoint)}, current={DescribeOptionalMapPoint(runState?.CurrentMapPoint)}, fallback={DescribeOptionalMapPoint(map.StartingMapPoint)}");
        Log.Warn($"[PathTheSpire2] Preference snapshot >> {DescribePreferences(preferences)}");
        Log.Warn($"[PathTheSpire2] Score context >> act={context.ActIndex + 1}, gold={context.Gold}, hp={context.CurrentHp}/{context.MaxHp}, hpRatio={FormatScore(context.HpRatio)}");
        Log.Warn($"[PathTheSpire2] Score tuning >> {MapPathScoreSettings.Describe(context.Tuning)}");

        var evaluations = EvaluateAllPaths(startPoint, bossPoint, context);
        _activePathRanks.Clear();

        if (evaluations.Count == 0)
        {
            Log.Warn("[PathTheSpire2] Path scoring failed: no path was produced");
            return false;
        }

        var rankedEvaluations = evaluations
            .OrderByDescending(static evaluation => evaluation.TotalScore)
            .ToList();
        var bestScore = rankedEvaluations[0].TotalScore;
        var bestEvaluations = rankedEvaluations
            .Where(evaluation => Math.Abs(evaluation.TotalScore - bestScore) < 0.001)
            .ToList();

        // 최고 점수가 같은 경로 중 하나를 선택
        var selectedEvaluation = bestEvaluations[Random.Shared.Next(bestEvaluations.Count)];

        LogTopPathCandidates(evaluations);
        SetHighlightedPaths(rankedEvaluations);
        Log.Warn($"[PathTheSpire2] Path selected >> total={FormatScore(selectedEvaluation.TotalScore)}, {DescribeScoredPath(selectedEvaluation)}");
        return true;
    }

    /// <summary>
    /// 시작점부터 보스까지 랜덤 경로를 선택한다.
    /// </summary>

    /// <summary>
    /// 시작 노드부터 보스 노드까지의 후보 경로를 열거한다.
    /// </summary>
    public void SelectRandomPathToBoss()
    {
        var mapScreen = GetParentOrNull<NMapScreen>();
        if (mapScreen == null)
        {
            Log.Warn("[PathTheSpire2] SelectRandomPathToBoss failed: parent map screen is null");
            _activePathRanks.Clear();
            return;
        }

        var map = MapField?.GetValue(mapScreen) as ActMap;
        if (map == null)
        {
            Log.Warn("[PathTheSpire2] SelectRandomPathToBoss failed: map field is null");
            _activePathRanks.Clear();
            return;
        }

        var bossPoint = map.BossMapPoint ?? map.SecondBossMapPoint;
        var runState = RunStateField?.GetValue(mapScreen) as RunState;
        var startPoint = GetRouteStartPoint(map, runState);
        if (startPoint == null || bossPoint == null)
        {
            Log.Warn("[PathTheSpire2] SelectRandomPathToBoss failed: start or boss point is null");
            _activePathRanks.Clear();
            return;
        }

        Log.Warn($"[PathTheSpire2] Random path start >> start={DescribeMapPoint(startPoint)}, boss={DescribeMapPoint(bossPoint)}");
        Log.Warn($"[PathTheSpire2] Route start source >> selected={DescribeOptionalMapPoint(_pendingSelectedRouteStartPoint)}, current={DescribeOptionalMapPoint(runState?.CurrentMapPoint)}, fallback={DescribeOptionalMapPoint(map.StartingMapPoint)}");

        var path = BuildRandomPath(startPoint, bossPoint);
        _activePathRanks.Clear();
        if (path.Count == 0)
        {
            Log.Warn("[PathTheSpire2] Random path selection failed: no path was produced");
            RefreshMarkers();
            return;
        }

        SetHighlightedPaths([new PathEvaluation(path, [], 0.0)]);

        Log.Warn($"[PathTheSpire2] Random path done >> {string.Join(" -> ", path.ConvertAll(DescribeMapPoint))}");
        RefreshMarkers();
    }

    private static List<PathEvaluation> EvaluateAllPaths(MapPoint startPoint, MapPoint bossPoint, ScoreContext context)
    {
        var evaluations = new List<PathEvaluation>();
        var workingPath = new List<MapPoint>();
        var guard = 0;

        void Explore(MapPoint current)
        {
            // 시작점부터 보스까지의 후보 경로를 DFS로 열거
            if (guard > 16384)
            {
                return;
            }

            workingPath.Add(current);
            if (current == bossPoint)
            {
                evaluations.Add(EvaluatePath([.. workingPath], context));
                guard++;
                workingPath.RemoveAt(workingPath.Count - 1);
                return;
            }

            foreach (var child in current.Children)
            {
                Explore(child);
            }

            workingPath.RemoveAt(workingPath.Count - 1);
        }

        Explore(startPoint);

        if (guard > 16384)
        {
            Log.Warn("[PathTheSpire2] Path scoring stopped early: candidate guard exceeded");
        }

        return evaluations;
    }

    /// <summary>
    /// 단일 경로의 총점과 노드별 점수를 계산한다.
    /// </summary>
    private static PathEvaluation EvaluatePath(List<MapPoint> path, ScoreContext context)
    {
        var nodeScores = new List<NodeScore>(path.Count);
        double total = 0.0;
        for (var index = 0; index < path.Count; index++)
        {
            var point = path[index];

            // 경로 점수는 노드별 점수 합으로 계산
            var score = ScorePoint(path, index, point, context);
            nodeScores.Add(score);
            total += score.Total;
        }

        return new PathEvaluation(path, nodeScores, total);
    }

    /// <summary>
    /// 단일 노드의 점수 항목을 계산한다.
    /// </summary>
    private static NodeScore ScorePoint(IReadOnlyList<MapPoint> path, int index, MapPoint point, ScoreContext context)
    {
        // 노드 점수는 항목별 점수를 합산해서 계산
        var preference = GetPreferenceScore(point.PointType, context.Preferences, context.Tuning);
        var branch = GetBranchScore(point, context.Tuning);
        var act = GetActScore(point.PointType, context.ActIndex, context.Tuning);
        var restElite = GetRestEliteScore(path, index, point, context.HpRatio, context.Tuning);
        var shop = GetShopScore(path, index, point, context.ActIndex, context.Tuning);
        var hp = GetHealthScore(path, index, point, context.HpRatio, context.Tuning);
        var total = preference + branch + act + restElite + shop + hp;
        return new NodeScore(point, preference, branch, act, restElite, shop, hp, total);
    }

    private static double GetPreferenceScore(
        MapPointType pointType,
        IReadOnlyDictionary<PathPreferenceNodeKind, PathPreferenceBucket> preferences,
        MapPathScoreTuning tuning)
    {
        // 선호도 설정 대상인 노드 타입만 점수 계산에 사용
        if (TryGetPreferenceKind(pointType, out var kind) == false)
        {
            return 0.0;
        }

        var bucket = preferences.TryGetValue(kind, out var value) ? value : PathPreferenceBucket.Neutral;
        return bucket switch
        {
            PathPreferenceBucket.Prefer => tuning.PreferBonus,
            PathPreferenceBucket.Avoid => -tuning.AvoidPenalty,
            _ => 0.0
        };
    }

    private static double GetBranchScore(MapPoint point, MapPathScoreTuning tuning)
    {
        // 분기 점수는 자식 수 구간별 고정값을 사용
        double score = point.Children.Count switch
        {
            >= 3 => 5.5,
            2 => 2.5,
            1 => 0.5,
            _ => 0.0
        };
        score *= tuning.BranchWeight;

        // 자식 중 Elite가 있으면 추가 점수를 더
        foreach (var child in point.Children)
        {
            if (child.PointType == MapPointType.Elite)
            {
                score += tuning.EliteChildBonus;
                break;
            }
        }

        return score;
    }

    private static double GetActScore(MapPointType pointType, int actIndex, MapPathScoreTuning tuning)
    {
        // Act 보정은 Elite와 Unknown에 적용
        if (pointType == MapPointType.Elite)
        {
            var score = actIndex switch
            {
                0 => 6.0,
                1 => 2.0,
                _ => -6.0
            };
            return score * tuning.ActWeight;
        }

        if (pointType == MapPointType.Unknown)
        {
            var score = actIndex switch
            {
                0 => 3.0,
                1 => 1.0,
                _ => -4.0
            };
            return score * tuning.ActWeight;
        }

        return 0.0;
    }

    private static double GetRestEliteScore(
        IReadOnlyList<MapPoint> path,
        int index,
        MapPoint point,
        double hpRatio,
        MapPathScoreTuning tuning)
    {
        // Rest와 Elite 사이 거리 기반 점수를 계산
        double score = 0.0;

        if (point.PointType == MapPointType.RestSite)
        {
            var nextEliteDistance = FindDistanceToNext(path, index, MapPointType.Elite);
            if (nextEliteDistance >= 0 && nextEliteDistance <= 3)
            {
                score += (4 - nextEliteDistance) * (2.5 + ((1.0 - hpRatio) * 2.0));
            }
        }

        if (point.PointType == MapPointType.Elite)
        {
            var previousRestDistance = FindDistanceToPrevious(path, index, MapPointType.RestSite);
            if (previousRestDistance >= 0 && previousRestDistance <= 3)
            {
                score += (4 - previousRestDistance) * (2.0 + ((1.0 - hpRatio) * 1.5));
            }
        }

        return score * tuning.RestEliteWeight;
    }

    private static double GetShopScore(
        IReadOnlyList<MapPoint> path,
        int index,
        MapPoint point,
        int actIndex,
        MapPathScoreTuning tuning)
    {
        if (point.PointType != MapPointType.Shop)
        {
            return 0.0;
        }

        // Shop 점수는 Act 배수와 경로 위치 값을 사용
        var nearWeight = Math.Max(0, 7 - index);
        var farWeight = Math.Min(index, 7);
        var actMultiplier = actIndex switch
        {
            0 => 0.7,
            1 => 1.25,
            _ => 1.55
        };
        var distanceScore = (nearWeight * 1.1) + (farWeight * 0.45);
        return distanceScore * actMultiplier * tuning.ShopWeight;
    }

    private static double GetHealthScore(
        IReadOnlyList<MapPoint> path,
        int index,
        MapPoint point,
        double hpRatio,
        MapPathScoreTuning tuning)
    {
        // 체력 점수는 Rest와 Elite에 적용
        var closeness = Math.Max(0, 6 - index);
        if (point.PointType == MapPointType.RestSite)
        {
            return closeness * (1.0 - hpRatio) * 2.5 * tuning.HealthWeight;
        }

        if (point.PointType == MapPointType.Elite)
        {
            return closeness * (hpRatio - 0.55) * 3.5 * tuning.HealthWeight;
        }

        return 0.0;
    }

    private static int FindDistanceToNext(IReadOnlyList<MapPoint> path, int startIndex, MapPointType type)
    {
        // 현재 후보 경로에서 다음 타입 노드까지의 거리를 찾는다.
        for (var index = startIndex + 1; index < path.Count; index++)
        {
            if (path[index].PointType == type)
            {
                return index - startIndex;
            }
        }

        return -1;
    }

    private static int FindDistanceToPrevious(IReadOnlyList<MapPoint> path, int startIndex, MapPointType type)
    {
        for (var index = startIndex - 1; index >= 0; index--)
        {
            if (path[index].PointType == type)
            {
                return startIndex - index;
            }
        }

        return -1;
    }

    private static bool TryGetPreferenceKind(MapPointType pointType, out PathPreferenceNodeKind kind)
    {
        kind = pointType switch
        {
            MapPointType.Unknown => PathPreferenceNodeKind.Unknown,
            MapPointType.Shop => PathPreferenceNodeKind.Shop,
            MapPointType.RestSite => PathPreferenceNodeKind.RestSite,
            MapPointType.Elite => PathPreferenceNodeKind.Elite,
            _ => default
        };

        return pointType == MapPointType.Unknown
            || pointType == MapPointType.Shop
            || pointType == MapPointType.RestSite
            || pointType == MapPointType.Elite;
    }

    private static ScoreContext CreateScoreContext(
        RunState? runState,
        Player? player,
        IReadOnlyDictionary<PathPreferenceNodeKind, PathPreferenceBucket> preferences,
        MapPoint bossPoint,
        MapPathScoreTuning tuning)
    {
        // 점수 계산에 사용할 런타임 값을 ScoreContext로 묶는다.
        var actIndex = runState?.CurrentActIndex ?? 0;
        var gold = player?.Gold ?? 0;
        var currentHp = player?.Creature?.CurrentHp ?? 0;
        var maxHp = player?.Creature?.MaxHp ?? 1;
        var hpRatio = maxHp > 0 ? currentHp / (double)maxHp : 0.0;
        return new ScoreContext(actIndex, gold, currentHp, maxHp, hpRatio, preferences, bossPoint, tuning);
    }

    private MapPoint? GetRouteStartPoint(ActMap map, RunState? runState)
    {
        return _pendingSelectedRouteStartPoint ?? runState?.CurrentMapPoint ?? map.StartingMapPoint;
    }

    private static IReadOnlyDictionary<PathPreferenceNodeKind, PathPreferenceBucket> GetPreferenceSnapshot(NMapScreen mapScreen)
    {
        var panel = FindPreferencePanel(mapScreen);

        // 선호도 패널이 없으면 기본 Neutral 스냅샷을 사용
        return panel?.GetPreferenceSnapshot()
            ?? new Dictionary<PathPreferenceNodeKind, PathPreferenceBucket>
            {
                { PathPreferenceNodeKind.Unknown, PathPreferenceBucket.Neutral },
                { PathPreferenceNodeKind.Shop, PathPreferenceBucket.Neutral },
                { PathPreferenceNodeKind.RestSite, PathPreferenceBucket.Neutral },
                { PathPreferenceNodeKind.Elite, PathPreferenceBucket.Neutral }
            };
    }

    private static MapPathPreferencePanel? FindPreferencePanel(Node node)
    {
        // 맵 화면 트리 전체에서 선호도 패널을 찾는다.
        foreach (Node child in node.GetChildren())
        {
            if (child is MapPathPreferencePanel panel)
            {
                return panel;
            }

            var found = FindPreferencePanel(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static string DescribePreferences(IReadOnlyDictionary<PathPreferenceNodeKind, PathPreferenceBucket> preferences)
    {
        return $"U={DescribeBucket(preferences, PathPreferenceNodeKind.Unknown)}, S={DescribeBucket(preferences, PathPreferenceNodeKind.Shop)}, R={DescribeBucket(preferences, PathPreferenceNodeKind.RestSite)}, E={DescribeBucket(preferences, PathPreferenceNodeKind.Elite)}";
    }

    private static string DescribeBucket(IReadOnlyDictionary<PathPreferenceNodeKind, PathPreferenceBucket> preferences, PathPreferenceNodeKind kind)
    {
        return preferences.TryGetValue(kind, out var bucket) ? bucket.ToString() : PathPreferenceBucket.Neutral.ToString();
    }

    private static void LogTopPathCandidates(List<PathEvaluation> evaluations)
    {
        var topEvaluations = evaluations
            .OrderByDescending(static evaluation => evaluation.TotalScore)
            .Take(3)
            .ToList();

        // 상위 후보 경로 로그
        Log.Warn($"[PathTheSpire2] Path candidates >> count={evaluations.Count}");
        for (var index = 0; index < topEvaluations.Count; index++)
        {
            var evaluation = topEvaluations[index];
            Log.Warn($"[PathTheSpire2] Path candidate {index + 1} >> total={FormatScore(evaluation.TotalScore)}, {DescribeScoredPath(evaluation)}");
        }
    }

    private static string DescribeScoredPath(PathEvaluation evaluation)
    {
        var parts = new List<string>(evaluation.NodeScores.Count);
        foreach (var score in evaluation.NodeScores)
        {
            parts.Add($"{DescribeMapPoint(score.Point)}={FormatScore(score.Total)}");
        }

        return string.Join(" -> ", parts);
    }

    private static string FormatScore(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static List<MapPoint> BuildRandomPath(MapPoint startPoint, MapPoint bossPoint)
    {
        // 시작점부터 보스까지 랜덤 경로 생성
        var path = new List<MapPoint> { startPoint };
        var current = startPoint;
        var guard = 0;

        while (current != bossPoint)
        {
            guard++;
            if (guard > 256)
            {
                Log.Warn("[PathTheSpire2] Random path aborted: exceeded safety guard");
                return [];
            }

            var children = new List<MapPoint>(current.Children);
            if (children.Count == 0)
            {
                Log.Warn($"[PathTheSpire2] Random path aborted: node has no children: {DescribeMapPoint(current)}");
                return [];
            }

            var nextIndex = Random.Shared.Next(children.Count);
            var nextPoint = children[nextIndex];

            path.Add(nextPoint);
            current = nextPoint;
        }

        return path;
    }

    private void DumpMapSnapshot()
    {
        var mapScreen = GetParentOrNull<NMapScreen>();
        var map = mapScreen != null ? MapField?.GetValue(mapScreen) as ActMap : null;
        var runState = mapScreen != null ? RunStateField?.GetValue(mapScreen) as RunState : null;
        if (map == null)
        {
            Log.Warn("[PathTheSpire2] Map snapshot skipped: map is null");
            return;
        }

        var bossPoint = map.BossMapPoint ?? map.SecondBossMapPoint;
        Log.Warn(
            $"[PathTheSpire2] Map snapshot >> rows={map.GetRowCount()}, cols={map.GetColumnCount()}, start={DescribeMapPoint(map.StartingMapPoint)}, boss={(bossPoint != null ? DescribeMapPoint(bossPoint) : "<null>")}");
        Log.Warn($"[PathTheSpire2] Map current point >> {DescribeOptionalMapPoint(runState?.CurrentMapPoint)}");

        // 맵 행 단위 정보를 로그에 남긴다.
        for (int row = 0; row < map.GetRowCount(); row++)
        {
            var rowPoints = new List<string>();
            foreach (var point in map.GetPointsInRow(row))
            {
                rowPoints.Add(DescribeMapPoint(point));
            }

            Log.Warn($"[PathTheSpire2] Map row {row} >> {string.Join(", ", rowPoints)}");
        }
    }

    private static MapPathTestMarker GetOrCreateMarker(Control host, out bool wasCreated)
    {
        var marker = host.GetNodeOrNull<MapPathTestMarker>(MapPathTestMarker.NodeName);
        if (marker != null)
        {
            wasCreated = false;
            return marker;
        }

        marker = new MapPathTestMarker();
        host.AddChild(marker);
        wasCreated = true;
        return marker;
    }

    private static MapPointType GetNextDisplayType(MapPointType currentType)
    {
        return currentType switch
        {
            MapPointType.Unassigned => MapPointType.Monster,
            MapPointType.Monster => MapPointType.Elite,
            MapPointType.Elite => MapPointType.RestSite,
            MapPointType.RestSite => MapPointType.Treasure,
            MapPointType.Treasure => MapPointType.Shop,
            MapPointType.Shop => MapPointType.Unknown,
            MapPointType.Unknown => MapPointType.Unassigned,
            _ => MapPointType.Unassigned
        };
    }

    private static string DescribeNode(Node? node)
    {
        if (node == null)
        {
            return "<null>";
        }

        return $"{node.Name} ({node.GetType().FullName})";
    }

    private static string DescribeMapPoint(MapPoint point)
    {
        // 디버그용 축약 표기: [column,row,typeCode,childCount]
        var coord = MapPointCoordField?.GetValue(point);
        var col = MapCoordColField?.GetValue(coord) ?? "?";
        var row = MapCoordRowField?.GetValue(coord) ?? "?";
        return $"[{col},{row},{GetPointTypeCode(point.PointType)},{point.Children.Count}]";
    }

    internal static string DescribePointForLog(MapPoint? point)
    {
        return DescribeOptionalMapPoint(point);
    }

    private static string DescribeOptionalMapPoint(MapPoint? point)
    {
        return point != null ? DescribeMapPoint(point) : "<null>";
    }

    private void ObserveMapProgress()
    {
        var mapScreen = GetParentOrNull<NMapScreen>();
        var currentPoint = GetCurrentMapPoint(mapScreen);
        if (ReferenceEquals(currentPoint, _lastObservedCurrentPoint) == true)
        {
            return;
        }

        Log.Warn(
            $"[PathTheSpire2] Map progress changed >> previous={DescribeOptionalMapPoint(_lastObservedCurrentPoint)}, current={DescribeOptionalMapPoint(currentPoint)}");

        _lastObservedCurrentPoint = currentPoint;

        if (_pendingSelectedRouteStartPoint != null)
        {
            Log.Warn($"[PathTheSpire2] Pending selected route start cleared >> point={DescribeMapPoint(_pendingSelectedRouteStartPoint)}");
            _pendingSelectedRouteStartPoint = null;
        }
    }

    private static MapPoint? GetCurrentMapPoint(NMapScreen? mapScreen)
    {
        var runState = mapScreen != null ? RunStateField?.GetValue(mapScreen) as RunState : null;
        return runState?.CurrentMapPoint;
    }

    private static string GetPointTypeCode(MapPointType pointType)
    {
        return pointType switch
        {
            MapPointType.Monster => "M",
            MapPointType.Elite => "E",
            MapPointType.RestSite => "R",
            MapPointType.Treasure => "T",
            MapPointType.Shop => "S",
            MapPointType.Unknown => "U",
            MapPointType.Boss => "B",
            MapPointType.Ancient => "A",
            _ => "_"
        };
    }

    private readonly record struct ScoreContext(
        int ActIndex,
        int Gold,
        int CurrentHp,
        int MaxHp,
        double HpRatio,
        IReadOnlyDictionary<PathPreferenceNodeKind, PathPreferenceBucket> Preferences,
        MapPoint BossPoint,
        MapPathScoreTuning Tuning);

    private readonly record struct NodeScore(
        MapPoint Point,
        double Preference,
        double Branch,
        double Act,
        double RestElite,
        double Shop,
        double Health,
        double Total);

    private readonly record struct PathEvaluation(
        List<MapPoint> Path,
        List<NodeScore> NodeScores,
        double TotalScore);

    private readonly record struct MapVisualNode(Control Host, MapPoint Point);
}
