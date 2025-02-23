using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Questionable.Controller.Steps.Common;
using Questionable.Controller.Steps.Shared;
using Questionable.Data;
using Questionable.External;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class SinglePlayerDuty
{
    public const int LahabreaTerritoryId = 1052;

    internal sealed class Factory(
        BossModIpc bossModIpc,
        TerritoryData territoryData,
        ICondition condition,
        IClientState clientState) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.SinglePlayerDuty)
                yield break;

            if (bossModIpc.IsConfiguredToRunSoloInstance(quest.Id, step.SinglePlayerDutyOptions))
            {
                if (!territoryData.TryGetContentFinderConditionForSoloInstance(quest.Id, step.SinglePlayerDutyIndex, out var cfcData))
                    throw new TaskException("Failed to get content finder condition for solo instance");

                yield return new StartSinglePlayerDuty(cfcData.ContentFinderConditionId);
                yield return new EnableAi();
                if (cfcData.TerritoryId == LahabreaTerritoryId)
                {
                    yield return new SetTarget(14643);
                    yield return new WaitCondition.Task(() => condition[ConditionFlag.Unconscious] || clientState.TerritoryType != LahabreaTerritoryId, "Wait(death)");
                    yield return new DisableAi();
                    yield return new WaitCondition.Task(() => !condition[ConditionFlag.Unconscious] || clientState.TerritoryType != LahabreaTerritoryId, "Wait(resurrection)");
                    yield return new EnableAi();
                }
                yield return new WaitSinglePlayerDuty(cfcData.ContentFinderConditionId);
                yield return new DisableAi();
                yield return new WaitAtEnd.WaitNextStepOrSequence();
            }
        }
    }

    internal sealed record StartSinglePlayerDuty(uint ContentFinderConditionId) : ITask
    {
        public override string ToString() => $"Wait(BossMod, entered instance {ContentFinderConditionId})";
    }

    internal sealed class StartSinglePlayerDutyExecutor : TaskExecutor<StartSinglePlayerDuty>
    {
        protected override bool Start() => true;

        public override unsafe ETaskResult Update()
        {
            return GameMain.Instance()->CurrentContentFinderConditionId == Task.ContentFinderConditionId
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
        BossModIpc bossModIpc,
        MovementController movementController)
        : TaskExecutor<WaitSinglePlayerDuty>, IStoppableTaskExecutor, IDebugStateProvider
    {
        protected override bool Start() => true;

        public override unsafe ETaskResult Update()
        {
            return GameMain.Instance()->CurrentContentFinderConditionId != Task.ContentFinderConditionId
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;
        }

        public void StopNow() => bossModIpc.DisableAi();

        public override bool ShouldInterruptOnDamage() => false;

        public string? GetDebugState()
        {
            if (!movementController.IsNavmeshReady)
                return $"Navmesh: {movementController.BuiltNavmeshPercent}%";
            else
                return null;
        }
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

    // TODO this should be handled in VBM
    internal sealed record SetTarget(uint DataId) : ITask
    {
        public override string ToString() => $"SetTarget({DataId})";
    }

    internal sealed class SetTargetExecutor(
        ITargetManager targetManager,
        IObjectTable objectTable) : TaskExecutor<SetTarget>
    {
        protected override bool Start() => true;

        public override ETaskResult Update()
        {
            if (targetManager.Target?.DataId == Task.DataId)
                return ETaskResult.TaskComplete;

            IGameObject? gameObject = objectTable.FirstOrDefault(x => x.DataId == Task.DataId);
            if (gameObject == null)
                return ETaskResult.StillRunning;

            targetManager.Target = gameObject;
            return ETaskResult.StillRunning;
        }

        public override bool ShouldInterruptOnDamage() => false;
    }
}
