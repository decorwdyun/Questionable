using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using LLib;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Logging;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;
using Action = System.Action;
using Mount = Questionable.Controller.Steps.Common.Mount;

namespace Questionable.Controller.Steps.Movement;

internal sealed class MoveExecutor : TaskExecutor<MoveTask>, IToastAware
{
    private readonly string _cannotExecuteAtThisTime;
    private readonly MovementController _movementController;
    private readonly GameFunctions _gameFunctions;
    private readonly ILogger<MoveExecutor> _logger;
    private readonly IClientState _clientState;
    private readonly ICondition _condition;
    private readonly Mount.MountExecutor _mountExecutor;
    private readonly Mount.UnmountExecutor _unmountExecutor;

    private Action? _startAction;
    private Vector3 _destination;
    private bool _canRestart;

    private (ITaskExecutor Executor, ITask Task, bool Triggered)? _nestedExecutor =
        (new NoOpTaskExecutor(), new NoOpTask(), true);

    public MoveExecutor(
        MovementController movementController,
        GameFunctions gameFunctions,
        ILogger<MoveExecutor> logger,
        IClientState clientState,
        ICondition condition,
        IDataManager dataManager,
        Mount.MountExecutor mountExecutor,
        Mount.UnmountExecutor unmountExecutor)
    {
        _movementController = movementController;
        _gameFunctions = gameFunctions;
        _logger = logger;
        _clientState = clientState;
        _condition = condition;
        _mountExecutor = mountExecutor;
        _unmountExecutor = unmountExecutor;
        _cannotExecuteAtThisTime = dataManager.GetString<LogMessage>(579, x => x.Text)!;
    }

    private void PrepareMovementIfNeeded()
    {
        if (!_gameFunctions.IsFlyingUnlocked(Task.TerritoryId))
        {
            Task = Task with { Fly = false, Land = false };
        }

        if (!Task.DisableNavmesh)
        {
            _startAction = () =>
                _movementController.NavigateTo(EMovementType.Quest, Task.DataId, _destination,
                    fly: Task.Fly,
                    sprint: Task.Sprint != false,
                    stopDistance: Task.StopDistance,
                    ignoreDistanceToObject: Task.IgnoreDistanceToObject,
                    land: Task.Land);
        }
        else
        {
            _startAction = () =>
                _movementController.NavigateTo(EMovementType.Quest, Task.DataId, [_destination],
                    fly: Task.Fly,
                    sprint: Task.Sprint != false,
                    stopDistance: Task.StopDistance,
                    ignoreDistanceToObject: Task.IgnoreDistanceToObject,
                    land: Task.Land);
        }
    }

    protected override bool Start()
    {
        _canRestart = Task.RestartNavigation;
        _destination = Task.Destination;


        float stopDistance = Task.StopDistance ?? QuestStep.DefaultStopDistance;
        Vector3? position = _clientState.LocalPlayer?.Position;
        float actualDistance = position == null ? float.MaxValue : Vector3.Distance(position.Value, _destination);
        bool requiresMovement = actualDistance > stopDistance;
        if (requiresMovement)
            PrepareMovementIfNeeded();

        // might be able to make this optional
        if (Task.Mount == true)
        {
            var mountTask = new Mount.MountTask(Task.TerritoryId, Mount.EMountIf.Always);
            if (_mountExecutor.Start(mountTask))
            {
                _nestedExecutor = (_mountExecutor, mountTask, true);
                return true;
            }
            else if (_mountExecutor.EvaluateMountState() == Mount.MountResult.WhenOutOfCombat)
                _nestedExecutor = (_mountExecutor, mountTask, false);
        }
        else if (Task.Mount == false)
        {
            var mountTask = new Mount.UnmountTask();
            if (_unmountExecutor.Start(mountTask))
            {
                _nestedExecutor = (_unmountExecutor, mountTask, true);
                return true;
            }
        }

        if (!Task.DisableNavmesh)
        {
            if (Task.Mount == null)
            {
                Mount.EMountIf mountIf =
                    actualDistance > stopDistance && Task.Fly &&
                    _gameFunctions.IsFlyingUnlocked(Task.TerritoryId)
                        ? Mount.EMountIf.Always
                        : Mount.EMountIf.AwayFromPosition;
                var mountTask = new Mount.MountTask(Task.TerritoryId, mountIf, _destination);
                if (_mountExecutor.Start(mountTask))
                {
                    _nestedExecutor = (_mountExecutor, mountTask, true);
                    return true;
                }
                else if (_mountExecutor.EvaluateMountState() == Mount.MountResult.WhenOutOfCombat)
                    _nestedExecutor = (_mountExecutor, mountTask, false);
            }
        }

        if (_startAction != null && (_nestedExecutor == null || _nestedExecutor.Value.Triggered == false))
            _startAction();
        return true;
    }

    public override ETaskResult Update()
    {
        if (_nestedExecutor is { } nestedExecutor)
        {
            if (nestedExecutor is { Triggered: false, Executor: Mount.MountExecutor mountExecutor })
            {
                if (!_condition[ConditionFlag.InCombat])
                {
                    if (mountExecutor.EvaluateMountState() == Mount.MountResult.DontMount)
                        _nestedExecutor = (new NoOpTaskExecutor(), new NoOpTask(), true);
                    else
                    {
                        if (_movementController.IsPathfinding || _movementController.IsPathRunning)
                            _movementController.Stop();

                        if (nestedExecutor.Executor.Start(nestedExecutor.Task))
                        {
                            _nestedExecutor = nestedExecutor with { Triggered = true };
                            return ETaskResult.StillRunning;
                        }
                    }
                }
                else if (!ShouldResolveCombatBeforeNextInteraction() &&
                         _movementController is { IsPathfinding: false, IsPathRunning: false } &&
                         mountExecutor.EvaluateMountState() == Mount.MountResult.DontMount)
                {
                    // except for e.g. jumping which would maybe break if combat navigates us away, if we don't
                    // need a mount anymore we can just skip combat and assume that the interruption is handled
                    // later.
                    //
                    // without this, the character would just stand around while getting hit
                    _nestedExecutor = (new NoOpTaskExecutor(), new NoOpTask(), true);
                }
            }
            else if (nestedExecutor.Executor.Update() == ETaskResult.TaskComplete)
            {
                _nestedExecutor = null;
                if (_startAction != null)
                {
                    _logger.LogInformation("Moving to {Destination}",
                        _destination.ToString("G", CultureInfo.InvariantCulture));
                    _startAction();
                }
                else
                    return ETaskResult.TaskComplete;
            }
            else if (!_condition[ConditionFlag.Mounted] && _condition[ConditionFlag.InCombat] &&
                     nestedExecutor is { Triggered: true, Executor: Mount.MountExecutor })
            {
                // if the problem wasn't caused by combat, the normal mount retry should handle it
                _logger.LogDebug("Resetting mount trigger state");
                _nestedExecutor = nestedExecutor with { Triggered = false };

                // however, we're also explicitly ignoring combat here and walking away
                _startAction?.Invoke();
            }

            return ETaskResult.StillRunning;
        }

        if (_startAction == null)
            return ETaskResult.TaskComplete;

        if (_movementController.IsPathfinding || _movementController.IsPathRunning)
            return ETaskResult.StillRunning;

        DateTime movementStartedAt = _movementController.MovementStartedAt;
        if (movementStartedAt == DateTime.MaxValue || movementStartedAt.AddSeconds(2) >= DateTime.Now)
            return ETaskResult.StillRunning;

        if (_canRestart &&
            Vector3.Distance(_clientState.LocalPlayer!.Position, _destination) >
            (Task.StopDistance ?? QuestStep.DefaultStopDistance) + 5f)
        {
            _canRestart = false;
            if (_clientState.TerritoryType == Task.TerritoryId)
            {
                _logger.LogInformation("Looks like movement was interrupted, re-attempting to move");
                _startAction();
                return ETaskResult.StillRunning;
            }
            else
                _logger.LogInformation(
                    "Looks like movement was interrupted, do nothing since we're in a different territory now");
        }

        return ETaskResult.TaskComplete;
    }

    public override bool WasInterrupted()
    {
        if (Task.Fly && _condition[ConditionFlag.InCombat] && !_condition[ConditionFlag.Mounted] &&
            _nestedExecutor is { Triggered: false, Executor: Mount.MountExecutor mountExecutor } &&
            mountExecutor.EvaluateMountState() == Mount.MountResult.WhenOutOfCombat)
        {
            return true;
        }

        return base.WasInterrupted();
    }

    public override bool ShouldInterruptOnDamage()
    {
        // have we stopped moving, and are we
        // (a) waiting for a mount to complete, or
        // (b) want combat to be done before any other interaction?
        return _movementController is { IsPathfinding: false, IsPathRunning: false } &&
               (_nestedExecutor is { Triggered: false, Executor: Mount.MountExecutor } || ShouldResolveCombatBeforeNextInteraction());
    }

    private bool ShouldResolveCombatBeforeNextInteraction() => Task.InteractionType is EInteractionType.Jump;

    public bool OnErrorToast(SeString message)
    {
        if (GameFunctions.GameStringEquals(_cannotExecuteAtThisTime, message.TextValue))
            return true;

        return false;
    }
}
