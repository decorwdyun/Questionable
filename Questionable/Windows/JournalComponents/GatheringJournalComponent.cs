using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using ImGuiNET;
using LLib.GameData;
using Lumina.Excel.GeneratedSheets;
using Questionable.Controller;
using Questionable.Model;
using Questionable.Model.Gathering;

namespace Questionable.Windows.JournalComponents;

internal sealed class GatheringJournalComponent
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly UiUtils _uiUtils;
    private readonly GatheringPointRegistry _gatheringPointRegistry;
    private readonly Dictionary<int, string> _gatheringItems;
    private readonly List<ExpansionPoints> _gatheringPoints;
    private readonly List<ushort> _gatheredItems = [];

    private delegate byte GetIsGatheringItemGatheredDelegate(ushort item);

    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 8B D9 8B F9")]
    private GetIsGatheringItemGatheredDelegate _getIsGatheringItemGathered = null!;

    internal bool IsGatheringItemGathered(uint item) => _getIsGatheringItemGathered((ushort)item) != 0;

    public GatheringJournalComponent(IDataManager dataManager, IDalamudPluginInterface pluginInterface, UiUtils uiUtils,
        IGameInteropProvider gameInteropProvider, GatheringPointRegistry gatheringPointRegistry)
    {
        _pluginInterface = pluginInterface;
        _uiUtils = uiUtils;
        _gatheringPointRegistry = gatheringPointRegistry;
        var routeToGatheringPoint = dataManager.GetExcelSheet<GatheringLeveRoute>()!
            .Where(x => x.UnkData0[0].GatheringPoint != 0)
            .SelectMany(x => x.UnkData0
                .Where(y => y.GatheringPoint != 0)
                .Select(y => new
                {
                    RouteId = x.RowId,
                    GatheringPointId = y.GatheringPoint
                }))
            .GroupBy(x => x.RouteId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.GatheringPointId).ToList());
        var gatheringLeveSheet = dataManager.GetExcelSheet<GatheringLeve>()!;
        var territoryTypeSheet = dataManager.GetExcelSheet<TerritoryType>()!;
        var gatheringPointToLeve = dataManager.GetExcelSheet<Leve>()!
            .Where(x => x.RowId > 0)
            .Select(x =>
            {
                uint startZonePlaceName = x.PlaceNameStartZone.Row;
                startZonePlaceName = startZonePlaceName switch
                {
                    27 => 28, // limsa
                    39 => 52, // gridania
                    51 => 40, // uldah
                    62 => 2300, // ishgard
                    _ => startZonePlaceName
                };

                var territoryType = territoryTypeSheet.FirstOrDefault(y => startZonePlaceName == y.PlaceName.Row)
                                    ?? throw new InvalidOperationException($"Unable to use {startZonePlaceName}");
                return new
                {
                    LeveId = x.RowId,
                    LeveName = x.Name.ToString(),
                    TerritoryType = (ushort)territoryType.RowId,
                    TerritoryName = territoryType.Name.ToString(),
                    GatheringLeve = gatheringLeveSheet.GetRow((uint)x.DataId),
                };
            })
            .Where(x => x.GatheringLeve != null)
            .Select(x => new
            {
                x.LeveId,
                x.LeveName,
                x.TerritoryType,
                x.TerritoryName,
                GatheringPoints = x.GatheringLeve!.Route
                    .Where(y => y.Row != 0)
                    .SelectMany(y => routeToGatheringPoint[y.Row]),
            })
            .SelectMany(x => x.GatheringPoints.Select(y => new
            {
                x.LeveId,
                x.LeveName,
                x.TerritoryType,
                x.TerritoryName,
                GatheringPointId = y
            }))
            .GroupBy(x => x.GatheringPointId)
            .ToDictionary(x => x.Key, x => x.First());

        var itemSheet = dataManager.GetExcelSheet<Item>()!;

        _gatheringItems = dataManager.GetExcelSheet<GatheringItem>()!
            .Where(x => x.RowId != 0 && x.GatheringItemLevel.Row != 0)
            .Select(x => new
            {
                GatheringItemId = (int)x.RowId,
                Name = itemSheet.GetRow((uint)x.Item)?.Name.ToString()
            })
            .Where(x => !string.IsNullOrEmpty(x.Name))
            .ToDictionary(x => x.GatheringItemId, x => x.Name!);

        _gatheringPoints = dataManager.GetExcelSheet<GatheringPoint>()!
            .Where(x => x.GatheringPointBase.Row != 0)
            .DistinctBy(x => x.GatheringPointBase.Row)
            .Select(x => new
            {
                GatheringPointId = x.RowId,
                Point = new DefaultGatheringPoint(new GatheringPointId((ushort)x.GatheringPointBase.Row),
                    x.GatheringPointBase.Value!.GatheringType.Row switch
                    {
                        0 or 1 => EClassJob.Miner,
                        2 or 3 => EClassJob.Botanist,
                        _ => EClassJob.Fisher
                    },
                    x.GatheringPointBase.Value.GatheringLevel,
                    x.GatheringPointBase.Value.Item.Where(y => y != 0).Select(y => (ushort)y).ToList(),
                    (EExpansionVersion?)x.TerritoryType.Value?.ExVersion.Row ?? (EExpansionVersion)byte.MaxValue,
                    (ushort)x.TerritoryType.Row,
                    x.TerritoryType.Value?.PlaceName.Value?.Name.ToString(),
                    $"{x.GatheringPointBase.Row} - {x.PlaceName.Value?.Name}")
            })
            .Where(x => x.Point.ClassJob != EClassJob.Fisher)
            .Select(x =>
            {
                if (gatheringPointToLeve.TryGetValue((int)x.GatheringPointId, out var leve))
                {
                    // it's a leve
                    return x.Point with
                    {
                        Expansion = EExpansionVersion.Shadowbringers,
                        TerritoryType = leve.TerritoryType,
                        TerritoryName = leve.TerritoryName,
                        PlaceName = leve.LeveName,
                    };
                }
                else if (x.Point.TerritoryType == 1 && _gatheringPointRegistry.TryGetGatheringPoint(x.Point.Id, out GatheringRoot? gatheringRoot))
                {
                    // for some reason the game doesn't know where this gathering location is
                    var territoryType = territoryTypeSheet.GetRow(gatheringRoot.Steps.Last().TerritoryId)!;
                    return x.Point with
                    {
                        Expansion = (EExpansionVersion)territoryType.ExVersion.Row,
                        TerritoryType = (ushort)territoryType.RowId,
                        TerritoryName = territoryType.PlaceName.Value?.Name.ToString(),
                    };
                }
                else
                    return x.Point;
            })
            .Where(x => x.Expansion != (EExpansionVersion)byte.MaxValue)
            .Where(x => x.GatheringItemIds.Count > 0)
            .GroupBy(x => x.Expansion)
            .Select(x => new ExpansionPoints(x.Key, x
                .GroupBy(y => new
                {
                    y.TerritoryType,
                    TerritoryName = $"{(!string.IsNullOrEmpty(y.TerritoryName) ? y.TerritoryName : "???")} ({y.TerritoryType})"
                })
                .Select(y => new TerritoryPoints(y.Key.TerritoryType, y.Key.TerritoryName, y.ToList()))
                .Where(y => y.Points.Count > 0)
                .ToList()))
            .OrderBy(x => x.Expansion)
            .ToList();

        gameInteropProvider.InitializeFromAttributes(this);
    }

    public void DrawGatheringItems()
    {
        using var tab = ImRaii.TabItem("Gathering Points");
        if (!tab)
            return;

        using var table = ImRaii.Table("GatheringPoints", 3, ImGuiTableFlags.NoSavedSettings);
        if (!table)
            return;

        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.NoHide);
        ImGui.TableSetupColumn("Supported", ImGuiTableColumnFlags.WidthFixed, 100 * ImGui.GetIO().FontGlobalScale);
        ImGui.TableSetupColumn("Collected", ImGuiTableColumnFlags.WidthFixed, 100 * ImGui.GetIO().FontGlobalScale);
        ImGui.TableHeadersRow();

        foreach (var expansion in _gatheringPoints)
            DrawExpansion(expansion);
    }

    private void DrawExpansion(ExpansionPoints expansion)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool open = ImGui.TreeNodeEx(expansion.Expansion.ToFriendlyString(), ImGuiTreeNodeFlags.SpanFullWidth);

        ImGui.TableNextColumn();
        DrawCount(expansion.CompletedPoints, expansion.TotalPoints);
        ImGui.TableNextColumn();
        DrawCount(expansion.CompletedItems, expansion.TotalItems);

        if (open)
        {
            foreach (var territory in expansion.PointsByTerritories)
                DrawTerritory(territory);

            ImGui.TreePop();
        }
    }

    private void DrawTerritory(TerritoryPoints territory)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool open = ImGui.TreeNodeEx(territory.ToFriendlyString(), ImGuiTreeNodeFlags.SpanFullWidth);

        ImGui.TableNextColumn();
        DrawCount(territory.CompletedPoints, territory.TotalPoints);
        ImGui.TableNextColumn();
        DrawCount(territory.CompletedItems, territory.TotalItems);

        if (open)
        {
            foreach (var point in territory.Points)
                DrawPoint(point);

            ImGui.TreePop();
        }
    }

    private void DrawPoint(DefaultGatheringPoint point)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool open = ImGui.TreeNodeEx($"{point.PlaceName} ({point.ClassJob} Lv. {point.Level})",
            ImGuiTreeNodeFlags.SpanFullWidth);

        ImGui.TableNextColumn();
        float spacing;
        // ReSharper disable once UnusedVariable
        using (var font = _pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
        {
            spacing = ImGui.GetColumnWidth() / 2 - ImGui.CalcTextSize(FontAwesomeIcon.Check.ToIconString()).X;
        }

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + spacing);
        _uiUtils.ChecklistItem(string.Empty, point.IsComplete);

        ImGui.TableNextColumn();
        DrawCount(point.CompletedItems, point.TotalItems);

        if (open)
        {
            foreach (var item in point.GatheringItemIds)
                DrawItem(item);

            ImGui.TreePop();
        }
    }

    private void DrawItem(ushort item)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TreeNodeEx(_gatheringItems.GetValueOrDefault(item, "???"),
            ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.SpanFullWidth);

        ImGui.TableNextColumn();

        ImGui.TableNextColumn();
        float spacing;
        // ReSharper disable once UnusedVariable
        using (var font = _pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
        {
            spacing = ImGui.GetColumnWidth() / 2 - ImGui.CalcTextSize(FontAwesomeIcon.Check.ToIconString()).X;
        }

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + spacing);
        if (item < 10_000)
            _uiUtils.ChecklistItem(string.Empty, _gatheredItems.Contains(item));
        else
            _uiUtils.ChecklistItem(string.Empty, ImGuiColors.DalamudGrey, FontAwesomeIcon.Minus);
    }

    private static void DrawCount(int count, int total)
    {
        string len = 999.ToString(CultureInfo.CurrentCulture);
        ImGui.PushFont(UiBuilder.MonoFont);

        string text =
            $"{count.ToString(CultureInfo.CurrentCulture).PadLeft(len.Length)} / {total.ToString(CultureInfo.CurrentCulture).PadLeft(len.Length)}";
        if (count == total)
            ImGui.TextColored(ImGuiColors.ParsedGreen, text);
        else
            ImGui.TextUnformatted(text);

        ImGui.PopFont();
    }

    internal void RefreshCounts()
    {
        _gatheredItems.Clear();
        foreach (ushort key in _gatheringItems.Keys)
        {
            if (IsGatheringItemGathered(key))
                _gatheredItems.Add(key);
        }

        foreach (var expansion in _gatheringPoints)
        {
            foreach (var territory in expansion.PointsByTerritories)
            {
                foreach (var point in territory.Points)
                {
                    point.TotalItems = point.GatheringItemIds.Count(x => x < 10_000);
                    point.CompletedItems = point.GatheringItemIds.Count(_gatheredItems.Contains);
                    point.IsComplete = _gatheringPointRegistry.TryGetGatheringPoint(point.Id, out _);
                }

                territory.TotalItems = territory.Points.Sum(x => x.TotalItems);
                territory.CompletedItems = territory.Points.Sum(x => x.CompletedItems);
                territory.CompletedPoints = territory.Points.Count(x => x.IsComplete);
            }

            expansion.TotalItems = expansion.PointsByTerritories.Sum(x => x.TotalItems);
            expansion.CompletedItems = expansion.PointsByTerritories.Sum(x => x.CompletedItems);
            expansion.TotalPoints = expansion.PointsByTerritories.Sum(x => x.TotalPoints);
            expansion.CompletedPoints = expansion.PointsByTerritories.Sum(x => x.CompletedPoints);
        }
    }

    private sealed record ExpansionPoints(EExpansionVersion Expansion, List<TerritoryPoints> PointsByTerritories)
    {
        public int TotalItems { get; set; }
        public int TotalPoints { get; set; }
        public int CompletedItems { get; set; }
        public int CompletedPoints { get; set; }
    }

    private sealed record TerritoryPoints(
        ushort TerritoryType,
        string TerritoryName,
        List<DefaultGatheringPoint> Points)
    {
        public int TotalItems { get; set; }
        public int TotalPoints => Points.Count;
        public int CompletedItems { get; set; }
        public int CompletedPoints { get; set; }
        public string ToFriendlyString() =>
            !string.IsNullOrEmpty(TerritoryName) ? TerritoryName : $"??? ({TerritoryType})";
    }

    private sealed record DefaultGatheringPoint(
        GatheringPointId Id,
        EClassJob ClassJob,
        byte Level,
        List<ushort> GatheringItemIds,
        EExpansionVersion Expansion,
        ushort TerritoryType,
        string? TerritoryName,
        string? PlaceName)
    {
        public int TotalItems { get; set; }
        public int CompletedItems { get; set; }
        public bool IsComplete { get; set; }
    }
}
