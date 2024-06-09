using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps.BaseTasks;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.InteractionFactory;

internal static class Say
{
    internal sealed class Factory(IServiceProvider serviceProvider, GameFunctions gameFunctions) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Say)
                return [];


            ArgumentNullException.ThrowIfNull(step.ChatMessage);

            string? excelString = gameFunctions.GetDialogueText(quest, step.ChatMessage.ExcelSheet, step.ChatMessage.Key);
            ArgumentNullException.ThrowIfNull(excelString);

            var unmount = serviceProvider.GetRequiredService<UnmountTask>();
            var task = serviceProvider.GetRequiredService<UseChat>().With(excelString);
            return [unmount, task];
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
            => throw new InvalidOperationException();
    }

    internal sealed class UseChat(GameFunctions gameFunctions) : AbstractDelayedTask
    {
        public string ChatMessage { get; set; } = null!;

        public ITask With(string chatMessage)
        {
            ChatMessage = chatMessage;
            return this;
        }

        protected override bool StartInternal()
        {
            gameFunctions.ExecuteCommand($"/say {ChatMessage}");
            return true;
        }

        public override string ToString() => $"Say({ChatMessage})";
    }
}
