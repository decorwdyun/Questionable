using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameUI;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps.Common;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Questionable.Controller.Steps.Leves;

internal static class InitiateLeve
{
    internal sealed class Factory(IGameGui gameGui, ICondition condition) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.InitiateLeve)
                yield break;

            yield return new SkipInitiateIfActive(quest.Id);
            yield return new OpenJournal(quest.Id);
            yield return new Initiate(quest.Id, gameGui);
            yield return new SelectDifficulty(gameGui);
            yield return new WaitConditionTask(() => condition[ConditionFlag.BoundByDuty], "Wait(BoundByDuty)");
        }
    }

    internal sealed unsafe class SkipInitiateIfActive(ElementId elementId) : ITask
    {
        public bool Start() => true;

        public ETaskResult Update()
        {
            var director = UIState.Instance()->DirectorTodo.Director;
            if (director != null &&
                director->EventHandlerInfo != null &&
                director->EventHandlerInfo->EventId.ContentId == EventHandlerType.GatheringLeveDirector &&
                director->ContentId == elementId.Value)
                return ETaskResult.SkipRemainingTasksForStep;

            return ETaskResult.TaskComplete;
        }

        public override string ToString() => $"CheckIfAlreadyActive({elementId})";
    }

    internal sealed unsafe class OpenJournal(ElementId elementId) : ITask
    {
        private readonly uint _questType = elementId is LeveId ? 2u : 1u;
        private DateTime _openedAt = DateTime.MinValue;

        public bool Start()
        {
            AgentQuestJournal.Instance()->OpenForQuest(elementId.Value, _questType);
            _openedAt = DateTime.Now;
            return true;
        }

        public ETaskResult Update()
        {
            AgentQuestJournal* agentQuestJournal = AgentQuestJournal.Instance();
            if (agentQuestJournal->IsAgentActive() &&
                agentQuestJournal->SelectedQuestId == elementId.Value &&
                agentQuestJournal->SelectedQuestType == _questType)
                return ETaskResult.TaskComplete;

            if (DateTime.Now > _openedAt.AddSeconds(3))
            {
                AgentQuestJournal.Instance()->OpenForQuest(elementId.Value, _questType);
                _openedAt = DateTime.Now;
            }

            return ETaskResult.StillRunning;
        }

        public override string ToString() => $"OpenJournal({elementId})";
    }

    internal sealed unsafe class Initiate(ElementId elementId, IGameGui gameGui) : ITask
    {
        public bool Start() => true;

        public ETaskResult Update()
        {
            if (gameGui.TryGetAddonByName("JournalDetail", out AtkUnitBase* addonJournalDetail))
            {
                var pickQuest = stackalloc AtkValue[]
                {
                    new() { Type = ValueType.Int, Int = 4 },
                    new() { Type = ValueType.UInt, Int = elementId.Value }
                };
                addonJournalDetail->FireCallback(2, pickQuest);
                return ETaskResult.TaskComplete;
            }

            return ETaskResult.StillRunning;
        }

        public override string ToString() => $"InitiateLeve({elementId})";
    }

    internal sealed unsafe class SelectDifficulty(IGameGui gameGui) : ITask
    {
        public bool Start() => true;

        public ETaskResult Update()
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

        public override string ToString() => "SelectLeveDifficulty";
    }
}
