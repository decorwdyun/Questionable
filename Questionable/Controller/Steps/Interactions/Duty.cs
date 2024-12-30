using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Questionable.Controller.Steps.Shared;
using Questionable.Data;
using Questionable.External;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class Duty
{
    internal sealed class Factory(AutoDutyIpc autoDutyIpc) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Duty)
                yield break;

            ArgumentNullException.ThrowIfNull(step.ContentFinderConditionId);

            if (autoDutyIpc.IsConfiguredToRunContent(step.ContentFinderConditionId, step.AutoDutyEnabled))
            {
                yield return new StartAutoDutyTask(step.ContentFinderConditionId.Value);
                yield return new WaitAutoDutyTask(step.ContentFinderConditionId.Value);
                yield return new WaitAtEnd.WaitNextStepOrSequence();
            }
            else
            {
                yield return new OpenDutyFinderTask(step.ContentFinderConditionId.Value);
            }
        }
    }

    internal sealed record StartAutoDutyTask(uint ContentFinderConditionId) : ITask
    {
        public override string ToString() => $"StartAutoDuty({ContentFinderConditionId})";
    }

    internal sealed class StartAutoDutyExecutor(
        AutoDutyIpc autoDutyIpc,
        TerritoryData territoryData,
        IClientState clientState) : TaskExecutor<StartAutoDutyTask>
    {
        protected override bool Start()
        {
            autoDutyIpc.StartInstance(Task.ContentFinderConditionId);
            return true;
        }

        public override ETaskResult Update()
        {
            if (!territoryData.TryGetTerritoryIdForContentFinderCondition(Task.ContentFinderConditionId,
                    out uint territoryId))
                throw new TaskException("Failed to get territory ID for content finder condition");

            return clientState.TerritoryType == territoryId ? ETaskResult.TaskComplete : ETaskResult.StillRunning;
        }
    }

    internal sealed record WaitAutoDutyTask(uint ContentFinderConditionId) : ITask
    {
        public override string ToString() => $"Wait(AutoDuty, left instance {ContentFinderConditionId})";
    }

    internal sealed class WaitAutoDutyExecutor(
        AutoDutyIpc autoDutyIpc,
        TerritoryData territoryData,
        IClientState clientState) : TaskExecutor<WaitAutoDutyTask>
    {
        protected override bool Start() => true;

        public override ETaskResult Update()
        {
            if (!territoryData.TryGetTerritoryIdForContentFinderCondition(Task.ContentFinderConditionId,
                    out uint territoryId))
                throw new TaskException("Failed to get territory ID for content finder condition");

            return clientState.TerritoryType != territoryId && autoDutyIpc.IsStopped()
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;
        }
    }

    internal sealed record OpenDutyFinderTask(uint ContentFinderConditionId) : ITask
    {
        public override string ToString() => $"OpenDutyFinder({ContentFinderConditionId})";
    }

    internal sealed class OpenDutyFinderExecutor(
        GameFunctions gameFunctions,
        ICondition condition) : TaskExecutor<OpenDutyFinderTask>
    {
        protected override bool Start()
        {
            if (condition[ConditionFlag.InDutyQueue])
                return false;

            gameFunctions.OpenDutyFinder(Task.ContentFinderConditionId);
            return true;
        }

        public override ETaskResult Update() => ETaskResult.TaskComplete;
    }
}
