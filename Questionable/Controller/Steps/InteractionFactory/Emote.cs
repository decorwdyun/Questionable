using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps.BaseTasks;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.InteractionFactory;

internal static class Emote
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType is EInteractionType.AcceptQuest or EInteractionType.CompleteQuest)
            {
                if (step.Emote == null)
                    return [];
            }
            if (step.InteractionType != EInteractionType.Emote)
                return [];

            ArgumentNullException.ThrowIfNull(step.Emote);

            var unmount = serviceProvider.GetRequiredService<UnmountTask>();
            if (step.DataId != null)
            {
                var task = serviceProvider.GetRequiredService<UseOnObject>().With(step.Emote.Value, step.DataId.Value);
                return [unmount, task];
            }
            else
            {
                var task = serviceProvider.GetRequiredService<Use>().With(step.Emote.Value);
                return [unmount, task];
            }
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
            => throw new InvalidOperationException();
    }

    internal sealed class UseOnObject(ChatFunctions chatFunctions) : AbstractDelayedTask
    {
        public EEmote Emote { get; set; }
        public uint DataId { get; set; }

        public ITask With(EEmote emote, uint dataId)
        {
            Emote = emote;
            DataId = dataId;
            return this;
        }

        protected override bool StartInternal()
        {
            chatFunctions.UseEmote(DataId, Emote);
            return true;
        }

        public override string ToString() => $"Emote({Emote} on {DataId})";
    }

    internal sealed class Use(ChatFunctions chatFunctions) : AbstractDelayedTask
    {
        public EEmote Emote { get; set; }

        public ITask With(EEmote emote)
        {
            Emote = emote;
            return this;
        }

        protected override bool StartInternal()
        {
            chatFunctions.UseEmote(Emote);
            return true;
        }

        public override string ToString() => $"Emote({Emote})";
    }
}
