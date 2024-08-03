using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Shared;

internal static class StepDisabled
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (!step.Disabled)
                return null;

            return serviceProvider.GetRequiredService<Task>();
        }
    }

    internal sealed class Task(ILogger<Task> logger) : ITask
    {
        public bool Start() => true;

        public ETaskResult Update()
        {
            logger.LogInformation("Skipping step, as it is disabled");
            return ETaskResult.SkipRemainingTasksForStep;
        }

        public override string ToString() => "StepDisabled";
    }
}
