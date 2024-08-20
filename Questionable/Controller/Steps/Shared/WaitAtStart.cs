using System;
using Questionable.Controller.Steps.Common;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Shared;

internal static class WaitAtStart
{
    internal sealed class Factory : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.DelaySecondsAtStart == null)
                return null;

            return new WaitDelay(TimeSpan.FromSeconds(step.DelaySecondsAtStart.Value));
        }
    }

    internal sealed class WaitDelay(TimeSpan delay) : AbstractDelayedTask(delay)
    {
        protected override bool StartInternal() => true;

        public override string ToString() => $"Wait[S](seconds: {Delay.TotalSeconds})";
    }
}
