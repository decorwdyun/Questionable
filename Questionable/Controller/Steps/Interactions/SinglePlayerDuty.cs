using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Questionable.Controller.Steps.Shared;
using Questionable.Data;
using Questionable.External;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class SinglePlayerDuty
{
    internal sealed class Factory : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.SinglePlayerDuty)
                yield break;

            if (step.BossModEnabled)
            {
                ArgumentNullException.ThrowIfNull(step.ContentFinderConditionId);

                yield return new StartSinglePlayerDuty(step.ContentFinderConditionId.Value);
                yield return new EnableAi();
                yield return new WaitSinglePlayerDuty(step.ContentFinderConditionId.Value);
                yield return new DisableAi();
                yield return new WaitAtEnd.WaitNextStepOrSequence();
            }
        }
    }

    internal sealed record StartSinglePlayerDuty(uint ContentFinderConditionId) : ITask
    {
        public override string ToString() => $"Wait(BossMod, entered instance {ContentFinderConditionId})";
    }

    internal sealed class StartSinglePlayerDutyExecutor(
        TerritoryData territoryData,
        IClientState clientState) : TaskExecutor<StartSinglePlayerDuty>
    {
        protected override bool Start() => true;

        public override ETaskResult Update()
        {
            if (!territoryData.TryGetContentFinderCondition(Task.ContentFinderConditionId,
                    out var cfcData))
                throw new TaskException("Failed to get territory ID for content finder condition");

            return clientState.TerritoryType == cfcData.TerritoryId
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;
        }

        public override bool ShouldInterruptOnDamage() => false;
    }

    internal sealed record EnableAi : ITask
    {
        public override string ToString() => "BossMod.EnableAi";
    }

    internal sealed class EnableAiExecutor(
        BossModIpc bossModIpc) : TaskExecutor<EnableAi>
    {
        protected override bool Start()
        {
            bossModIpc.EnableAi();
            return true;
        }

        public override ETaskResult Update() => ETaskResult.TaskComplete;

        public override bool ShouldInterruptOnDamage() => false;
    }

    internal sealed record WaitSinglePlayerDuty(uint ContentFinderConditionId) : ITask
    {
        public override string ToString() => $"Wait(BossMod, left instance {ContentFinderConditionId})";
    }

    internal sealed class WaitSinglePlayerDutyExecutor(
        TerritoryData territoryData,
        IClientState clientState,
        BossModIpc bossModIpc) : TaskExecutor<WaitSinglePlayerDuty>, IStoppableTaskExecutor
    {
        protected override bool Start() => true;

        public override ETaskResult Update()
        {
            if (!territoryData.TryGetContentFinderCondition(Task.ContentFinderConditionId,
                 out var cfcData))
            throw new TaskException("Failed to get territory ID for content finder condition");

            return clientState.TerritoryType != cfcData.TerritoryId
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;
        }

        public void StopNow() => bossModIpc.DisableAi();

        public override bool ShouldInterruptOnDamage() => false;
    }

    internal sealed record DisableAi : ITask
    {
        public override string ToString() => "BossMod.DisableAi";
    }

    internal sealed class DisableAiExecutor(
        BossModIpc bossModIpc) : TaskExecutor<DisableAi>
    {
        protected override bool Start()
        {
            bossModIpc.DisableAi();
            return true;
        }

        public override ETaskResult Update() => ETaskResult.TaskComplete;

        public override bool ShouldInterruptOnDamage() => false;
    }
}
