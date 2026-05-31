#nullable enable
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace PathTheSpire2;

public enum PathPreferenceBucket
{
    Prefer,
    Neutral,
    Avoid
}

public enum PathPreferenceNodeKind
{
    Unknown,
    Shop,
    RestSite,
    Elite
}

/// <summary>
/// 선호도 블록 UI와 옵션 팝업을 관리한다.
/// </summary>
public partial class MapPathPreferencePanel : PanelContainer
{
    public const string NodeName = "MapPathPreferencePanel";

    // 패널과 블록의 고정 크기 값이다.
    internal const float BlockSize = 60.0f;
    internal const float BlockGap = 4.0f;
    internal const float AreaPadding = 2.0f;
    internal const float AreaWidth = (BlockSize * 4.0f) + (BlockGap * 3.0f) + (AreaPadding * 2.0f);
    internal const float AreaHeight = BlockSize + (AreaPadding * 2.0f);

    private static readonly FieldInfo? MapLegendField = AccessTools.Field(typeof(NMapScreen), "_mapLegend");
    private static readonly FieldInfo? LegendItemsField = AccessTools.Field(typeof(NMapScreen), "_legendItems");
    private static readonly FieldInfo? LegendItemPointTypeField = AccessTools.Field(typeof(NMapLegendItem), "_pointType");
    private static readonly FieldInfo? LegendItemIconField = AccessTools.Field(typeof(NMapLegendItem), "_icon");

    private readonly Dictionary<PathPreferenceBucket, MapPathPreferenceArea> _areas = [];
    private readonly Dictionary<PathPreferenceNodeKind, MapPathPreferenceBlock> _blocks = [];
    private readonly Dictionary<PathPreferenceNodeKind, PathPreferenceBucket> _preferences = [];
    private readonly Dictionary<string, SpinBox> _optionInputs = [];
    private Control? _legendRoot;
    private Control? _legendItems;
    private PopupPanel? _optionsPopup;
    private Button? _optionsButton;
    private bool _didLogLegendAttach;

    public MapPathPreferencePanel()
    {
        Name = NodeName;
        MouseFilter = MouseFilterEnum.Stop;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        if (GetChildCount() == 0)
        {
            BuildUi();
        }
    }

    public override void _Process(double delta)
    {
        RefreshLegendPlacement();
    }

    /// <summary>
    /// 패널을 범례 아래에 연결한다.
    /// </summary>
    public void AttachToLegend(NMapScreen mapScreen)
    {
        if (GetChildCount() == 0)
        {
            BuildUi();
        }

        _legendRoot = MapLegendField?.GetValue(mapScreen) as Control;
        _legendItems = LegendItemsField?.GetValue(mapScreen) as Control;
        if (_legendRoot == null)
        {
            Log.Warn("[PathTheSpire2] MapPathPreferencePanel legend attach skipped: map legend root is null");
            return;
        }

        if (GetParent() != _legendRoot)
        {
            GetParent()?.RemoveChild(this);
            _legendRoot.AddChild(this);
        }

        // 범례 아이콘을 블록에 적용한다.
        RefreshLegendPlacement();
        ApplyLegendIcons();
    }

    /// <summary>
    /// 블록을 지정한 에어리어로 이동시킨다.
    /// </summary>
    internal void MoveBlockToBucket(PathPreferenceNodeKind kind, PathPreferenceBucket bucket)
    {
        if (_blocks.TryGetValue(kind, out var block) == false)
        {
            return;
        }

        if (_areas.TryGetValue(bucket, out var area) == false)
        {
            return;
        }

        PlaceBlock(block, area.Content, area.Content.GetChildCount(), bucket);
    }

    /// <summary>
    /// 블록을 다른 블록 앞이나 뒤로 이동시킨다.
    /// </summary>
    internal void MoveBlockNextToBlock(PathPreferenceNodeKind movedKind, PathPreferenceNodeKind targetKind, bool insertAfter)
    {
        if (movedKind == targetKind)
        {
            return;
        }

        if (_blocks.TryGetValue(movedKind, out var movedBlock) == false)
        {
            return;
        }

        if (_blocks.TryGetValue(targetKind, out var targetBlock) == false)
        {
            return;
        }

        if (_preferences.TryGetValue(targetKind, out var targetBucket) == false)
        {
            return;
        }

        if (_areas.TryGetValue(targetBucket, out var area) == false)
        {
            return;
        }

        var targetContent = area.Content;
        var targetIndex = targetBlock.GetIndex();
        var currentParent = movedBlock.GetParent();
        var currentIndex = currentParent == targetContent ? movedBlock.GetIndex() : -1;

        // 같은 컨테이너 안에서 이동하는 경우 목표 인덱스를 보정한다.
        if (currentIndex >= 0 && currentIndex < targetIndex)
        {
            targetIndex--;
        }

        if (insertAfter == true)
        {
            targetIndex++;
        }

        PlaceBlock(movedBlock, targetContent, targetIndex, targetBucket);
    }

    internal bool TryGetDraggedKind(Variant data, out PathPreferenceNodeKind kind)
    {
        kind = default;
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }

        var dictionary = data.AsGodotDictionary();
        if (dictionary.ContainsKey("kind") == false)
        {
            return false;
        }

        var kindText = dictionary["kind"].AsString();
        return Enum.TryParse(kindText, out kind);
    }

    /// <summary>
    /// 현재 선호도 배치를 반환한다.
    /// </summary>
    internal IReadOnlyDictionary<PathPreferenceNodeKind, PathPreferenceBucket> GetPreferenceSnapshot()
    {
        return new Dictionary<PathPreferenceNodeKind, PathPreferenceBucket>(_preferences);
    }

    /// <summary>
    /// 패널 기본 UI를 생성한다.
    /// </summary>
    private void BuildUi()
    {
        SetAnchorsPreset(LayoutPreset.TopLeft);
        CustomMinimumSize = new Vector2(AreaWidth + 8.0f, 0.0f);
        Size = new Vector2(AreaWidth + 8.0f, 0.0f);
        SizeFlagsHorizontal = SizeFlags.ShrinkBegin;

        AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color("17202c"), new Color("324052")));

        var outer = new MarginContainer();
        outer.AddThemeConstantOverride("margin_left", 4);
        outer.AddThemeConstantOverride("margin_top", 4);
        outer.AddThemeConstantOverride("margin_right", 4);
        outer.AddThemeConstantOverride("margin_bottom", 4);
        AddChild(outer);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 6);
        outer.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 6);
        root.AddChild(header);

        var title = new Label();
        title.Text = "Path";
        title.AddThemeFontSizeOverride("font_size", 12);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);

        var randomButton = new Button();
        randomButton.Text = "Random";
        randomButton.CustomMinimumSize = new Vector2(58.0f, 24.0f);
        randomButton.FocusMode = Control.FocusModeEnum.None;
        randomButton.AddThemeStyleboxOverride("normal", CreateActionButtonStyle(new Color("31445c"), new Color("93b8df")));
        randomButton.AddThemeStyleboxOverride("hover", CreateActionButtonStyle(new Color("3d5675"), new Color("b5d2f0")));
        randomButton.AddThemeStyleboxOverride("pressed", CreateActionButtonStyle(new Color("253548"), new Color("93b8df")));
        randomButton.AddThemeColorOverride("font_color", new Color("eff6ff"));
        randomButton.AddThemeColorOverride("font_hover_color", new Color("ffffff"));
        randomButton.AddThemeColorOverride("font_pressed_color", new Color("ffffff"));
        randomButton.Pressed += OnRandomButtonPressed;
        header.AddChild(randomButton);

        var optionsButton = new Button();
        optionsButton.Text = "Options";
        optionsButton.CustomMinimumSize = new Vector2(60.0f, 24.0f);
        optionsButton.FocusMode = Control.FocusModeEnum.None;
        optionsButton.AddThemeStyleboxOverride("normal", CreateActionButtonStyle(new Color("2f3641"), new Color("74869a")));
        optionsButton.AddThemeStyleboxOverride("hover", CreateActionButtonStyle(new Color("3c4655"), new Color("a7b5c3")));
        optionsButton.AddThemeStyleboxOverride("pressed", CreateActionButtonStyle(new Color("252c35"), new Color("8d9fb1")));
        optionsButton.AddThemeColorOverride("font_color", new Color("eef4fb"));
        optionsButton.AddThemeColorOverride("font_hover_color", new Color("ffffff"));
        optionsButton.AddThemeColorOverride("font_pressed_color", new Color("ffffff"));
        optionsButton.Pressed += OnOptionsButtonPressed;
        header.AddChild(optionsButton);
        _optionsButton = optionsButton;

        _optionsPopup = BuildOptionsPopup();
        AddChild(_optionsPopup);

        AddArea(root, PathPreferenceBucket.Prefer);
        AddArea(root, PathPreferenceBucket.Neutral);
        AddArea(root, PathPreferenceBucket.Avoid);

        // 기본 시작 상태는 Neutral 줄이다.
        foreach (var kind in new[]
        {
            PathPreferenceNodeKind.Unknown,
            PathPreferenceNodeKind.Shop,
            PathPreferenceNodeKind.RestSite,
            PathPreferenceNodeKind.Elite
        })
        {
            var block = new MapPathPreferenceBlock(this, kind);
            _blocks[kind] = block;
            _preferences[kind] = PathPreferenceBucket.Neutral;
            _areas[PathPreferenceBucket.Neutral].Content.AddChild(block);
        }

        UpdateAreaCaptions();
    }

    /// <summary>
    /// 옵션 팝업 UI를 생성한다.
    /// </summary>
    private PopupPanel BuildOptionsPopup()
    {
        var popup = new PopupPanel();
        popup.Visible = false;
        popup.AddThemeStyleboxOverride("panel", CreateOptionsStyle());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 5);
        margin.AddThemeConstantOverride("margin_top", 5);
        margin.AddThemeConstantOverride("margin_right", 5);
        margin.AddThemeConstantOverride("margin_bottom", 5);
        popup.AddChild(margin);

        var grid = new GridContainer();
        grid.Columns = 4;
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        margin.AddChild(grid);

        var tuning = MapPathScoreSettings.Current;
        AddOptionInput(grid, "Prefer", "prefer_bonus", tuning.PreferBonus, 0.0, 40.0, 0.5);
        AddOptionInput(grid, "Avoid", "avoid_penalty", tuning.AvoidPenalty, 0.0, 40.0, 0.5);
        AddOptionInput(grid, "Branch", "branch_weight", tuning.BranchWeight, 0.0, 4.0, 0.1);
        AddOptionInput(grid, "Elite+", "elite_child_bonus", tuning.EliteChildBonus, 0.0, 16.0, 0.5);
        AddOptionInput(grid, "Act", "act_weight", tuning.ActWeight, 0.0, 4.0, 0.1);
        AddOptionInput(grid, "Rest", "rest_elite_weight", tuning.RestEliteWeight, 0.0, 4.0, 0.1);
        AddOptionInput(grid, "Shop", "shop_weight", tuning.ShopWeight, 0.0, 4.0, 0.1);
        AddOptionInput(grid, "HP", "health_weight", tuning.HealthWeight, 0.0, 4.0, 0.1);

        return popup;
    }

    private void AddOptionInput(
        GridContainer grid,
        string labelText,
        string key,
        double initialValue,
        double minValue,
        double maxValue,
        double step)
    {
        var label = new Label();
        label.Text = labelText;
        label.AddThemeFontSizeOverride("font_size", 10);
        label.CustomMinimumSize = new Vector2(40.0f, 0.0f);
        var tooltip = GetOptionTooltip(key);
        label.TooltipText = tooltip;
        grid.AddChild(label);

        var input = new SpinBox();
        input.MinValue = minValue;
        input.MaxValue = maxValue;
        input.Step = step;
        input.AllowGreater = false;
        input.AllowLesser = false;
        input.Value = initialValue;
        input.CustomMinimumSize = new Vector2(56.0f, 22.0f);
        input.SelectAllOnFocus = true;
        input.GetLineEdit().Alignment = HorizontalAlignment.Center;
        input.TooltipText = tooltip;
        input.ValueChanged += _ => OnOptionValueChanged();
        grid.AddChild(input);

        _optionInputs[key] = input;
    }

    private void AddArea(VBoxContainer root, PathPreferenceBucket bucket)
    {
        var area = new MapPathPreferenceArea(this, bucket);
        _areas[bucket] = area;
        root.AddChild(area);
    }

    private void UpdateAreaCaptions()
    {
        // 각 에어리어의 블록 개수를 캡션에 반영한다.
        foreach (var (bucket, area) in _areas)
        {
            var count = 0;
            foreach (var value in _preferences.Values)
            {
                if (value == bucket)
                {
                    count++;
                }
            }

            area.SetCount(count);
        }
    }

    private void LogLayoutSnapshot(PathPreferenceNodeKind movedKind, PathPreferenceBucket targetBucket)
    {
        Log.Warn(
            $"[PathTheSpire2] Preference UI moved: block={movedKind}, target={targetBucket}, prefer={DescribeBucket(PathPreferenceBucket.Prefer)}, default={DescribeBucket(PathPreferenceBucket.Neutral)}, avoid={DescribeBucket(PathPreferenceBucket.Avoid)}");
    }

    private string DescribeBucket(PathPreferenceBucket bucket)
    {
        if (_areas.TryGetValue(bucket, out var area) == false)
        {
            return "[]";
        }

        var items = new List<string>();
        var index = 0;
        foreach (Node child in area.Content.GetChildren())
        {
            if (child is not MapPathPreferenceBlock block)
            {
                continue;
            }

            items.Add($"{block.Kind}@{index}");
            index++;
        }

        return $"[{string.Join(", ", items)}]";
    }

    private void PlaceBlock(MapPathPreferenceBlock block, HBoxContainer targetContent, int targetIndex, PathPreferenceBucket targetBucket)
    {
        var currentParent = block.GetParent();
        if (currentParent != null)
        {
            currentParent.RemoveChild(block);
        }

        targetContent.AddChild(block);

        var clampedIndex = Math.Clamp(targetIndex, 0, targetContent.GetChildCount() - 1);
        targetContent.MoveChild(block, clampedIndex);

        _preferences[block.Kind] = targetBucket;
        UpdateAreaCaptions();
        LogLayoutSnapshot(block.Kind, targetBucket);

        // 경로 재계산 요청은 deferred 호출로 전달한다.
        CallDeferred(MethodName.RequestPreferredPathRefreshDeferred);
    }

    /// <summary>
    /// 범례 기준으로 패널 위치를 갱신한다.
    /// </summary>
    private void RefreshLegendPlacement()
    {
        if (_legendRoot == null)
        {
            return;
        }

        SetAnchorsPreset(LayoutPreset.TopLeft);

        // 범례 아이템 컨테이너가 있으면 그 아래에 배치한다.
        if (_legendItems != null)
        {
            Position = _legendItems.Position + new Vector2(0.0f, _legendItems.Size.Y + 6.0f);
        }
        else
        {
            Position = new Vector2(0.0f, 6.0f);
        }

        if (_didLogLegendAttach == false)
        {
            Log.Warn(
                $"[PathTheSpire2] MapPathPreferencePanel attached to legend: parent={_legendRoot.Name}, legendItems={(_legendItems != null ? _legendItems.Name.ToString() : "<null>")}");
            _didLogLegendAttach = true;
        }
    }

    private void OnOptionsButtonPressed()
    {
        if (_optionsPopup == null || _optionsButton == null)
        {
            return;
        }

        if (_optionsPopup.Visible == true)
        {
            _optionsPopup.Hide();
            Log.Warn("[PathTheSpire2] Score options popup hidden");
            return;
        }

        _optionsPopup.ResetSize();
        var popupSize = new Vector2I((int)Mathf.Ceil(AreaWidth + 14.0f), 74);
        var globalPosition = _optionsButton.GetGlobalPosition() + new Vector2(0.0f, _optionsButton.Size.Y + 4.0f);
        var popupPosition = new Vector2I((int)Mathf.Round(globalPosition.X), (int)Mathf.Round(globalPosition.Y));
        var popupRect = new Rect2I(popupPosition, popupSize);
        _optionsPopup.Popup(popupRect);
        Log.Warn($"[PathTheSpire2] Score options popup shown: pos={popupRect.Position}, size={popupRect.Size}");
    }

    private void OnRandomButtonPressed()
    {
        var mapScreen = FindMapScreen();
        if (mapScreen == null)
        {
            Log.Warn("[PathTheSpire2] Random button ignored: map screen not found");
            return;
        }

        var system = mapScreen.GetNodeOrNull<MapPathSystem>(MapPathSystem.NodeName);
        if (system == null)
        {
            Log.Warn("[PathTheSpire2] Random button ignored: MapPathSystem not found");
            return;
        }

        Log.Warn("[PathTheSpire2] Random button pressed");
        system.SelectRandomPathToBoss();
    }

    /// <summary>
    /// 옵션 입력값을 점수 설정에 반영한다.
    /// </summary>
    private void OnOptionValueChanged()
    {
        if (_optionInputs.Count == 0)
        {
            return;
        }

        // 옵션 입력값으로 점수 가중치 구조체를 다시 만든다.
        var tuning = new MapPathScoreTuning(
            PreferBonus: GetOptionValue("prefer_bonus", MapPathScoreSettings.Defaults.PreferBonus),
            AvoidPenalty: GetOptionValue("avoid_penalty", MapPathScoreSettings.Defaults.AvoidPenalty),
            BranchWeight: GetOptionValue("branch_weight", MapPathScoreSettings.Defaults.BranchWeight),
            EliteChildBonus: GetOptionValue("elite_child_bonus", MapPathScoreSettings.Defaults.EliteChildBonus),
            ActWeight: GetOptionValue("act_weight", MapPathScoreSettings.Defaults.ActWeight),
            RestEliteWeight: GetOptionValue("rest_elite_weight", MapPathScoreSettings.Defaults.RestEliteWeight),
            ShopWeight: GetOptionValue("shop_weight", MapPathScoreSettings.Defaults.ShopWeight),
            HealthWeight: GetOptionValue("health_weight", MapPathScoreSettings.Defaults.HealthWeight));

        MapPathScoreSettings.Update(tuning);
        MapPathScoreSettings.Save();
        Log.Warn($"[PathTheSpire2] Score options changed >> {MapPathScoreSettings.Describe(tuning)}");
        CallDeferred(MethodName.RequestPreferredPathRefreshDeferred);
    }

    private double GetOptionValue(string key, double fallback)
    {
        return _optionInputs.TryGetValue(key, out var input) ? input.Value : fallback;
    }

    private static string GetOptionTooltip(string key)
    {
        return key switch
        {
            "prefer_bonus" => "Stronger bonus for nodes placed in Prefer.",
            "avoid_penalty" => "Stronger penalty for nodes placed in Avoid.",
            "branch_weight" => "How much branch count affects the score.",
            "elite_child_bonus" => "Extra bonus when a child path includes an Elite.",
            "act_weight" => "Strength of Act-based Elite and Unknown scoring.",
            "rest_elite_weight" => "Strength of Rest and Elite synergy scoring.",
            "shop_weight" => "Strength of Act and distance based Shop scoring.",
            "health_weight" => "Strength of HP-based Rest and Elite scoring.",
            _ => string.Empty
        };
    }

    private void RequestPreferredPathRefresh()
    {
        var mapScreen = FindMapScreen();
        if (mapScreen == null)
        {
            Log.Warn("[PathTheSpire2] Preference refresh skipped: map screen not found");
            return;
        }

        var system = mapScreen.GetNodeOrNull<MapPathSystem>(MapPathSystem.NodeName);
        if (system == null)
        {
            Log.Warn("[PathTheSpire2] Preference refresh skipped: MapPathSystem not found");
            return;
        }

        Log.Warn("[PathTheSpire2] Preference change requested path refresh");
        system.SelectPreferredPathToBoss();
    }

    private void RequestPreferredPathRefreshDeferred()
    {
        Log.Warn("[PathTheSpire2] Preference change queued deferred path refresh");
        RequestPreferredPathRefresh();
    }

    private NMapScreen? FindMapScreen()
    {
        Node? current = this;
        while (current != null)
        {
            if (current is NMapScreen mapScreen)
            {
                return mapScreen;
            }

            current = current.GetParent();
        }

        return null;
    }

    /// <summary>
    /// 범례 아이콘을 블록에 적용한다.
    /// </summary>
    private void ApplyLegendIcons()
    {
        var iconByType = new Dictionary<MapPointType, Texture2D>();
        if (_legendItems != null)
        {
            // 범례에서 노드 타입별 아이콘 텍스처를 읽는다.
            foreach (Node child in _legendItems.GetChildren())
            {
                if (child is not NMapLegendItem legendItem)
                {
                    continue;
                }

                if (LegendItemPointTypeField?.GetValue(legendItem) is not MapPointType pointType)
                {
                    continue;
                }

                if (LegendItemIconField?.GetValue(legendItem) is not TextureRect iconRect || iconRect.Texture == null)
                {
                    continue;
                }

                iconByType[pointType] = iconRect.Texture;
            }
        }

        foreach (var (kind, block) in _blocks)
        {
            var pointType = GetMapPointType(kind);
            iconByType.TryGetValue(pointType, out var texture);
            block.SetIconTexture(texture);
        }

        Log.Warn($"[PathTheSpire2] MapPathPreferencePanel icon refresh complete: icons={iconByType.Count}");
    }

    private static MapPointType GetMapPointType(PathPreferenceNodeKind kind)
    {
        return kind switch
        {
            PathPreferenceNodeKind.Unknown => MapPointType.Unknown,
            PathPreferenceNodeKind.Shop => MapPointType.Shop,
            PathPreferenceNodeKind.RestSite => MapPointType.RestSite,
            PathPreferenceNodeKind.Elite => MapPointType.Elite,
            _ => MapPointType.Unassigned
        };
    }

    private static StyleBoxFlat CreatePanelStyle(Color bgColor, Color borderColor)
    {
        return new StyleBoxFlat
        {
            BgColor = bgColor,
            BorderColor = borderColor,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            ContentMarginBottom = 8,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 8
        };
    }

    private static StyleBoxFlat CreateActionButtonStyle(Color bgColor, Color borderColor)
    {
        return new StyleBoxFlat
        {
            BgColor = bgColor,
            BorderColor = borderColor,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ContentMarginBottom = 4,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4
        };
    }

    private static StyleBoxFlat CreateOptionsStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color("101821"),
            BorderColor = new Color("46586d"),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ContentMarginBottom = 4,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 4
        };
    }
}

/// <summary>
/// 선호도 한 줄의 드롭 영역이다.
/// </summary>
public partial class MapPathPreferenceArea : PanelContainer
{
    private readonly MapPathPreferencePanel _panel;
    private readonly PathPreferenceBucket _bucket;
    private readonly Label _caption;

    public HBoxContainer Content { get; } = new();

    public MapPathPreferenceArea(MapPathPreferencePanel panel, PathPreferenceBucket bucket)
    {
        _panel = panel;
        _bucket = bucket;
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(MapPathPreferencePanel.AreaWidth, MapPathPreferencePanel.AreaHeight);
        Size = new Vector2(MapPathPreferencePanel.AreaWidth, MapPathPreferencePanel.AreaHeight);
        SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;

        AddThemeStyleboxOverride("panel", CreateAreaStyle(bucket));

        _caption = new Label();
        _caption.Text = GetBucketTitle(bucket);
        _caption.AddThemeFontSizeOverride("font_size", 10);
        _caption.Modulate = new Color(0.86f, 0.9f, 0.95f, 0.72f);
        _caption.MouseFilter = MouseFilterEnum.Ignore;
        _caption.Position = new Vector2(6.0f, -7.0f);
        AddChild(_caption);

        // 블록 배치는 내부 HBox가 담당한다.
        Content.AddThemeConstantOverride("separation", (int)MapPathPreferencePanel.BlockGap);
        Content.Alignment = BoxContainer.AlignmentMode.Begin;
        Content.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(Content);
    }

    public void SetCount(int count)
    {
        _caption.Text = count > 0 ? $"{GetBucketTitle(_bucket)} {count}" : GetBucketTitle(_bucket);
    }

    public override void _Ready()
    {
        Content.SetAnchorsPreset(LayoutPreset.FullRect);
        Content.OffsetLeft = MapPathPreferencePanel.AreaPadding;
        Content.OffsetTop = MapPathPreferencePanel.AreaPadding;
        Content.OffsetRight = -MapPathPreferencePanel.AreaPadding;
        Content.OffsetBottom = -MapPathPreferencePanel.AreaPadding;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return _panel.TryGetDraggedKind(data, out _);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_panel.TryGetDraggedKind(data, out var kind) == false)
        {
            return;
        }

        // 에어리어에 드롭한 블록을 해당 줄로 이동시킨다.
        _panel.MoveBlockToBucket(kind, _bucket);
    }

    private static string GetBucketTitle(PathPreferenceBucket bucket)
    {
        return bucket switch
        {
            PathPreferenceBucket.Prefer => "Prefer",
            PathPreferenceBucket.Neutral => "Default",
            PathPreferenceBucket.Avoid => "Avoid",
            _ => "Area"
        };
    }

    private static StyleBoxFlat CreateAreaStyle(PathPreferenceBucket bucket)
    {
        var style = new StyleBoxFlat
        {
            BgColor = bucket switch
            {
                PathPreferenceBucket.Prefer => new Color("183126"),
                PathPreferenceBucket.Neutral => new Color("263241"),
                PathPreferenceBucket.Avoid => new Color("352226"),
                _ => new Color("263241")
            },
            BorderColor = bucket switch
            {
                PathPreferenceBucket.Prefer => new Color("49a86f"),
                PathPreferenceBucket.Neutral => new Color("5f748c"),
                PathPreferenceBucket.Avoid => new Color("c46d6d"),
                _ => new Color("5f748c")
            },
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            ContentMarginBottom = 4,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 4
        };

        return style;
    }
}

/// <summary>
/// 드래그 가능한 선호도 블록이다.
/// </summary>
public partial class MapPathPreferenceBlock : PanelContainer
{
    private readonly MapPathPreferencePanel _panel;
    private readonly TextureRect _iconRect = new();
    private readonly Label _fallbackLabel = new();

    public PathPreferenceNodeKind Kind { get; }

    public MapPathPreferenceBlock(MapPathPreferencePanel panel, PathPreferenceNodeKind kind)
    {
        _panel = panel;
        Kind = kind;
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(MapPathPreferencePanel.BlockSize, MapPathPreferencePanel.BlockSize);
        Size = new Vector2(MapPathPreferencePanel.BlockSize, MapPathPreferencePanel.BlockSize);
        SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        TooltipText = "Drag to move between areas";

        AddThemeStyleboxOverride("panel", CreateBlockStyle());

        // 아이콘은 블록 내부 여백 안에 채운다.
        _iconRect.SetAnchorsPreset(LayoutPreset.FullRect);
        _iconRect.OffsetLeft = 4.0f;
        _iconRect.OffsetTop = 4.0f;
        _iconRect.OffsetRight = -4.0f;
        _iconRect.OffsetBottom = -4.0f;
        _iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _iconRect.CustomMinimumSize = Vector2.Zero;
        _iconRect.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_iconRect);

        _fallbackLabel.Text = GetKindLabel(kind);
        _fallbackLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        _fallbackLabel.MouseFilter = MouseFilterEnum.Ignore;
        _fallbackLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _fallbackLabel.VerticalAlignment = VerticalAlignment.Center;
        _fallbackLabel.Visible = false;
        AddChild(_fallbackLabel);
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        // 드래그 프리뷰를 별도로 만든다.
        var preview = new PanelContainer();
        preview.AddThemeStyleboxOverride("panel", CreateBlockStyle());
        preview.CustomMinimumSize = new Vector2(MapPathPreferencePanel.BlockSize, MapPathPreferencePanel.BlockSize);

        if (_iconRect.Texture != null)
        {
            var previewIcon = new TextureRect();
            previewIcon.SetAnchorsPreset(LayoutPreset.FullRect);
            previewIcon.OffsetLeft = 4.0f;
            previewIcon.OffsetTop = 4.0f;
            previewIcon.OffsetRight = -4.0f;
            previewIcon.OffsetBottom = -4.0f;
            previewIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            previewIcon.Texture = _iconRect.Texture;
            previewIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            previewIcon.CustomMinimumSize = Vector2.Zero;
            preview.AddChild(previewIcon);
        }
        else
        {
            var previewLabel = new Label();
            previewLabel.SetAnchorsPreset(LayoutPreset.FullRect);
            previewLabel.Text = GetKindLabel(Kind);
            previewLabel.HorizontalAlignment = HorizontalAlignment.Center;
            previewLabel.VerticalAlignment = VerticalAlignment.Center;
            preview.AddChild(previewLabel);
        }

        SetDragPreview(preview);

        var data = new Godot.Collections.Dictionary
        {
            { "kind", Kind.ToString() }
        };

        return Variant.From(data);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return _panel.TryGetDraggedKind(data, out var draggedKind) && draggedKind != Kind;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_panel.TryGetDraggedKind(data, out var draggedKind) == false || draggedKind == Kind)
        {
            return;
        }

        // 드롭 위치의 좌우에 따라 삽입 위치를 결정한다.
        var insertAfter = atPosition.X >= Size.X * 0.5f;
        _panel.MoveBlockNextToBlock(draggedKind, Kind, insertAfter);
    }

    /// <summary>
    /// 블록 아이콘을 갱신한다.
    /// </summary>
    public void SetIconTexture(Texture2D? texture)
    {
        _iconRect.Texture = texture;
        _iconRect.Visible = texture != null;
        _fallbackLabel.Visible = texture == null;
        TooltipText = GetKindLabel(Kind);
    }

    private static string GetKindLabel(PathPreferenceNodeKind kind)
    {
        return kind switch
        {
            PathPreferenceNodeKind.Unknown => "Unknown",
            PathPreferenceNodeKind.Shop => "Shop",
            PathPreferenceNodeKind.RestSite => "Rest",
            PathPreferenceNodeKind.Elite => "Elite",
            _ => kind.ToString()
        };
    }

    private static StyleBoxFlat CreateBlockStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color("eff3f7"),
            BorderColor = new Color("0d1621"),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ContentMarginBottom = 2,
            ContentMarginLeft = 2,
            ContentMarginRight = 2,
            ContentMarginTop = 2
        };
    }
}
