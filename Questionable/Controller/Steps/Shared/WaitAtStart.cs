using System;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps.Common;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Shared;

internal static class WaitAtStart
{
    internal sealed class Factory(IServiceProvider serviceProvider) : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.DelaySecondsAtStart == null)
                return null;

            return serviceProvider.GetRequiredService<WaitDelay>()
                .With(TimeSpan.FromSeconds(step.DelaySecondsAtStart.Value));
        }
    }

    internal sealed class WaitDelay : AbstractDelayedTask
    {
        public ITask With(TimeSpan delay)
        {
            Delay = delay;
            return this;
        }

        protected override bool StartInternal() => true;

        public override string ToString() => $"Wait[S](seconds: {Delay.TotalSeconds})";
    }
}
