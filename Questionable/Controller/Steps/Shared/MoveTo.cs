using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using LLib;
using Lumina.Excel.GeneratedSheets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Common;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;
using Action = System.Action;
using Mount = Questionable.Controller.Steps.Common.Mount;
using Quest = Questionable.Model.Quest;

namespace Questionable.Controller.Steps.Shared;

internal static class MoveTo
{
    internal sealed class Factory(
        MovementController movementController,
        GameFunctions gameFunctions,
        ICondition condition,
        IDataManager dataManager,
        IClientState clientState,
        AetheryteData aetheryteData,
        TerritoryData territoryData,
        ILoggerFactory loggerFactory,
        Mount.Factory mountFactory,
        ILogger<Factory> logger) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.Position != null)
            {
                return CreateMountTasks(quest.Id, step, step.Position.Value);
            }
            else if (step is { DataId: not null, StopDistance: not null })
            {
                return [ExpectToBeNearDataId(step.DataId.Value, step.StopDistance.Value)];
            }
            else if (step is { InteractionType: EInteractionType.AttuneAetheryte, Aetheryte: not null })
            {
                return CreateMountTasks(quest.Id, step, aetheryteData.Locations[step.Aetheryte.Value]);
            }
            else if (step is { InteractionType: EInteractionType.AttuneAethernetShard, AethernetShard: not null })
            {
                return CreateMountTasks(quest.Id, step, aetheryteData.Locations[step.AethernetShard.Value]);
            }

            return [];
        }

        public ITask Move(QuestStep step, Vector3 destination)
        {
            return Move(new MoveParams(step, destination));
        }

        public ITask Move(MoveParams moveParams)
        {
            return new MoveInternal(moveParams, movementController, mountFactory, gameFunctions,
                loggerFactory.CreateLogger<MoveInternal>(), clientState, dataManager);
        }

        public ITask Land()
        {
            return new LandTask(clientState, condition, loggerFactory.CreateLogger<LandTask>());
        }

        public ITask ExpectToBeNearDataId(uint dataId, float stopDistance)
        {
            return new WaitForNearDataId(dataId, stopDistance, gameFunctions, clientState);
        }

        public IEnumerable<ITask> CreateMountTasks(ElementId questId, QuestStep step, Vector3 destination)
        {
            if (step.InteractionType == EInteractionType.Jump && step.JumpDestination != null &&
                (clientState.LocalPlayer!.Position - step.JumpDestination.Position).Length() <=
                (step.JumpDestination.StopDistance ?? 1f))
            {
                logger.LogInformation("We're at the jump destination, skipping movement");
                yield break;
            }

            yield return new WaitConditionTask(() => clientState.TerritoryType == step.TerritoryId,
                $"Wait(territory: {territoryData.GetNameAndId(step.TerritoryId)})");

            if (!step.DisableNavmesh)
            {
                yield return new WaitConditionTask(() => movementController.IsNavmeshReady,
                    "Wait(navmesh ready)");

                yield return Move(step, destination);
            }
            else
            {
                yield return Move(step, destination);
            }

            if (step is { Fly: true, Land: true })
                yield return Land();
        }
    }

    private sealed class MoveInternal : ITask, IToastAware
    {
        private readonly string _cannotExecuteAtThisTime;
        private readonly MovementController _movementController;
        private readonly Mount.Factory _mountFactory;
        private readonly GameFunctions _gameFunctions;
        private readonly ILogger<MoveInternal> _logger;
        private readonly IClientState _clientState;

        private readonly Action _startAction;
        private readonly Vector3 _destination;
        private readonly MoveParams _moveParams;
        private bool _canRestart;
        private ITask? _mountTask;

        public MoveInternal(MoveParams moveParams,
            MovementController movementController,
            Mount.Factory mountFactory,
            GameFunctions gameFunctions,
            ILogger<MoveInternal> logger,
            IClientState clientState,
            IDataManager dataManager)
        {
            _movementController = movementController;
            _mountFactory = mountFactory;
            _gameFunctions = gameFunctions;
            _logger = logger;
            _clientState = clientState;
            _cannotExecuteAtThisTime = dataManager.GetString<LogMessage>(579, x => x.Text)!;

            _destination = moveParams.Destination;

            if (!gameFunctions.IsFlyingUnlocked(moveParams.TerritoryId))
            {
                moveParams = moveParams with { Fly = false, Land = false };
            }

            if (!moveParams.DisableNavMesh)
            {
                _startAction = () =>
                    _movementController.NavigateTo(EMovementType.Quest, moveParams.DataId, _destination,
                        fly: moveParams.Fly,
                        sprint: moveParams.Sprint,
                        stopDistance: moveParams.StopDistance,
                        ignoreDistanceToObject: moveParams.IgnoreDistanceToObject,
                        land: moveParams.Land);
            }
            else
            {
                _startAction = () =>
                    _movementController.NavigateTo(EMovementType.Quest, moveParams.DataId, [_destination],
                        fly: moveParams.Fly,
                        sprint: moveParams.Sprint,
                        stopDistance: moveParams.StopDistance,
                        ignoreDistanceToObject: moveParams.IgnoreDistanceToObject,
                        land: moveParams.Land);
            }

            _moveParams = moveParams;
            _canRestart = moveParams.RestartNavigation;
        }

        public InteractionProgressContext? ProgressContext() => _mountTask?.ProgressContext();

        public bool ShouldRedoOnInterrupt() => true;

        public bool Start()
        {
            float stopDistance = _moveParams.StopDistance ?? QuestStep.DefaultStopDistance;
            Vector3? position = _clientState.LocalPlayer?.Position;
            float actualDistance = position == null ? float.MaxValue : Vector3.Distance(position.Value, _destination);

            if (_moveParams.Mount == true)
            {
                var mountTask = _mountFactory.Mount(_moveParams.TerritoryId, Mount.EMountIf.Always);
                if (mountTask.Start())
                {
                    _mountTask = mountTask;
                    return true;
                }
            }
            else if (_moveParams.Mount == false)
            {
                var mountTask = _mountFactory.Unmount();
                if (mountTask.Start())
                {
                    _mountTask = mountTask;
                    return true;
                }
            }

            if (!_moveParams.DisableNavMesh)
            {
                if (_moveParams.Mount == null)
                {
                    Mount.EMountIf mountIf =
                        actualDistance > stopDistance && _moveParams.Fly &&
                        _gameFunctions.IsFlyingUnlocked(_moveParams.TerritoryId)
                            ? Mount.EMountIf.Always
                            : Mount.EMountIf.AwayFromPosition;
                    var mountTask = _mountFactory.Mount(_moveParams.TerritoryId, mountIf, _destination);
                    if (mountTask.Start())
                    {
                        _mountTask = mountTask;
                        return true;
                    }
                }
            }

            _mountTask = new NoOpTask();
            return true;
        }

        public ETaskResult Update()
        {
            if (_mountTask != null)
            {
                if (_mountTask.Update() == ETaskResult.TaskComplete)
                {
                    _mountTask = null;

                    _logger.LogInformation("Moving to {Destination}", _destination.ToString("G", CultureInfo.InvariantCulture));
                    _startAction();
                }
                return ETaskResult.StillRunning;
            }

            if (_movementController.IsPathfinding || _movementController.IsPathRunning)
                return ETaskResult.StillRunning;

            DateTime movementStartedAt = _movementController.MovementStartedAt;
            if (movementStartedAt == DateTime.MaxValue || movementStartedAt.AddSeconds(2) >= DateTime.Now)
                return ETaskResult.StillRunning;

            if (_canRestart &&
                Vector3.Distance(_clientState.LocalPlayer!.Position, _destination) >
                (_moveParams.StopDistance ?? QuestStep.DefaultStopDistance) + 5f)
            {
                _canRestart = false;
                if (_clientState.TerritoryType == _moveParams.TerritoryId)
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

        public override string ToString() => $"MoveTo({_destination.ToString("G", CultureInfo.InvariantCulture)})";

        public bool OnErrorToast(SeString message)
        {
            if (GameFunctions.GameStringEquals(_cannotExecuteAtThisTime, message.TextValue))
                return true;

            return false;
        }
    }

    private sealed class NoOpTask : ITask
    {
        public bool Start() => true;

        public ETaskResult Update() => ETaskResult.TaskComplete;
    }

    internal sealed record MoveParams(
        ushort TerritoryId,
        Vector3 Destination,
        bool? Mount = null,
        float? StopDistance = null,
        uint? DataId = null,
        bool DisableNavMesh = false,
        bool Sprint = true,
        bool Fly = false,
        bool Land = false,
        bool IgnoreDistanceToObject = false,
        bool RestartNavigation = true)
    {
        public MoveParams(QuestStep step, Vector3 destination)
            : this(step.TerritoryId,
                destination,
                step.Mount,
                step.CalculateActualStopDistance(),
                step.DataId,
                step.DisableNavmesh,
                step.Sprint != false,
                step.Fly == true,
                step.Land == true,
                step.IgnoreDistanceToObject == true,
                step.RestartNavigationIfCancelled != false)
        {
        }
    }

    private sealed class WaitForNearDataId(
        uint dataId,
        float stopDistance,
        GameFunctions gameFunctions,
        IClientState clientState) : ITask
    {
        public bool ShouldRedoOnInterrupt() => true;

        public bool Start() => true;

        public ETaskResult Update()
        {
            IGameObject? gameObject = gameFunctions.FindObjectByDataId(dataId);
            if (gameObject == null ||
                (gameObject.Position - clientState.LocalPlayer!.Position).Length() > stopDistance)
            {
                throw new TaskException("Object not found or too far away, no position so we can't move");
            }

            return ETaskResult.TaskComplete;
        }
    }

    private sealed class LandTask(IClientState clientState, ICondition condition, ILogger<LandTask> logger) : ITask
    {
        private bool _landing;
        private DateTime _continueAt;

        public bool ShouldRedoOnInterrupt() => true;

        public bool Start()
        {
            if (!condition[ConditionFlag.InFlight])
            {
                logger.LogInformation("Not flying, not attempting to land");
                return false;
            }

            _landing = AttemptLanding();
            _continueAt = DateTime.Now.AddSeconds(0.25);
            return true;
        }

        public ETaskResult Update()
        {
            if (DateTime.Now < _continueAt)
                return ETaskResult.StillRunning;

            if (condition[ConditionFlag.InFlight])
            {
                if (!_landing)
                {
                    _landing = AttemptLanding();
                    _continueAt = DateTime.Now.AddSeconds(0.25);
                }

                return ETaskResult.StillRunning;
            }

            return ETaskResult.TaskComplete;
        }

        private unsafe bool AttemptLanding()
        {
            var character = (Character*)(clientState.LocalPlayer?.Address ?? 0);
            if (character != null)
            {
                if (ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 23) == 0)
                {
                    logger.LogInformation("Attempting to land");
                    return ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23);
                }
            }

            return false;
        }
    }
}
