using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.NavigationOverrides;
using Questionable.Controller.Steps.Common;
using Questionable.Data;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.Shared;

internal static class Move
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.Position != null)
            {
                var builder = serviceProvider.GetRequiredService<MoveBuilder>();
                builder.Step = step;
                builder.Destination = step.Position.Value;
                return builder.Build();
            }
            else if (step is { DataId: not null, StopDistance: not null })
            {
                var task = serviceProvider.GetRequiredService<ExpectToBeNearDataId>();
                task.DataId = step.DataId.Value;
                task.StopDistance = step.StopDistance.Value;
                return [task];
            }

            return [];
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
            => throw new InvalidOperationException();
    }

    internal sealed class MoveBuilder(
        IServiceProvider serviceProvider,
        ILogger<MoveBuilder> logger,
        GameFunctions gameFunctions,
        IClientState clientState,
        MovementController movementController,
        TerritoryData territoryData)
    {
        public QuestStep Step { get; set; } = null!;
        public Vector3 Destination { get; set; }

        public IEnumerable<ITask> Build()
        {
            if (Step.InteractionType == EInteractionType.Jump && Step.JumpDestination != null &&
                (clientState.LocalPlayer!.Position - Step.JumpDestination.Position).Length() <=
                (Step.JumpDestination.StopDistance ?? 1f))
            {
                logger.LogInformation("We're at the jump destination, skipping movement");
                yield break;
            }

            yield return new WaitConditionTask(() => clientState.TerritoryType == Step.TerritoryId,
                $"Wait(territory: {territoryData.GetNameAndId(Step.TerritoryId)})");

            if (!Step.DisableNavmesh)
                yield return new WaitConditionTask(() => movementController.IsNavmeshReady,
                    "Wait(navmesh ready)");

            float distance = Step.CalculateActualStopDistance();
            var position = clientState.LocalPlayer?.Position ?? new Vector3();
            float actualDistance = (position - Destination).Length();

            if (Step.Mount == true)
                yield return serviceProvider.GetRequiredService<MountTask>()
                    .With(Step.TerritoryId, MountTask.EMountIf.Always);
            else if (Step.Mount == false)
                yield return serviceProvider.GetRequiredService<UnmountTask>();

            if (!Step.DisableNavmesh)
            {
                if (Step.Mount == null)
                {
                    MountTask.EMountIf mountIf =
                        actualDistance > distance && Step.Fly == true &&
                        gameFunctions.IsFlyingUnlocked(Step.TerritoryId)
                            ? MountTask.EMountIf.Always
                            : MountTask.EMountIf.AwayFromPosition;
                    yield return serviceProvider.GetRequiredService<MountTask>()
                        .With(Step.TerritoryId, mountIf, Destination);
                }

                if (actualDistance > distance)
                {
                    yield return serviceProvider.GetRequiredService<MoveInternal>()
                        .With(Destination, m =>
                        {
                            m.NavigateTo(EMovementType.Quest, Step.DataId, Destination,
                                fly: Step.Fly == true && gameFunctions.IsFlyingUnlocked(Step.TerritoryId),
                                sprint: Step.Sprint != false,
                                stopDistance: distance,
                                ignoreDistanceToObject: Step.IgnoreDistanceToObject == true,
                                land: Step.Land == true);
                        });
                }
            }
            else
            {
                // navmesh won't move close enough
                if (actualDistance > distance)
                {
                    yield return serviceProvider.GetRequiredService<MoveInternal>()
                        .With(Destination, m =>
                        {
                            m.NavigateTo(EMovementType.Quest, Step.DataId, [Destination],
                                fly: Step.Fly == true && gameFunctions.IsFlyingUnlockedInCurrentZone(),
                                sprint: Step.Sprint != false,
                                stopDistance: distance,
                                land: Step.Land == true);
                        });
                }
            }

            if (Step.Fly == true && Step.Land == true)
                yield return serviceProvider.GetRequiredService<Land>();
        }
    }

    internal sealed class MoveInternal(MovementController movementController, ILogger<MoveInternal> logger) : ITask
    {
        public Action<MovementController> StartAction { get; set; } = null!;
        public Vector3 Destination { get; set; }

        public ITask With(Vector3 destination, Action<MovementController> startAction)
        {
            Destination = destination;
            StartAction = startAction;
            return this;
        }

        public bool Start()
        {
            logger.LogInformation("Moving to {Destination}", Destination.ToString("G", CultureInfo.InvariantCulture));
            StartAction(movementController);
            return true;
        }

        public ETaskResult Update()
        {
            if (movementController.IsPathfinding || movementController.IsPathRunning)
                return ETaskResult.StillRunning;

            DateTime movementStartedAt = movementController.MovementStartedAt;
            if (movementStartedAt == DateTime.MaxValue || movementStartedAt.AddSeconds(2) >= DateTime.Now)
                return ETaskResult.StillRunning;

            return ETaskResult.TaskComplete;
        }

        public override string ToString() => $"MoveTo({Destination.ToString("G", CultureInfo.InvariantCulture)})";
    }

    internal sealed class ExpectToBeNearDataId(GameFunctions gameFunctions, IClientState clientState) : ITask
    {
        public uint DataId { get; set; }
        public float StopDistance { get; set; }

        public bool Start() => true;

        public ETaskResult Update()
        {
            IGameObject? gameObject = gameFunctions.FindObjectByDataId(DataId);
            if (gameObject == null ||
                (gameObject.Position - clientState.LocalPlayer!.Position).Length() > StopDistance)
            {
                throw new TaskException("Object not found or too far away, no position so we can't move");
            }

            return ETaskResult.TaskComplete;
        }
    }

    internal sealed class Land(IClientState clientState, ICondition condition, ILogger<Land> logger) : ITask
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
                logger.LogInformation("Attempting to land");
                return ActionManager.Instance()->UseAction(ActionType.Mount, character->Mount.MountId);
            }

            return false;
        }
    }
}
