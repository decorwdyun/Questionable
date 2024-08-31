using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps.Common;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class Emote
{
    internal sealed class Factory(ChatFunctions chatFunctions, Mount.Factory mountFactory) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType is EInteractionType.AcceptQuest or EInteractionType.CompleteQuest
                or EInteractionType.SinglePlayerDuty)
            {
                if (step.Emote == null)
                    return [];
            }
            else if (step.InteractionType != EInteractionType.Emote)
                return [];

            ArgumentNullException.ThrowIfNull(step.Emote);

            var unmount = mountFactory.Unmount();
            if (step.DataId != null)
            {
                var task = new UseOnObject(step.Emote.Value, step.DataId.Value, chatFunctions);
                return [unmount, task];
            }
            else
            {
                var task = new UseOnSelf(step.Emote.Value, chatFunctions);
                return [unmount, task];
            }
        }
    }

    private sealed class UseOnObject(EEmote emote, uint dataId, ChatFunctions chatFunctions) : AbstractDelayedTask
    {
        protected override bool StartInternal()
        {
            chatFunctions.UseEmote(dataId, emote);
            return true;
        }

        public override string ToString() => $"Emote({emote} on {dataId})";
    }

    private sealed class UseOnSelf(EEmote emote, ChatFunctions chatFunctions) : AbstractDelayedTask
    {
        protected override bool StartInternal()
        {
            chatFunctions.UseEmote(emote);
            return true;
        }

        public override string ToString() => $"Emote({emote})";
    }
}
