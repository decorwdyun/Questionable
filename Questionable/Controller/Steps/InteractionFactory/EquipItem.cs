using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.GeneratedSheets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.BaseTasks;
using Questionable.Model.V1;
using Quest = Questionable.Model.Quest;

namespace Questionable.Controller.Steps.InteractionFactory;

internal static class EquipItem
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.EquipItem)
                return null;

            ArgumentNullException.ThrowIfNull(step.ItemId);
            return serviceProvider.GetRequiredService<DoEquip>()
                .With(step.ItemId.Value);
        }
    }

    internal sealed class DoEquip(IDataManager dataManager, ILogger<DoEquip> logger)
        : AbstractDelayedTask(TimeSpan.FromSeconds(1))
    {
        private static readonly IReadOnlyList<InventoryType> SourceInventoryTypes =
        [
            InventoryType.ArmoryMainHand,
            InventoryType.ArmoryOffHand,
            InventoryType.ArmoryHead,
            InventoryType.ArmoryBody,
            InventoryType.ArmoryHands,
            InventoryType.ArmoryLegs,
            InventoryType.ArmoryFeets,

            InventoryType.ArmoryEar,
            InventoryType.ArmoryNeck,
            InventoryType.ArmoryWrist,
            InventoryType.ArmoryRings,

            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4,
        ];

        private uint _itemId;
        private Item _item = null!;
        private List<ushort> _targetSlots = [];

        public ITask With(uint itemId)
        {
            _itemId = itemId;
            _item = dataManager.GetExcelSheet<Item>()!.GetRow(itemId) ??
                    throw new ArgumentOutOfRangeException(nameof(itemId));
            _targetSlots = GetEquipSlot(_item) ?? throw new InvalidOperationException("Not a piece of equipment");
            return this;
        }

        protected override unsafe bool StartInternal()
        {
            var inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null)
                return false;

            var equippedContainer = inventoryManager->GetInventoryContainer(InventoryType.EquippedItems);
            if (equippedContainer == null)
                return false;

            if (_targetSlots.Any(slot => equippedContainer->GetInventorySlot(slot)->ItemID == _itemId))
            {
                logger.LogInformation("Already equipped {Item}, skipping step", _item.Name?.ToString());
                return false;
            }

            foreach (InventoryType sourceInventoryType in SourceInventoryTypes)
            {
                var sourceContainer = inventoryManager->GetInventoryContainer(sourceInventoryType);
                if (sourceContainer == null)
                    continue;

                if (inventoryManager->GetItemCountInContainer(_itemId, sourceInventoryType, true) == 0 &&
                    inventoryManager->GetItemCountInContainer(_itemId, sourceInventoryType) == 0)
                    continue;

                for (ushort sourceSlot = 0; sourceSlot < sourceContainer->Size; sourceSlot++)
                {
                    var sourceItem = sourceContainer->GetInventorySlot(sourceSlot);
                    if (sourceItem == null || sourceItem->ItemID != _itemId)
                        continue;

                    // Move the item to the first available slot
                    ushort targetSlot = _targetSlots
                        .Where(x => inventoryManager->GetInventorySlot(InventoryType.EquippedItems, x)->ItemID == 0)
                        .Concat(_targetSlots).First();

                    logger.LogInformation(
                        "Equipping item from {SourceInventory}, {SourceSlot} to {TargetInventory}, {TargetSlot}",
                        sourceInventoryType, sourceSlot, InventoryType.EquippedItems, targetSlot);

                    int result = inventoryManager->MoveItemSlot(sourceInventoryType, sourceSlot,
                        InventoryType.EquippedItems, targetSlot, 1);
                    logger.LogInformation("MoveItemSlot result: {Result}", result);
                    return true;
                }
            }

            return false;
        }

        protected override unsafe ETaskResult UpdateInternal()
        {
            InventoryManager* inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null)
                return ETaskResult.StillRunning;

            if (_targetSlots.Any(x =>
                    inventoryManager->GetInventorySlot(InventoryType.EquippedItems, x)->ItemID == _itemId))
                return ETaskResult.TaskComplete;

            return ETaskResult.StillRunning;
        }

        private static List<ushort>? GetEquipSlot(Item item)
        {
            return item.EquipSlotCategory.Row switch
            {
                >= 1 and <= 11 => [(ushort)(item.EquipSlotCategory.Row - 1)],
                12 => [11, 12], // rings
                17 => [14], // soul crystal
                _ => null
            };
        }

        public override string ToString() => $"Equip({_item.Name})";
    }
}
