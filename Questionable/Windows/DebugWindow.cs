using System.Globalization;
using System.Numerics;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Questionable.Controller;

namespace Questionable.Windows;

internal sealed class DebugWindow : Window
{
    private readonly MovementController _movementController;
    private readonly GameFunctions _gameFunctions;
    private readonly IClientState _clientState;
    private readonly ITargetManager _targetManager;

    public DebugWindow(MovementController movementController, GameFunctions gameFunctions, IClientState clientState,
        ITargetManager targetManager)
        : base("Questionable", ImGuiWindowFlags.AlwaysAutoResize)
    {
        _movementController = movementController;
        _gameFunctions = gameFunctions;
        _clientState = clientState;
        _targetManager = targetManager;

        IsOpen = true;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(100, 0),
            MaximumSize = default
        };
    }

    public override unsafe void Draw()
    {
        if (!_clientState.IsLoggedIn || _clientState.LocalPlayer == null)
            return;

        ImGui.Text(
            $"Current TerritoryId: {_clientState.TerritoryType}, Flying: {(_gameFunctions.IsFlyingUnlocked(_clientState.TerritoryType) ? "Yes" : "No")}");

        var q = _gameFunctions.GetCurrentQuest();
        ImGui.Text($"Current Quest: {q.CurrentQuest} → {q.Sequence}");

        if (_targetManager.Target != null)
        {
            ImGui.Separator();
            ImGui.Text($"Target: {_targetManager.Target.Name}");

            ImGui.BeginDisabled(!_movementController.IsNavmeshReady);
            if (!_movementController.IsPathfinding)
            {
                if (ImGui.Button("Move to Target"))
                {
                    _movementController.NavigateTo(EMovementType.DebugWindow, _targetManager.Target.Position,
                        _gameFunctions.IsFlyingUnlocked(_clientState.TerritoryType));
                }
            }
            else
            {
                if (ImGui.Button("Cancel pathfinding"))
                    _movementController.ResetPathfinding();
            }

            ImGui.EndDisabled();

            ImGui.SameLine();
            if (ImGui.Button("Interact"))
            {
                TargetSystem.Instance()->InteractWithObject(
                    (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)_targetManager.Target.Address, false);
            }

            ImGui.SameLine();

            if (ImGui.Button($"Copy"))
            {
                ImGui.SetClipboardText($$"""
                                         "DataId": {{_targetManager.Target.DataId}},
                                         "Position": {
                                             "X": {{_targetManager.Target.Position.X.ToString(CultureInfo.InvariantCulture)}},
                                             "Y": {{_targetManager.Target.Position.Y.ToString(CultureInfo.InvariantCulture)}},
                                             "Z": {{_targetManager.Target.Position.Z.ToString(CultureInfo.InvariantCulture)}}
                                         },
                                         "TerritoryId": {{_clientState.TerritoryType}},
                                         "InteractionType": "Interact"
                                         """);
            }
        }
        else
        {
            if (ImGui.Button($"Copy"))
            {
                ImGui.SetClipboardText($$"""
                                         "Position": {
                                             "X": {{_clientState.LocalPlayer.Position.X.ToString(CultureInfo.InvariantCulture)}},
                                             "Y": {{_clientState.LocalPlayer.Position.Y.ToString(CultureInfo.InvariantCulture)}},
                                             "Z": {{_clientState.LocalPlayer.Position.Z.ToString(CultureInfo.InvariantCulture)}}
                                         },
                                         "TerritoryId": {{_clientState.TerritoryType}},
                                         "InteractionType": ""
                                         """);
            }
        }

        ImGui.Separator();

        var map = AgentMap.Instance();
        ImGui.BeginDisabled(map == null || map->IsFlagMarkerSet == 0 ||
                            map->FlagMapMarker.TerritoryId != _clientState.TerritoryType);
        if (ImGui.Button("Move to Flag"))
            _gameFunctions.ExecuteCommand(
                $"/vnav {(_gameFunctions.IsFlyingUnlocked(_clientState.TerritoryType) ? "flyflag" : "moveflag")}");
        ImGui.EndDisabled();

        ImGui.SameLine();

        ImGui.BeginDisabled(!_movementController.IsPathRunning);
        if (ImGui.Button("Stop Nav"))
            _movementController.Stop();
        ImGui.EndDisabled();
    }
}
