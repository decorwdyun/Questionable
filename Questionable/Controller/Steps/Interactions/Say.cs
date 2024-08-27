using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps.Common;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class Say
{
    internal sealed class Factory(
        ChatFunctions chatFunctions,
        Mount.Factory mountFactory,
        ExcelFunctions excelFunctions) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType is EInteractionType.AcceptQuest or EInteractionType.CompleteQuest)
            {
                if (step.ChatMessage == null)
                    return [];
            }
            else if (step.InteractionType != EInteractionType.Say)
                return [];


            ArgumentNullException.ThrowIfNull(step.ChatMessage);

            string? excelString =
                excelFunctions.GetDialogueText(quest, step.ChatMessage.ExcelSheet, step.ChatMessage.Key, false)
                    .GetString();
            ArgumentNullException.ThrowIfNull(excelString);

            var unmount = mountFactory.Unmount();
            var task = new UseChat(excelString, chatFunctions);
            return [unmount, task];
        }
    }

    private sealed class UseChat(string chatMessage, ChatFunctions chatFunctions) : AbstractDelayedTask
    {
        protected override bool StartInternal()
        {
            chatFunctions.ExecuteCommand($"/say {chatMessage}");
            return true;
        }

        public override string ToString() => $"Say({chatMessage})";
    }
}
