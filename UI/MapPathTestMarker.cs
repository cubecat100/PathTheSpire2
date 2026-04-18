#nullable enable
using Godot;
using MegaCrit.Sts2.Core.Map;

namespace PathTheSpire2;

public partial class MapPathTestMarker : Control
{
    public const string NodeName = "MapPathTestMarker";

    private static readonly Color ShadowColor = new(0.06f, 0.08f, 0.1f, 0.58f);
    private static readonly Color HighlightColor = new(1.0f, 1.0f, 1.0f, 0.14f);
    private static readonly Color PathColorRank1 = new(0.49f, 0.97f, 1.0f, 0.74f);
    private static readonly Color PathShadowColorRank1 = new(0.02f, 0.15f, 0.18f, 0.54f);
    private static readonly Color PathColorRank2 = new(0.44f, 0.76f, 1.0f, 0.42f);
    private static readonly Color PathShadowColorRank2 = new(0.05f, 0.12f, 0.18f, 0.28f);
    private const float RingPadding = 9.0f;
    private const float OuterWidth = 5.0f;
    private const float InnerWidth = 2.5f;

    public MapPointType TypeMarkerType { get; private set; } = MapPointType.Unassigned;
    public int PathHighlightRank { get; private set; }

    public MapPathTestMarker()
    {
        Name = NodeName;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetLeft = -RingPadding;
        OffsetTop = -RingPadding;
        OffsetRight = RingPadding;
        OffsetBottom = RingPadding;
        ShowBehindParent = true;
        ZIndex = 0;
        Visible = false;
    }

    public void SetTypeMarker(MapPointType markerType)
    {
        TypeMarkerType = markerType;
        UpdateVisibility();
        QueueRedraw();
    }

    public void SetPathHighlightRank(int rank)
    {
        PathHighlightRank = rank;
        UpdateVisibility();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Visible == false)
        {
            return;
        }

        var size = Size;
        var center = size * 0.5f;
        var radius = Mathf.Min(size.X, size.Y) * 0.5f - (OuterWidth * 0.5f) - 1.0f;

        if (PathHighlightRank > 0)
        {
            var pathShadowColor = GetPathShadowColor(PathHighlightRank);
            var pathColor = GetPathColor(PathHighlightRank);
            var widthOffset = PathHighlightRank == 1 ? 1.0f : 0.0f;
            DrawArc(center, radius + 4.0f, 0.0f, Mathf.Tau, 48, pathShadowColor, OuterWidth + 2.0f + widthOffset, true);
            DrawArc(center, radius + 2.5f, 0.0f, Mathf.Tau, 48, pathColor, OuterWidth + 1.0f + widthOffset, true);
        }

        if (TypeMarkerType != MapPointType.Unassigned)
        {
            var color = GetMarkerColor(TypeMarkerType);
            DrawArc(center, radius + 1.5f, 0.0f, Mathf.Tau, 48, ShadowColor, OuterWidth + 1.0f, true);
            DrawArc(center, radius, 0.0f, Mathf.Tau, 48, color, OuterWidth, true);
            DrawArc(center, radius - 4.0f, -0.9f, 2.2f, 24, HighlightColor, InnerWidth, true);
        }
    }

    private static Color GetMarkerColor(MapPointType markerType)
    {
        return markerType switch
        {
            MapPointType.Monster => new Color(1.0f, 0.42f, 0.42f, 0.72f),
            MapPointType.Elite => new Color(1.0f, 0.62f, 0.26f, 0.72f),
            MapPointType.RestSite => new Color(0.30f, 0.82f, 0.22f, 0.72f),
            MapPointType.Treasure => new Color(0.99f, 0.79f, 0.34f, 0.72f),
            MapPointType.Shop => new Color(0.33f, 0.63f, 1.0f, 0.72f),
            MapPointType.Unknown => new Color(0.78f, 0.84f, 0.90f, 0.68f),
            _ => new Color(1.0f, 1.0f, 1.0f, 0.72f)
        };
    }

    private static Color GetPathColor(int rank)
    {
        return rank switch
        {
            1 => PathColorRank1,
            2 => PathColorRank2,
            _ => Colors.Transparent
        };
    }

    private static Color GetPathShadowColor(int rank)
    {
        return rank switch
        {
            1 => PathShadowColorRank1,
            2 => PathShadowColorRank2,
            _ => Colors.Transparent
        };
    }

    private void UpdateVisibility()
    {
        Visible = PathHighlightRank > 0 || TypeMarkerType != MapPointType.Unassigned;
    }
}
