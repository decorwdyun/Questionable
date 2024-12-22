using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Json.Schema;
using Microsoft.Extensions.Logging;
using Questionable.Model;
using System;
using System.IO;
using System.Numerics;

namespace Questionable.Controller.CombatModules;

internal sealed class BossModModule : ICombatModule, IDisposable
{
    private const string Name = "BossMod";
    private readonly ILogger<BossModModule> _logger;
    private readonly MovementController _movementController;
    private readonly IClientState _clientState;
    private readonly Configuration _configuration;
    private readonly ICallGateSubscriber<string, string?> _getPreset;
    private readonly ICallGateSubscriber<string, bool, bool> _createPreset;
    private readonly ICallGateSubscriber<string, bool> _setPreset;
    private readonly ICallGateSubscriber<bool> _clearPreset;

    private static Stream Preset => typeof(BossModModule).Assembly.GetManifestResourceStream("Questionable.Controller.CombatModules.BossModPreset")!;
    private DateTime _lastDistanceCheck = DateTime.MinValue;

    public BossModModule(
        ILogger<BossModModule> logger,
        MovementController movementController,
        IClientState clientState,
        IDalamudPluginInterface pluginInterface,
        Configuration configuration)
    {
        _logger = logger;
        _movementController = movementController;
        _clientState = clientState;
        _configuration = configuration;

        _getPreset = pluginInterface.GetIpcSubscriber<string, string?>($"{Name}.Presets.Get");
        _createPreset = pluginInterface.GetIpcSubscriber<string, bool, bool>($"{Name}.Presets.Create");
        _setPreset = pluginInterface.GetIpcSubscriber<string, bool>($"{Name}.Presets.SetActive");
        _clearPreset = pluginInterface.GetIpcSubscriber<bool>($"{Name}.Presets.ClearActive");
    }

    public bool CanHandleFight(CombatController.CombatData combatData)
    {
        if (_configuration.General.CombatModule != Configuration.ECombatModule.BossMod)
            return false;

        try
        {
            return _getPreset.HasFunction;
        }
        catch (IpcError)
        {
            return false;
        }
    }

    public bool Start(CombatController.CombatData combatData)
    {
        try
        {
            if (_getPreset.InvokeFunc("Questionable") == null)
            {
                using var reader = new StreamReader(Preset);
                _logger.LogInformation("Loading Questionable BossMod Preset: {LoadedState}", _createPreset.InvokeFunc(reader.ReadToEnd(), true));
            }
            _setPreset.InvokeFunc("Questionable");
            _lastDistanceCheck = DateTime.Now;
            return true;
        }
        catch (IpcError e)
        {
            _logger.LogWarning(e, "Could not start combat");
            return false;
        }
    }

    public bool Stop()
    {
        try
        {
            _clearPreset.InvokeFunc();
            return true;
        }
        catch (IpcError e)
        {
            _logger.LogWarning(e, "Could not turn off combat");
            return false;
        }
    }

    public void MoveToTarget(IGameObject gameObject)
    {
        var player = _clientState.LocalPlayer;
        if (player == null)
            return; // uh oh

        float hitboxOffset = player.HitboxRadius + gameObject.HitboxRadius;
        float actualDistance = Vector3.Distance(player.Position, gameObject.Position);
        float maxDistance = player.ClassJob.ValueNullable?.Role is 3 or 4 ? 20f : 2.9f;
        if (actualDistance - hitboxOffset >= maxDistance)
        {
            if (actualDistance - hitboxOffset <= 5)
            {
                _logger.LogInformation("Moving to {TargetName} ({DataId}) to attack", gameObject.Name,
                    gameObject.DataId);
                _movementController.NavigateTo(EMovementType.Combat, null, [gameObject.Position], false, false,
                    maxDistance + hitboxOffset - 0.25f, true);
            }
            else
            {
                _logger.LogInformation("Moving to {TargetName} ({DataId}) to attack (with navmesh)", gameObject.Name,
                    gameObject.DataId);
                _movementController.NavigateTo(EMovementType.Combat, null, gameObject.Position, false, false,
                    maxDistance + hitboxOffset - 0.25f, true);
            }
        }

        _lastDistanceCheck = DateTime.Now;
    }

    public void Update(IGameObject gameObject)
    {
        if (_movementController.IsPathfinding || _movementController.IsPathRunning)
            return;

        if (DateTime.Now > _lastDistanceCheck.AddSeconds(10))
        {
            MoveToTarget(gameObject);
            _lastDistanceCheck = DateTime.Now;
        }
    }

    public bool CanAttack(IBattleNpc target) => true;

    public void Dispose() => Stop();
}
