using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class Duty
{
    internal sealed class Factory(GameFunctions gameFunctions, ICondition condition) : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Duty)
                return null;

            ArgumentNullException.ThrowIfNull(step.ContentFinderConditionId);
            return new OpenDutyFinder(step.ContentFinderConditionId.Value, gameFunctions, condition);
        }
    }

    private sealed class OpenDutyFinder(
        uint contentFinderConditionId,
        GameFunctions gameFunctions,
        ICondition condition) : ITask
    {
        public bool Start()
        {
            if (condition[ConditionFlag.InDutyQueue])
                return false;

            gameFunctions.OpenDutyFinder(contentFinderConditionId);
            return true;
        }

        public ETaskResult Update() => ETaskResult.TaskComplete;

        public override string ToString() => $"OpenDutyFinder({contentFinderConditionId})";
    }
}
