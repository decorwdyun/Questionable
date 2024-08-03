using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class Duty
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Duty)
                return null;

            ArgumentNullException.ThrowIfNull(step.ContentFinderConditionId);

            return serviceProvider.GetRequiredService<OpenDutyFinder>()
                .With(step.ContentFinderConditionId.Value);
        }
    }

    internal sealed class OpenDutyFinder(GameFunctions gameFunctions, ICondition condition) : ITask
    {
        public uint ContentFinderConditionId { get; set; }

        public ITask With(uint contentFinderConditionId)
        {
            ContentFinderConditionId = contentFinderConditionId;
            return this;
        }

        public bool Start()
        {
            if (condition[ConditionFlag.InDutyQueue])
                return false;

            gameFunctions.OpenDutyFinder(ContentFinderConditionId);
            return true;
        }

        public ETaskResult Update() => ETaskResult.TaskComplete;

        public override string ToString() => $"OpenDutyFinder({ContentFinderConditionId})";
    }
}
