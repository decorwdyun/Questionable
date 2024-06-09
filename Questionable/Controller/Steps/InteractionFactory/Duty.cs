using System;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.InteractionFactory;

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

    internal sealed class OpenDutyFinder(GameFunctions gameFunctions) : ITask
    {
        public uint ContentFinderConditionId { get; set; }

        public ITask With(uint contentFinderConditionId)
        {
            ContentFinderConditionId = contentFinderConditionId;
            return this;
        }

        public bool Start()
        {
            gameFunctions.OpenDutyFinder(ContentFinderConditionId);
            return true;
        }

        public ETaskResult Update() => ETaskResult.TaskComplete;

        public override string ToString() => $"OpenDutyFinder({ContentFinderConditionId})";
    }
}
