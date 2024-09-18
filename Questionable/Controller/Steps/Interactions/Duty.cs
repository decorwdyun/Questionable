using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class Duty
{
    internal sealed class Factory : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Duty)
                return null;

            ArgumentNullException.ThrowIfNull(step.ContentFinderConditionId);
            return new Task(step.ContentFinderConditionId.Value);
        }
    }

    internal sealed record Task(uint ContentFinderConditionId) : ITask
    {
        public override string ToString() => $"OpenDutyFinder({ContentFinderConditionId})";
    }

    internal sealed class Executor(
        GameFunctions gameFunctions,
        ICondition condition) : TaskExecutor<Task>
    {
        protected override bool Start()
        {
            if (condition[ConditionFlag.InDutyQueue])
                return false;

            gameFunctions.OpenDutyFinder(Task.ContentFinderConditionId);
            return true;
        }

        public override ETaskResult Update() => ETaskResult.TaskComplete;
    }
}
