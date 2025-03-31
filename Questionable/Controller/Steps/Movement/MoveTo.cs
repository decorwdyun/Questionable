using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Common;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Movement;

internal static class MoveTo
{
    internal sealed class Factory(
        IClientState clientState,
        AetheryteData aetheryteData,
        TerritoryData territoryData,
        ILogger<Factory> logger) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.Position != null)
            {
                return CreateMoveTasks(step, step.Position.Value);
            }
            else if (step is { DataId: not null, StopDistance: not null })
            {
                return [new WaitForNearDataId(step.DataId.Value, step.StopDistance.Value)];
            }
            else if (step is { InteractionType: EInteractionType.AttuneAetheryte, Aetheryte: not null })
            {
                return CreateMoveTasks(step, aetheryteData.Locations[step.Aetheryte.Value]);
            }
            else if (step is { InteractionType: EInteractionType.AttuneAethernetShard, AethernetShard: not null })
            {
                return CreateMoveTasks(step, aetheryteData.Locations[step.AethernetShard.Value]);
            }

            return [];
        }

        private IEnumerable<ITask> CreateMoveTasks(QuestStep step, Vector3 destination)
        {
            if (step.InteractionType == EInteractionType.Jump && step.JumpDestination != null &&
                (clientState.LocalPlayer!.Position - step.JumpDestination.Position).Length() <=
                (step.JumpDestination.StopDistance ?? 1f))
            {
                logger.LogInformation("We're at the jump destination, skipping movement");
                yield break;
            }

            yield return new WaitCondition.Task(() => clientState.TerritoryType == step.TerritoryId,
                $"Wait(territory: {territoryData.GetNameAndId(step.TerritoryId)})");

            if (!step.DisableNavmesh)
                yield return new WaitNavmesh.Task();

            yield return new MoveTask(step, destination);

            if (step is { Fly: true, Land: true })
                yield return new LandTask();
        }
    }

    internal sealed record WaitForNearDataId(uint DataId, float StopDistance) : ITask
    {
        public bool ShouldRedoOnInterrupt() => true;
    }

    internal sealed class WaitForNearDataIdExecutor(
        GameFunctions gameFunctions,
        IClientState clientState) : TaskExecutor<WaitForNearDataId>
    {
        protected override bool Start() => true;

        public override ETaskResult Update()
        {
            IGameObject? gameObject = gameFunctions.FindObjectByDataId(Task.DataId);
            if (gameObject == null ||
                (gameObject.Position - clientState.LocalPlayer!.Position).Length() > Task.StopDistance)
            {
                throw new TaskException("Object not found or too far away, no position so we can't move");
            }

            return ETaskResult.TaskComplete;
        }

        public override bool ShouldInterruptOnDamage() => false;
    }

    internal sealed class LandTask : ITask
    {
        public bool ShouldRedoOnInterrupt() => true;
        public override string ToString() => "Land";
    }

    internal sealed class LandExecutor(IClientState clientState, ICondition condition, ILogger<LandExecutor> logger)
        : TaskExecutor<LandTask>
    {
        private bool _landing;
        private DateTime _continueAt;

        protected override bool Start()
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

        public override ETaskResult Update()
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

        public override bool ShouldInterruptOnDamage() => false;
    }
}
