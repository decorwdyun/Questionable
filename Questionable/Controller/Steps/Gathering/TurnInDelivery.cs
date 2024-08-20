using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Model;
using Questionable.Model.Questing;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Questionable.Controller.Steps.Gathering;

internal static class TurnInDelivery
{
    internal sealed class Factory(ILoggerFactory loggerFactory) : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (quest.Id is not SatisfactionSupplyNpcId || sequence.Sequence != 1)
                return null;

            return new SatisfactionSupplyTurnIn(loggerFactory.CreateLogger<SatisfactionSupplyTurnIn>());
        }
    }

    private sealed class SatisfactionSupplyTurnIn(ILogger<SatisfactionSupplyTurnIn> logger) : ITask
    {
        private ushort? _remainingAllowances;

        public bool Start() => true;

        public unsafe ETaskResult Update()
        {
            AgentSatisfactionSupply* agentSatisfactionSupply = AgentSatisfactionSupply.Instance();
            if (agentSatisfactionSupply == null || !agentSatisfactionSupply->IsAgentActive())
                return _remainingAllowances == null ? ETaskResult.StillRunning : ETaskResult.TaskComplete;

            var addonId = agentSatisfactionSupply->GetAddonId();
            if (addonId == 0)
                return _remainingAllowances == null ? ETaskResult.StillRunning : ETaskResult.TaskComplete;

            AtkUnitBase* addon = LAddon.GetAddonById(addonId);
            if (addon == null || !LAddon.IsAddonReady(addon))
                return ETaskResult.StillRunning;

            ushort remainingAllowances = agentSatisfactionSupply->RemainingAllowances;
            if (remainingAllowances == 0)
            {
                logger.LogInformation("No remaining weekly allowances");
                addon->FireCallbackInt(0);
                return ETaskResult.TaskComplete;
            }

            if (InventoryManager.Instance()->GetInventoryItemCount(agentSatisfactionSupply->Items[1].Id,
                    minCollectability: (short)agentSatisfactionSupply->Items[1].Collectability1) == 0)
            {
                logger.LogInformation("Inventory has no {ItemId}", agentSatisfactionSupply->Items[1].Id);
                addon->FireCallbackInt(0);
                return ETaskResult.TaskComplete;
            }

            // we should at least wait until we have less allowances
            if (_remainingAllowances == remainingAllowances)
                return ETaskResult.StillRunning;

            // try turning it in...
            logger.LogInformation("Attempting turn-in (remaining allowances: {RemainingAllowances})",
                remainingAllowances);
            _remainingAllowances = remainingAllowances;

            var pickGatheringItem = stackalloc AtkValue[]
            {
                new() { Type = ValueType.Int, Int = 1 },
                new() { Type = ValueType.Int, Int = 1 }
            };
            addon->FireCallback(2, pickGatheringItem);
            return ETaskResult.StillRunning;
        }

        public override string ToString() => "WeeklyDeliveryTurnIn";
    }
}
