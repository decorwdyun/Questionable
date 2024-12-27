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
    private readonly IClientState _clientState;
    private readonly Configuration _configuration;
    private readonly ICallGateSubscriber<string, object> _test;
    private readonly ICallGateSubscriber<StateCommandType, object> _changeOperationMode;

    public RotationSolverRebornModule(ILogger<RotationSolverRebornModule> logger, MovementController movementController,
        IClientState clientState, IDalamudPluginInterface pluginInterface, Configuration configuration)
    {
        _logger = logger;
        _clientState = clientState;
        _configuration = configuration;
        _test = pluginInterface.GetIpcSubscriber<string, object>("RotationSolverReborn.Test");
        _changeOperationMode =
            pluginInterface.GetIpcSubscriber<StateCommandType, object>("RotationSolverReborn.ChangeOperatingMode");
    }

    public bool CanHandleFight(CombatController.CombatData combatData)
    {
        if (_configuration.General.CombatModule != Configuration.ECombatModule.RotationSolverReborn)
            return false;

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
        if (!_changeOperationMode.HasAction)
            return true;

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

    public void Update(IGameObject gameObject)
    {
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
