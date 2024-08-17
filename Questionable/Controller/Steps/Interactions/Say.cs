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
    internal sealed class Factory(IServiceProvider serviceProvider, ExcelFunctions excelFunctions) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Say)
                return [];


            ArgumentNullException.ThrowIfNull(step.ChatMessage);

            string? excelString =
                excelFunctions.GetDialogueText(quest, step.ChatMessage.ExcelSheet, step.ChatMessage.Key, false).GetString();
            ArgumentNullException.ThrowIfNull(excelString);

            var unmount = serviceProvider.GetRequiredService<UnmountTask>();
            var task = serviceProvider.GetRequiredService<UseChat>().With(excelString);
            return [unmount, task];
        }
    }

    internal sealed class UseChat(ChatFunctions chatFunctions) : AbstractDelayedTask
    {
        public string ChatMessage { get; set; } = null!;

        public ITask With(string chatMessage)
        {
            ChatMessage = chatMessage;
            return this;
        }

        protected override bool StartInternal()
        {
            chatFunctions.ExecuteCommand($"/say {ChatMessage}");
            return true;
        }

        public override string ToString() => $"Say({ChatMessage})";
    }
}
