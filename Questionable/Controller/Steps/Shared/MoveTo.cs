using System;
using System.Collections.Generic;
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
            return new MoveInternal(moveParams, movementController, gameFunctions,
                loggerFactory.CreateLogger<MoveInternal>(), condition, dataManager);
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
                yield return new WaitConditionTask(() => movementController.IsNavmeshReady,
                    "Wait(navmesh ready)");

            float stopDistance = step.CalculateActualStopDistance();
            Vector3? position = clientState.LocalPlayer?.Position;
            float actualDistance = position == null ? float.MaxValue : Vector3.Distance(position.Value, destination);

            // if we teleport to a different zone, assume we always need to move; this is primarily relevant for cases
            // where you're e.g. in Lakeland, and the step navigates via Crystarium → Tesselation back into the same
            // zone.
            //
            // Side effects of this check being broken include:
            //   - mounting when near the target npc (if you spawn close enough for the next step)
            //   - trying to fly when near the target npc (if close enough where no movement is required)
            if (step.AetheryteShortcut != null &&
                aetheryteData.TerritoryIds[step.AetheryteShortcut.Value] != step.TerritoryId)
            {
                logger.LogDebug("Aetheryte: Changing distance to max, previous distance: {Distance}", actualDistance);
                actualDistance = float.MaxValue;
            }

            if (step.Mount == true)
                yield return mountFactory.Mount(step.TerritoryId, Mount.EMountIf.Always);
            else if (step.Mount == false)
                yield return mountFactory.Unmount();

            if (!step.DisableNavmesh)
            {
                if (step.Mount == null)
                {
                    Mount.EMountIf mountIf =
                        actualDistance > stopDistance && step.Fly == true &&
                        gameFunctions.IsFlyingUnlocked(step.TerritoryId)
                            ? Mount.EMountIf.Always
                            : Mount.EMountIf.AwayFromPosition;
                    yield return mountFactory.Mount(step.TerritoryId, mountIf, destination);
                }

                if (actualDistance > stopDistance)
                {
                    yield return Move(step, destination);
                }
                else
                    logger.LogInformation("Skipping move task, distance: {ActualDistance} < {StopDistance}",
                        actualDistance, stopDistance);
            }
            else
            {
                // navmesh won't move close enough
                if (actualDistance > stopDistance)
                {
                    yield return Move(step, destination);
                }
                else
                    logger.LogInformation("Skipping move task, distance: {ActualDistance} < {StopDistance}",
                        actualDistance, stopDistance);
            }

            if (step.Fly == true && step.Land == true)
                yield return Land();
        }
    }

    private sealed class MoveInternal : ITask, IToastAware
    {
        private readonly string _cannotExecuteAtThisTime;
        private readonly MovementController _movementController;
        private readonly ILogger<MoveInternal> _logger;
        private readonly ICondition _condition;

        private readonly Action _startAction;
        private readonly Vector3 _destination;

        public MoveInternal(MoveParams moveParams,
            MovementController movementController,
            GameFunctions gameFunctions,
            ILogger<MoveInternal> logger,
            ICondition condition,
            IDataManager dataManager)
        {
            _movementController = movementController;
            _logger = logger;
            _condition = condition;
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
        }

        public bool Start()
        {
            _logger.LogInformation("Moving to {Destination}", _destination.ToString("G", CultureInfo.InvariantCulture));
            _startAction();
            return true;
        }

        public ETaskResult Update()
        {
            if (_movementController.IsPathfinding || _movementController.IsPathRunning)
                return ETaskResult.StillRunning;

            DateTime movementStartedAt = _movementController.MovementStartedAt;
            if (movementStartedAt == DateTime.MaxValue || movementStartedAt.AddSeconds(2) >= DateTime.Now)
                return ETaskResult.StillRunning;

            return ETaskResult.TaskComplete;
        }

        public override string ToString() => $"MoveTo({_destination.ToString("G", CultureInfo.InvariantCulture)})";

        public bool OnErrorToast(SeString message)
        {
            if (GameFunctions.GameStringEquals(_cannotExecuteAtThisTime, message.TextValue) &&
                _condition[ConditionFlag.Diving])
                return true;

            return false;
        }
    }

    internal sealed record MoveParams(
        ushort TerritoryId,
        Vector3 Destination,
        float? StopDistance = null,
        uint? DataId = null,
        bool DisableNavMesh = false,
        bool Sprint = true,
        bool Fly = false,
        bool Land = false,
        bool IgnoreDistanceToObject = false)
    {
        public MoveParams(QuestStep step, Vector3 destination)
            : this(step.TerritoryId,
                destination,
                step.CalculateActualStopDistance(),
                step.DataId,
                step.DisableNavmesh,
                step.Sprint != false,
                step.Fly == true,
                step.Land == true,
                step.IgnoreDistanceToObject == true)
        {
        }
    }

    private sealed class WaitForNearDataId(
        uint dataId,
        float stopDistance,
        GameFunctions gameFunctions,
        IClientState clientState) : ITask
    {
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
