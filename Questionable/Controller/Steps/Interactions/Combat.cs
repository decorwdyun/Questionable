using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps.Common;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.Interactions;

internal static class Combat
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Combat)
                return [];

            ArgumentNullException.ThrowIfNull(step.EnemySpawnType);

            var unmount = serviceProvider.GetRequiredService<UnmountTask>();
            if (step.EnemySpawnType == EEnemySpawnType.AfterInteraction)
            {
                ArgumentNullException.ThrowIfNull(step.DataId);

                var task = serviceProvider.GetRequiredService<Interact.DoInteract>()
                    .With(step.DataId.Value, true);
                return [unmount, task];
            }
            else if (step.EnemySpawnType == EEnemySpawnType.AfterItemUse)
            {
                ArgumentNullException.ThrowIfNull(step.DataId);
                ArgumentNullException.ThrowIfNull(step.ItemId);

                var task = serviceProvider.GetRequiredService<UseItem.UseOnObject>()
                    .With(step.DataId.Value, step.ItemId.Value);
                return [unmount, task];
            }
            else
                // automatically triggered when entering area, i.e. only unmount
                return [unmount];
        }

        public ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
            => throw new InvalidOperationException();
    }
}
