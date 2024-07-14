using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
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

    public bool IsLoaded
    {
        get
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
    }

    public bool Start()
    {
        try
        {
            _changeOperationMode.InvokeAction(StateCommandType.Manual);
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

    public void SetTarget(IGameObject gameObject)
    {
        var player = _clientState.LocalPlayer;
        if (player == null)
            return; // uh oh

        float hitboxOffset = player.HitboxRadius + gameObject.HitboxRadius;
        float actualDistance = Vector3.Distance(player.Position, gameObject.Position);
        float maxDistance = player.ClassJob.GameData?.Role is 3 or 4 ? 25f : 3f;
        if (actualDistance - hitboxOffset > maxDistance)
            _movementController.NavigateTo(EMovementType.Combat, null, [gameObject.Position], false, false,
                maxDistance + hitboxOffset - 0.25f, true);
    }

    public void Dispose() => Stop();

    enum StateCommandType : byte
    {
        Off,
        Auto,
        Manual,
    }
}
