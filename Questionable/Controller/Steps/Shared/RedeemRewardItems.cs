using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Shared;

internal static class RedeemRewardItems
{
    internal sealed class Factory(QuestData questData) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.AcceptQuest)
                return [];

            List<ITask> tasks = [];
            unsafe
            {
                InventoryManager* inventoryManager = InventoryManager.Instance();
                if (inventoryManager == null)
                    return tasks;

                foreach (var itemReward in questData.RedeemableItems)
                {
                    if (inventoryManager->GetInventoryItemCount(itemReward.ItemId) > 0 &&
                        !itemReward.IsUnlocked())
                    {
                        tasks.Add(new Task(itemReward));
                    }
                }
            }

            return tasks;
        }
    }

    internal sealed record Task(ItemReward ItemReward) : ITask
    {
        public override string ToString() => $"TryRedeem({ItemReward.Name})";
    }

    internal sealed class Executor(
        GameFunctions gameFunctions,
        ICondition condition) : TaskExecutor<Task>
    {
        private DateTime _continueAt;

        protected override bool Start()
        {
            if (condition[ConditionFlag.Mounted])
                return false;

            _continueAt = DateTime.Now.Add(Task.ItemReward.CastTime).AddSeconds(1);
            return gameFunctions.UseItem(Task.ItemReward.ItemId);
        }

        public override ETaskResult Update()
        {
            if (condition[ConditionFlag.Casting])
                return ETaskResult.StillRunning;

            return DateTime.Now <= _continueAt ? ETaskResult.StillRunning : ETaskResult.TaskComplete;
        }
    }
}
