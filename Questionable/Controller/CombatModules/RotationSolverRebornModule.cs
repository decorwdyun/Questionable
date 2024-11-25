using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Questionable.Model;

namespace Questionable.Controller.CombatModules;

internal sealed class RotationSolverRebornModule : ICombatModule, IDisposable
{
    private readonly ILogger<RotationSolverRebornModule> _logger;
    private readonly MovementController _movementController;
    private readonly IClientState _clientState;
    private readonly ICallGateSubscriber<string, object> _test;
    private readonly ICallGateSubscriber<StateCommandType, object> _changeOperationMode;

    private DateTime _lastDistanceCheck = DateTime.MinValue;

    public RotationSolverRebornModule(ILogger<RotationSolverRebornModule> logger, MovementController movementController,
        IClientState clientState, IDalamudPluginInterface pluginInterface)
    {
        _logger = logger;
        _movementController = movementController;
        _clientState = clientState;
        _test = pluginInterface.GetIpcSubscriber<string, object>("RotationSolverReborn.Test");
        _changeOperationMode =
            pluginInterface.GetIpcSubscriber<StateCommandType, object>("RotationSolverReborn.ChangeOperatingMode");
    }

    public bool CanHandleFight(CombatController.CombatData combatData)
    {
        try
        {
            _test.InvokeAction("Validate RSR is callable from Questionable");
            return true;
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
            _changeOperationMode.InvokeAction(StateCommandType.Manual);
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
            _changeOperationMode.InvokeAction(StateCommandType.Off);
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

    [PublicAPI]
    enum StateCommandType : byte
    {
        Off,
        Auto,
        Manual,
    }
}
