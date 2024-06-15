using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.BaseTasks;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.InteractionFactory;

internal static class UseItem
{
    public const int VesperBayAetheryteTicket = 30362;

    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.UseItem)
                return [];

            ArgumentNullException.ThrowIfNull(step.ItemId);

            var unmount = serviceProvider.GetRequiredService<UnmountTask>();
            if (step.GroundTarget == true)
            {
                ArgumentNullException.ThrowIfNull(step.DataId);

                var task = serviceProvider.GetRequiredService<UseOnGround>()
                    .With(step.DataId.Value, step.ItemId.Value);
                return [unmount, task];
            }
            else if (step.DataId != null)
            {
                var task = serviceProvider.GetRequiredService<UseOnObject>()
                    .With(step.DataId.Value, step.ItemId.Value);
                return [unmount, task];
            }
            else
            {
                var task = serviceProvider.GetRequiredService<Use>()
                    .With(step.ItemId.Value);
                return [unmount, task];
            }
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
            => throw new InvalidOperationException();
    }

    internal abstract class UseItemBase(ILogger logger) : ITask
    {
        private bool _usedItem;
        private DateTime _continueAt;
        private int _itemCount;

        public uint ItemId { get; set; }

        protected abstract bool UseItem();

        public unsafe bool Start()
        {
            InventoryManager* inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null)
                throw new TaskException("No InventoryManager");

            _itemCount = inventoryManager->GetInventoryItemCount(ItemId);
            if (_itemCount == 0)
                throw new TaskException($"Don't have any {ItemId} in inventory (NQ only)");

            _usedItem = UseItem();
            if (ItemId == VesperBayAetheryteTicket)
                _continueAt = DateTime.Now.AddSeconds(11);
            else
                _continueAt = DateTime.Now.AddSeconds(2);
            return true;
        }

        public unsafe ETaskResult Update()
        {
            if (DateTime.Now <= _continueAt)
                return ETaskResult.StillRunning;

            if (ItemId == VesperBayAetheryteTicket)
            {
                InventoryManager* inventoryManager = InventoryManager.Instance();
                if (inventoryManager == null)
                {
                    logger.LogWarning("InventoryManager is not available");
                    return ETaskResult.StillRunning;
                }

                int itemCount = inventoryManager->GetInventoryItemCount(ItemId);
                if (itemCount == _itemCount)
                {
                    // TODO Better handling for game-provided errors, i.e. reacting to the 'Could not use' messages. UseItem() is successful in this case (and returns 0)
                    logger.LogInformation("Attempted to use vesper bay aetheryte ticket, but it didn't consume an item - reattempting next frame");
                    _usedItem = false;
                    return ETaskResult.StillRunning;
                }
            }

            if (!_usedItem)
            {
                _usedItem = UseItem();
                _continueAt = DateTime.Now.AddSeconds(2);
                return ETaskResult.StillRunning;
            }

            return ETaskResult.TaskComplete;
        }
    }


    internal sealed class UseOnGround(GameFunctions gameFunctions, ILogger<UseOnGround> logger) : UseItemBase(logger)
    {
        public uint DataId { get; set; }

        public ITask With(uint dataId, uint itemId)
        {
            DataId = dataId;
            ItemId = itemId;
            return this;
        }

        protected override bool UseItem() => gameFunctions.UseItemOnGround(DataId, ItemId);

        public override string ToString() => $"UseItem({ItemId} on ground at {DataId})";
    }

    internal sealed class UseOnObject(GameFunctions gameFunctions, ILogger<UseOnObject> logger) : UseItemBase(logger)
    {
        public uint DataId { get; set; }

        public ITask With(uint dataId, uint itemId)
        {
            DataId = dataId;
            ItemId = itemId;
            return this;
        }

        protected override bool UseItem() => gameFunctions.UseItem(DataId, ItemId);

        public override string ToString() => $"UseItem({ItemId} on {DataId})";
    }

    internal sealed class Use(GameFunctions gameFunctions, ILogger<Use> logger) : UseItemBase(logger)
    {
        public ITask With(uint itemId)
        {
            ItemId = itemId;
            return this;
        }

        protected override bool UseItem() => gameFunctions.UseItem(ItemId);

        public override string ToString() => $"UseItem({ItemId})";
    }
}
