using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps.BaseTasks;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.BaseFactory;

internal static class WaitAtEnd
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.CompletionQuestVariablesFlags.Count == 6)
            {
                var task = serviceProvider.GetRequiredService<WaitForCompletionFlags>()
                    .With(quest, step);
                var delay = serviceProvider.GetRequiredService<WaitDelay>();
                return [task, delay, new NextStep()];
            }

            switch (step.InteractionType)
            {
                case EInteractionType.Combat:
                case EInteractionType.WaitForManualProgress:
                case EInteractionType.ShouldBeAJump:
                case EInteractionType.Instruction:
                    return [serviceProvider.GetRequiredService<WaitNextStepOrSequence>()];

                case EInteractionType.Duty:
                case EInteractionType.SinglePlayerDuty:
                    return [new EndAutomation()];

                case EInteractionType.WalkTo:
                case EInteractionType.Jump:
                    // no need to wait if we're just moving around
                    return [new NextStep()];

                case EInteractionType.WaitForObjectAtPosition:
                    return [serviceProvider.GetRequiredService<WaitObjectAtPosition>(), new NextStep()];

                default:
                    return [serviceProvider.GetRequiredService<WaitDelay>(), new NextStep()];
            }
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
            => throw new InvalidOperationException();
    }

    internal sealed class WaitDelay() : AbstractDelayedTask(TimeSpan.FromSeconds(1))
    {
        protected override bool StartInternal() => true;

        public override string ToString() => $"Wait(seconds: {Delay.TotalSeconds})";
    }

    internal sealed class WaitNextStepOrSequence : ITask
    {
        public bool Start() => true;

        public ETaskResult Update() => ETaskResult.StillRunning;

        public override string ToString() => "Wait(next step or sequence)";
    }

    internal sealed class WaitForCompletionFlags(GameFunctions gameFunctions) : ITask
    {
        public Quest Quest { get; set; } = null!;
        public QuestStep Step { get; set; } = null!;
        public IList<short?> Flags { get; set; } = null!;

        public ITask With(Quest quest, QuestStep step)
        {
            Quest = quest;
            Step = step;
            Flags = step.CompletionQuestVariablesFlags;
            return this;
        }

        public bool Start() => true;

        public ETaskResult Update()
        {
            QuestWork? questWork = gameFunctions.GetQuestEx(Quest.QuestId);
            return questWork != null && Step.MatchesQuestVariables(questWork.Value)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;
        }

        public override string ToString() =>
            $"WaitCF({string.Join(", ", Flags.Select(x => x?.ToString(CultureInfo.InvariantCulture) ?? "-"))})";
    }

    internal sealed class WaitObjectAtPosition(GameFunctions gameFunctions) : ITask
    {
        public uint DataId { get; set; }
        public Vector3 Destination { get; set; }

        public bool Start() => true;

        public ETaskResult Update() =>
            gameFunctions.IsObjectAtPosition(DataId, Destination)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;

        public override string ToString() =>
            $"WaitObj({DataId} at {Destination.ToString("G", CultureInfo.InvariantCulture)})";
    }

    internal sealed class NextStep : ILastTask
    {
        public bool Start() => true;

        public ETaskResult Update() => ETaskResult.NextStep;

        public override string ToString() => "Next Step";
    }

    internal sealed class EndAutomation : ILastTask
    {
        public bool Start() => true;

        public ETaskResult Update() => ETaskResult.End;

        public override string ToString() => "End automation";
    }
}
