using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Common;
using Questionable.Controller.Steps.Shared;
using Questionable.Model;
using Questionable.Model.V1;
using AethernetShortcut = Questionable.Controller.Steps.Shared.AethernetShortcut;

namespace Questionable.Controller.Steps.Interactions;

internal static class UseItem
{
    public const int VesperBayAetheryteTicket = 30362;

    internal sealed class Factory(IServiceProvider serviceProvider, ILogger<Factory> logger) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.UseItem)
                return [];

            ArgumentNullException.ThrowIfNull(step.ItemId);

            if (step.ItemId == VesperBayAetheryteTicket)
            {
                unsafe
                {
                    InventoryManager* inventoryManager = InventoryManager.Instance();
                    if (inventoryManager->GetInventoryItemCount(step.ItemId.Value) == 0)
                        return CreateVesperBayFallbackTask();
                }
            }

            var unmount = serviceProvider.GetRequiredService<UnmountTask>();
            if (step.GroundTarget == true)
            {
                ITask task;
                if (step.DataId != null)
                    task = serviceProvider.GetRequiredService<UseOnGround>()
                        .With(step.DataId.Value, step.ItemId.Value);
                else
                {
                    ArgumentNullException.ThrowIfNull(step.Position);
                    task = serviceProvider.GetRequiredService<UseOnPosition>()
                        .With(step.Position.Value, step.ItemId.Value);
                }

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

        private IEnumerable<ITask> CreateVesperBayFallbackTask()
        {
            logger.LogWarning("No vesper bay aetheryte tickets in inventory, navigating via ferry in Limsa instead");

            uint npcId = 1003540;
            ushort territoryId = 129;
            Vector3 destination = new(-360.9217f, 8f, 38.92566f);
            yield return serviceProvider.GetRequiredService<AetheryteShortcut.UseAetheryteShortcut>()
                .With(null, EAetheryteLocation.Limsa, territoryId);
            yield return serviceProvider.GetRequiredService<AethernetShortcut.UseAethernetShortcut>()
                .With(EAetheryteLocation.Limsa, EAetheryteLocation.LimsaArcanist);
            yield return serviceProvider.GetRequiredService<WaitAtEnd.WaitDelay>();
            yield return serviceProvider.GetRequiredService<Move.MoveInternal>()
                .With(territoryId, destination, dataId: npcId, sprint: false);
            yield return serviceProvider.GetRequiredService<Interact.DoInteract>()
                .With(npcId, true);
        }
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
                    logger.LogInformation(
                        "Attempted to use vesper bay aetheryte ticket, but it didn't consume an item - reattempting next frame");
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

    internal sealed class UseOnPosition(GameFunctions gameFunctions, ILogger<UseOnPosition> logger)
        : UseItemBase(logger)
    {
        public Vector3 Position { get; set; }

        public ITask With(Vector3 position, uint itemId)
        {
            Position = position;
            ItemId = itemId;
            return this;
        }

        protected override bool UseItem() => gameFunctions.UseItemOnPosition(Position, ItemId);

        public override string ToString() =>
            $"UseItem({ItemId} on ground at {Position.ToString("G", CultureInfo.InvariantCulture)})";
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
