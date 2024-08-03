using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Questionable.Model.Gathering;

namespace GatheringPathRenderer.Windows;

internal sealed class EditorWindow : Window
{
    private readonly RendererPlugin _plugin;
    private readonly EditorCommands _editorCommands;
    private readonly IDataManager _dataManager;
    private readonly ITargetManager _targetManager;
    private readonly IClientState _clientState;
    private readonly IObjectTable _objectTable;

    private readonly Dictionary<Guid, LocationOverride> _changes = [];

    private IGameObject? _target;

    private (RendererPlugin.GatheringLocationContext Context, GatheringNode Node, GatheringLocation Location)?
        _targetLocation;

    public EditorWindow(RendererPlugin plugin, EditorCommands editorCommands, IDataManager dataManager,
        ITargetManager targetManager, IClientState clientState, IObjectTable objectTable)
        : base("Gathering Path Editor###QuestionableGatheringPathEditor")
    {
        _plugin = plugin;
        _editorCommands = editorCommands;
        _dataManager = dataManager;
        _targetManager = targetManager;
        _clientState = clientState;
        _objectTable = objectTable;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 300),
        };
        ShowCloseButton = false;
    }

    public override void Update()
    {
        _target = _targetManager.Target;
        var gatheringLocations = _plugin.GetLocationsInTerritory(_clientState.TerritoryType);
        var location = gatheringLocations.SelectMany(context =>
                context.Root.Groups.SelectMany(group =>
                    group.Nodes
                        .SelectMany(node => node.Locations
                            .Where(location =>
                            {
                                if (_target != null)
                                    return Vector3.Distance(location.Position, _target.Position) < 0.1f;
                                else
                                    return Vector3.Distance(location.Position, _clientState.LocalPlayer!.Position) < 3f;
                            })
                            .Select(location => new { Context = context, Node = node, Location = location }))))
            .FirstOrDefault();
        if (_target != null && _target.ObjectKind != ObjectKind.GatheringPoint)
        {
            _target = null;
            _targetLocation = null;
            return;
        }

        if (location == null)
        {
            _targetLocation = null;
            return;
        }

        _target ??= _objectTable.FirstOrDefault(
            x => x.ObjectKind == ObjectKind.GatheringPoint &&
                 x.DataId == location.Node.DataId &&
                 Vector3.Distance(location.Location.Position, _clientState.LocalPlayer!.Position) < 3f);
        _targetLocation = (location.Context, location.Node, location.Location);
    }

    public override bool DrawConditions()
    {
        return _target != null || _targetLocation != null;
    }

    public override void Draw()
    {
        if (_target != null && _targetLocation != null)
        {
            var context = _targetLocation.Value.Context;
            var node = _targetLocation.Value.Node;
            var location = _targetLocation.Value.Location;
            ImGui.Text(context.File.Directory?.Name ?? string.Empty);
            ImGui.Indent();
            ImGui.Text(context.File.Name);
            ImGui.Unindent();
            ImGui.Text($"{_target.DataId} // {location.InternalId}");
            ImGui.Text(string.Create(CultureInfo.InvariantCulture, $"{location.Position:G}"));

            if (!_changes.TryGetValue(location.InternalId, out LocationOverride? locationOverride))
            {
                locationOverride = new LocationOverride();
                _changes[location.InternalId] = locationOverride;
            }

            int minAngle = locationOverride.MinimumAngle ?? location.MinimumAngle.GetValueOrDefault();
            if (ImGui.DragInt("Min Angle", ref minAngle, 5, -360, 360))
            {
                locationOverride.MinimumAngle = minAngle;
                locationOverride.MaximumAngle ??= location.MaximumAngle.GetValueOrDefault();
                _plugin.Redraw();
            }

            int maxAngle = locationOverride.MaximumAngle ?? location.MaximumAngle.GetValueOrDefault();
            if (ImGui.DragInt("Max Angle", ref maxAngle, 5, -360, 360))
            {
                locationOverride.MinimumAngle ??= location.MinimumAngle.GetValueOrDefault();
                locationOverride.MaximumAngle = maxAngle;
                _plugin.Redraw();
            }

            bool unsaved = locationOverride is { MinimumAngle: not null, MaximumAngle: not null };
            ImGui.BeginDisabled(!unsaved);
            if (unsaved)
                ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
            if (ImGui.Button("Save"))
            {
                location.MinimumAngle = locationOverride.MinimumAngle;
                location.MaximumAngle = locationOverride.MaximumAngle;
                _plugin.Save(context.File, context.Root);
            }
            if (unsaved)
                ImGui.PopStyleColor();

            ImGui.SameLine();
            if (ImGui.Button("Reset"))
            {
                _changes[location.InternalId] = new LocationOverride();
                _plugin.Redraw();
            }

            ImGui.EndDisabled();


            List<IGameObject> nodesInObjectTable = _objectTable
                .Where(x => x.ObjectKind == ObjectKind.GatheringPoint && x.DataId == _target.DataId)
                .ToList();
            List<IGameObject> missingLocations = nodesInObjectTable
                .Where(x => !node.Locations.Any(y => Vector3.Distance(x.Position, y.Position) < 0.1f))
                .ToList();
            if (missingLocations.Count > 0)
            {
                if (ImGui.Button("Add missing locations"))
                {
                    foreach (var missing in missingLocations)
                        _editorCommands.AddToExistingGroup(context.Root, missing);

                    _plugin.Save(context.File, context.Root);
                }
            }
        }
        else if (_target != null)
        {
            var gatheringPoint = _dataManager.GetExcelSheet<GatheringPoint>()!.GetRow(_target.DataId);
            if (gatheringPoint == null)
                return;

            var locationsInTerritory = _plugin.GetLocationsInTerritory(_clientState.TerritoryType).ToList();
            var location = locationsInTerritory.SingleOrDefault(x => x.Id == gatheringPoint.GatheringPointBase.Row);
            if (location != null)
            {
                var targetFile = location.File;
                var root = location.Root;

                if (ImGui.Button("Add to closest group"))
                {
                    _editorCommands.AddToExistingGroup(root, _target);
                    _plugin.Save(targetFile, root);
                }

                ImGui.BeginDisabled(root.Groups.Any(group => group.Nodes.Any(node => node.DataId == _target.DataId)));
                ImGui.SameLine();
                if (ImGui.Button("Add as new group"))
                {
                    _editorCommands.AddToNewGroup(root, _target);
                    _plugin.Save(targetFile, root);
                }

                ImGui.EndDisabled();
            }
            else
            {
                if (ImGui.Button("Create location"))
                {
                    var (targetFile, root) = _editorCommands.CreateNewFile(gatheringPoint, _target);
                    _plugin.Save(targetFile, root);
                }
            }
        }
    }

    public bool TryGetOverride(Guid internalId, out LocationOverride? locationOverride)
        => _changes.TryGetValue(internalId, out locationOverride);
}

internal sealed class LocationOverride
{
    public int? MinimumAngle { get; set; }
    public int? MaximumAngle { get; set; }
    public float? MinimumDistance { get; set; }
    public float? MaximumDistance { get; set; }

    public bool IsCone()
    {
        return MinimumAngle != null && MaximumAngle != null && MinimumAngle != MaximumAngle;
    }
}
