using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameUI;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps.Common;
using Questionable.Model;
using Questionable.Model.Questing;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Questionable.Controller.Steps.Leves;

internal static class InitiateLeve
{
    internal sealed class Factory(IServiceProvider serviceProvider, ICondition condition) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.InitiateLeve)
                yield break;

            yield return serviceProvider.GetRequiredService<OpenJournal>().With(quest.Id);
            yield return serviceProvider.GetRequiredService<Initiate>().With(quest.Id);
            yield return serviceProvider.GetRequiredService<SelectDifficulty>();
            yield return new WaitConditionTask(() => condition[ConditionFlag.BoundByDuty], "Wait(BoundByDuty)");
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
            => throw new NotImplementedException();
    }

    internal sealed unsafe class OpenJournal : ITask
    {
        private ElementId _elementId = null!;
        private uint _questType;

        public ITask With(ElementId elementId)
        {
            _elementId = elementId;
            _questType = _elementId is LeveId ? 2u : 1u;
            return this;
        }

        public bool Start()
        {
            AgentQuestJournal.Instance()->OpenForQuest(_elementId.Value, _questType);
            return true;
        }

        public ETaskResult Update()
        {
            AgentQuestJournal* agentQuestJournal = AgentQuestJournal.Instance();
            if (!agentQuestJournal->IsAgentActive())
                return ETaskResult.StillRunning;

            return agentQuestJournal->SelectedQuestId == _elementId.Value &&
                   agentQuestJournal->SelectedQuestType == _questType
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;
        }

        public override string ToString() => $"OpenJournal({_elementId})";
    }

    internal sealed unsafe class Initiate(IGameGui gameGui) : ITask
    {
        private ElementId _elementId = null!;

        public ITask With(ElementId elementId)
        {
            _elementId = elementId;
            return this;
        }

        public bool Start() => true;

        public ETaskResult Update()
        {
            if (gameGui.TryGetAddonByName("JournalDetail", out AtkUnitBase* addonJournalDetail))
            {
                var pickQuest = stackalloc AtkValue[]
                {
                    new() { Type = ValueType.Int, Int = 4 },
                    new() { Type = ValueType.UInt, Int = _elementId.Value }
                };
                addonJournalDetail->FireCallback(2, pickQuest);
                return ETaskResult.TaskComplete;
            }

            return ETaskResult.StillRunning;
        }

        public override string ToString() => $"InitiateLeve({_elementId})";
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
    }
}
