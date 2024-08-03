using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
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

    private readonly Dictionary<Guid, LocationOverride> _changes = [];

    private IGameObject? _target;
    private (RendererPlugin.GatheringLocationContext, GatheringLocation)? _targetLocation;
    private string _newFileName = string.Empty;

    public EditorWindow(RendererPlugin plugin, EditorCommands editorCommands, IDataManager dataManager,
        ITargetManager targetManager, IClientState clientState)
        : base("Gathering Path Editor###QuestionableGatheringPathEditor")
    {
        _plugin = plugin;
        _editorCommands = editorCommands;
        _dataManager = dataManager;
        _targetManager = targetManager;
        _clientState = clientState;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 300),
        };
    }

    public override void Update()
    {
        _target = _targetManager.Target;
        if (_target == null || _target.ObjectKind != ObjectKind.GatheringPoint)
        {
            _targetLocation = null;
            return;
        }

        var gatheringLocations = _plugin.GetLocationsInTerritory(_clientState.TerritoryType);
        var location = gatheringLocations.SelectMany(context =>
                context.Root.Groups.SelectMany(group =>
                    group.Nodes
                        .Where(node => node.DataId == _target.DataId)
                        .SelectMany(node => node.Locations)
                        .Where(location => Vector3.Distance(location.Position, _target.Position) < 0.1f)
                        .Select(location => new { Context = context, Location = location })))
            .FirstOrDefault();
        if (location == null)
        {
            _targetLocation = null;
            return;
        }

        _targetLocation = (location.Context, location.Location);
    }

    public override bool DrawConditions()
    {
        return _target != null || _targetLocation != null;
    }

    public override void Draw()
    {
        if (_target != null && _targetLocation != null)
        {
            var context = _targetLocation.Value.Item1;
            var location = _targetLocation.Value.Item2;
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
            if (ImGui.DragInt("Min Angle", ref minAngle, 5, -180, 360))
            {
                locationOverride.MinimumAngle = minAngle;
                locationOverride.MaximumAngle ??= location.MaximumAngle.GetValueOrDefault();
                _plugin.Redraw();
            }

            int maxAngle = locationOverride.MaximumAngle ?? location.MaximumAngle.GetValueOrDefault();
            if (ImGui.DragInt("Max Angle", ref maxAngle, 5, -180, 360))
            {
                locationOverride.MinimumAngle ??= location.MinimumAngle.GetValueOrDefault();
                locationOverride.MaximumAngle = maxAngle;
                _plugin.Redraw();
            }

            ImGui.BeginDisabled(locationOverride.MinimumAngle == null && locationOverride.MaximumAngle == null);
            if (ImGui.Button("Save"))
            {
                location.MinimumAngle = locationOverride.MinimumAngle;
                location.MaximumAngle = locationOverride.MaximumAngle;
                _plugin.Save(context.File, context.Root);
            }
            ImGui.SameLine();
            if (ImGui.Button("Reset"))
            {
                _changes[location.InternalId] = new LocationOverride();
                _plugin.Redraw();
            }
            ImGui.EndDisabled();

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
                ImGui.InputText("File Name", ref _newFileName, 128);
                ImGui.BeginDisabled(string.IsNullOrEmpty(_newFileName));
                if (ImGui.Button("Create location"))
                {
                    var (targetFile, root) = _editorCommands.CreateNewFile(gatheringPoint, _target, _newFileName);
                    _plugin.Save(targetFile, root);
                    _newFileName = string.Empty;
                }

                ImGui.EndDisabled();
            }
        }
    }

    public bool TryGetOverride(Guid internalId, out LocationOverride? locationOverride)
        => _changes.TryGetValue(internalId, out locationOverride);
}

internal class LocationOverride
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
