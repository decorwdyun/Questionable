using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameUI;
using Questionable.Controller.Steps.Common;
using Questionable.Model;
using Questionable.Model.Questing;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Questionable.Controller.Steps.Leves;

internal static class InitiateLeve
{
    internal sealed class Factory(ICondition condition) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.InitiateLeve)
                yield break;

            yield return new SkipInitiateIfActive(quest.Id);
            yield return new OpenJournal(quest.Id);
            yield return new Initiate(quest.Id);
            yield return new SelectDifficulty();
            yield return new WaitCondition.Task(() => condition[ConditionFlag.BoundByDuty], "Wait(BoundByDuty)");
        }
    }

    internal sealed record SkipInitiateIfActive(ElementId ElementId) : ITask
    {
        public override string ToString() => $"CheckIfAlreadyActive({ElementId})";
    }

    internal sealed unsafe class SkipInitiateIfActiveExecutor : TaskExecutor<SkipInitiateIfActive>
    {
        protected override bool Start() => true;

        public override ETaskResult Update()
        {
            var director = UIState.Instance()->DirectorTodo.Director;
            if (director != null &&
                director->Info.EventId.ContentId == EventHandlerContent.GatheringLeveDirector &&
                director->ContentId == Task.ElementId.Value)
                return ETaskResult.SkipRemainingTasksForStep;

            return ETaskResult.TaskComplete;
        }

        public override bool ShouldInterruptOnDamage() => false;
    }

    internal sealed record OpenJournal(ElementId ElementId) : ITask
    {
        public uint QuestType => ElementId is LeveId ? 2u : 1u;
        public override string ToString() => $"OpenJournal({ElementId})";
    }

    internal sealed unsafe class OpenJournalExecutor : TaskExecutor<OpenJournal>
    {
        private DateTime _openedAt = DateTime.MinValue;

        protected override bool Start()
        {
            AgentQuestJournal.Instance()->OpenForQuest(Task.ElementId.Value, Task.QuestType);
            _openedAt = DateTime.Now;
            return true;
        }

        public override ETaskResult Update()
        {
            AgentQuestJournal* agentQuestJournal = AgentQuestJournal.Instance();
            if (agentQuestJournal->IsAgentActive() &&
                agentQuestJournal->SelectedQuestId == Task.ElementId.Value &&
                agentQuestJournal->SelectedQuestType == Task.QuestType)
                return ETaskResult.TaskComplete;

            if (DateTime.Now > _openedAt.AddSeconds(3))
            {
                AgentQuestJournal.Instance()->OpenForQuest(Task.ElementId.Value, Task.QuestType);
                _openedAt = DateTime.Now;
            }

            return ETaskResult.StillRunning;
        }

        public override bool ShouldInterruptOnDamage() => false;
    }

    internal sealed record Initiate(ElementId ElementId) : ITask
    {
        public override string ToString() => $"InitiateLeve({ElementId})";
    }

    internal sealed unsafe class InitiateExecutor(IGameGui gameGui) : TaskExecutor<Initiate>
    {
        protected override bool Start() => true;

        public override ETaskResult Update()
        {
            if (gameGui.TryGetAddonByName("JournalDetail", out AtkUnitBase* addonJournalDetail))
            {
                var pickQuest = stackalloc AtkValue[]
                {
                    new() { Type = ValueType.Int, Int = 4 },
                    new() { Type = ValueType.UInt, Int = Task.ElementId.Value }
                };
                addonJournalDetail->FireCallback(2, pickQuest);
                return ETaskResult.TaskComplete;
            }

            return ETaskResult.StillRunning;
        }

        public override bool ShouldInterruptOnDamage() => false;
    }

    internal sealed class SelectDifficulty : ITask
    {
        public override string ToString() => "SelectLeveDifficulty";
    }

    internal sealed unsafe class SelectDifficultyExecutor(IGameGui gameGui) : TaskExecutor<SelectDifficulty>
    {
        protected override bool Start() => true;

        public override ETaskResult Update()
        {
            if (gameGui.TryGetAddonByName("GuildLeveDifficulty", out AtkUnitBase* addon))
            {
                // atkvalues: 1 → default difficulty, 2 → min, 3 → max
                var pickDifficulty = stackalloc AtkValue[]
                {
                    new() { Type = ValueType.Int, Int = 0 },
                    new() { Type = ValueType.Int, Int = addon->AtkValues[1].Int }
                };
                addon->FireCallback(2, pickDifficulty, true);
                return ETaskResult.TaskComplete;
            }

            return ETaskResult.StillRunning;
        }

        public override bool ShouldInterruptOnDamage() => false;
    }
}
