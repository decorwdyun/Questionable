using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class EquipRecommended
{
    internal sealed class Factory(IServiceProvider serviceProvider) : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.EquipRecommended)
                return null;

            return serviceProvider.GetRequiredService<DoEquipRecommended>();
        }
    }

    internal sealed class BeforeDutyOrInstance(IServiceProvider serviceProvider) : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Duty &&
                step.InteractionType != EInteractionType.SinglePlayerDuty &&
                step.InteractionType != EInteractionType.Combat)
                return null;

            return serviceProvider.GetRequiredService<DoEquipRecommended>();
        }
    }

    internal sealed unsafe class DoEquipRecommended(IClientState clientState, IChatGui chatGui) : ITask
    {
        private bool _equipped;

        public bool Start()
        {
            RecommendEquipModule.Instance()->SetupForClassJob((byte)clientState.LocalPlayer!.ClassJob.Id);
            return true;
        }

        public ETaskResult Update()
        {
            var recommendedEquipModule = RecommendEquipModule.Instance();
            if (recommendedEquipModule->IsUpdating)
                return ETaskResult.StillRunning;

            if (!_equipped)
            {
                InventoryManager* inventoryManager = InventoryManager.Instance();
                InventoryContainer* equippedItems =
                    inventoryManager->GetInventoryContainer(InventoryType.EquippedItems);
                bool isAllEquipped = true;
                foreach (var recommendedItemPtr in recommendedEquipModule->RecommendedItems)
                {
                    var recommendedItem = recommendedItemPtr.Value;
                    if (recommendedItem == null || recommendedItem->ItemId == 0)
                        continue;

                    bool isEquipped = false;
                    for (int i = 0; i < equippedItems->Size; ++i)
                    {
                        var equippedItem = equippedItems->Items[i];
                        if (equippedItem.ItemId != 0 && equippedItem.ItemId == recommendedItem->ItemId)
                        {
                            isEquipped = true;
                            break;
                        }
                    }

                    if (!isEquipped)
                        isAllEquipped = false;
                }

                if (!isAllEquipped)
                {
                    chatGui.Print("Equipping recommended gear.", "Questionable");
                    recommendedEquipModule->EquipRecommendedGear();
                }

                _equipped = true;
                return ETaskResult.StillRunning;
            }

            return ETaskResult.TaskComplete;
        }

        public override string ToString() => "EquipRecommended";
    }
}
