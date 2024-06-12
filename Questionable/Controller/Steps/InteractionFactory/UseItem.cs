using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps.BaseTasks;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.InteractionFactory;

internal static class UseItem
{
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

    internal abstract class UseItemBase : ITask
    {
        private bool _usedItem;
        private DateTime _continueAt;

        protected abstract bool UseItem();

        public bool Start()
        {
            _usedItem = UseItem();
            _continueAt = DateTime.Now.AddSeconds(2);
            return true;
        }

        public ETaskResult Update()
        {
            if (DateTime.Now > _continueAt)
                return ETaskResult.StillRunning;

            if (!_usedItem)
            {
                _usedItem = UseItem();
                _continueAt = DateTime.Now.AddSeconds(2);
                return ETaskResult.StillRunning;
            }

            return ETaskResult.TaskComplete;
        }
    }


    internal sealed class UseOnGround(GameFunctions gameFunctions) : UseItemBase
    {
        public uint DataId { get; set; }
        public uint ItemId { get; set; }

        public ITask With(uint dataId, uint itemId)
        {
            DataId = dataId;
            ItemId = itemId;
            return this;
        }

        protected override bool UseItem() => gameFunctions.UseItemOnGround(DataId, ItemId);

        public override string ToString() => $"UseItem({ItemId} on ground at {DataId})";
    }

    internal sealed class UseOnObject(GameFunctions gameFunctions) : UseItemBase
    {
        public uint DataId { get; set; }
        public uint ItemId { get; set; }

        public ITask With(uint dataId, uint itemId)
        {
            DataId = dataId;
            ItemId = itemId;
            return this;
        }

        protected override bool UseItem() => gameFunctions.UseItem(DataId, ItemId);

        public override string ToString() => $"UseItem({ItemId} on {DataId})";
    }

    internal sealed class Use(GameFunctions gameFunctions) : UseItemBase
    {
        public uint ItemId { get; set; }

        public ITask With(uint itemId)
        {
            ItemId = itemId;
            return this;
        }

        protected override bool UseItem() => gameFunctions.UseItem(ItemId);

        public override string ToString() => $"UseItem({ItemId})";
    }
}
