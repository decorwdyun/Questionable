using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Questionable.External;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.Interactions;

internal static class SinglePlayerDuty
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.SinglePlayerDuty)
                return [];

            ArgumentNullException.ThrowIfNull(step.DataId);
            return
            [
                serviceProvider.GetRequiredService<DisableYesAlready>(),
                serviceProvider.GetRequiredService<Interact.DoInteract>()
                    .With(step.DataId.Value, true),
                serviceProvider.GetRequiredService<RestoreYesAlready>()
            ];
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
            => throw new InvalidOperationException();
    }

    internal sealed class DisableYesAlready(YesAlreadyIpc yesAlreadyIpc) : ITask
    {
        public bool Start()
        {
            yesAlreadyIpc.DisableYesAlready();
            return true;
        }

        public ETaskResult Update() => ETaskResult.TaskComplete;

        public override string ToString() => "DisableYA";
    }

    internal sealed class RestoreYesAlready(YesAlreadyIpc yesAlreadyIpc, IGameGui gameGui) : ITask
    {
        public bool Start() => true;

        public ETaskResult Update()
        {
            if (gameGui.GetAddonByName("SelectYesno") != nint.Zero ||
                gameGui.GetAddonByName("DifficultySelectYesNo") != nint.Zero)
                return ETaskResult.StillRunning;

            yesAlreadyIpc.RestoreYesAlready();
            return ETaskResult.TaskComplete;
        }

        public override string ToString() => "Wait(DialogClosed) → RestoreYA";
    }
}
