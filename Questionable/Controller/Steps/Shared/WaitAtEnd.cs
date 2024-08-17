using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps.Common;
using Questionable.Controller.Utils;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Shared;

internal static class WaitAtEnd
{
    internal sealed class Factory(
        IServiceProvider serviceProvider,
        IClientState clientState,
        ICondition condition,
        TerritoryData territoryData)
        : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.CompletionQuestVariablesFlags.Count == 6 && QuestWorkUtils.HasCompletionFlags(step.CompletionQuestVariablesFlags))
            {
                var task = serviceProvider.GetRequiredService<WaitForCompletionFlags>()
                    .With((QuestId)quest.Id, step);
                var delay = serviceProvider.GetRequiredService<WaitDelay>();
                return [task, delay, Next(quest, sequence)];
            }

            switch (step.InteractionType)
            {
                case EInteractionType.Combat:
                    var notInCombat =
                        new WaitConditionTask(() => !condition[ConditionFlag.InCombat], "Wait(not in combat)");
                    return
                    [
                        serviceProvider.GetRequiredService<WaitDelay>(),
                        notInCombat,
                        serviceProvider.GetRequiredService<WaitDelay>(),
                        Next(quest, sequence)
                    ];

                case EInteractionType.WaitForManualProgress:
                case EInteractionType.Instruction:
                    return [serviceProvider.GetRequiredService<WaitNextStepOrSequence>()];

                case EInteractionType.Duty:
                case EInteractionType.SinglePlayerDuty:
                    return [new EndAutomation()];

                case EInteractionType.WalkTo:
                case EInteractionType.Jump:
                    // no need to wait if we're just moving around
                    return [Next(quest, sequence)];

                case EInteractionType.WaitForObjectAtPosition:
                    ArgumentNullException.ThrowIfNull(step.DataId);
                    ArgumentNullException.ThrowIfNull(step.Position);

                    return
                    [
                        serviceProvider.GetRequiredService<WaitObjectAtPosition>()
                            .With(step.DataId.Value, step.Position.Value, step.NpcWaitDistance ?? 0.05f),
                        serviceProvider.GetRequiredService<WaitDelay>(),
                        Next(quest, sequence)
                    ];

                case EInteractionType.Interact when step.TargetTerritoryId != null:
                case EInteractionType.UseItem when step.TargetTerritoryId != null:
                    ITask waitInteraction;
                    if (step.TerritoryId != step.TargetTerritoryId)
                    {
                        // interaction moves to a different territory
                        waitInteraction = new WaitConditionTask(
                            () => clientState.TerritoryType == step.TargetTerritoryId,
                            $"Wait(tp to territory: {territoryData.GetNameAndId(step.TargetTerritoryId.Value)})");
                    }
                    else
                    {
                        Vector3 lastPosition = step.Position ?? clientState.LocalPlayer?.Position ?? Vector3.Zero;
                        waitInteraction = new WaitConditionTask(() =>
                            {
                                Vector3? currentPosition = clientState.LocalPlayer?.Position;
                                if (currentPosition == null)
                                    return false;

                                // interaction moved to elsewhere in the zone
                                // the 'closest' locations are probably
                                //   - waking sands' solar
                                //   - rising stones' solar + dawn's respite
                                return (lastPosition - currentPosition.Value).Length() > 2;
                            }, $"Wait(tp away from {lastPosition.ToString("G", CultureInfo.InvariantCulture)})");
                    }

                    return
                    [
                        waitInteraction,
                        serviceProvider.GetRequiredService<WaitDelay>(),
                        Next(quest, sequence)
                    ];

                case EInteractionType.AcceptQuest:
                {
                    var accept = serviceProvider.GetRequiredService<WaitQuestAccepted>()
                        .With(step.PickUpQuestId ?? quest.Id);
                    var delay = serviceProvider.GetRequiredService<WaitDelay>();
                    if (step.PickUpQuestId != null)
                        return [accept, delay, Next(quest, sequence)];
                    else
                        return [accept, delay];
                }

                case EInteractionType.CompleteQuest:
                {
                    var complete = serviceProvider.GetRequiredService<WaitQuestCompleted>()
                        .With(step.TurnInQuestId ?? quest.Id);
                    var delay = serviceProvider.GetRequiredService<WaitDelay>();
                    if (step.TurnInQuestId != null)
                        return [complete, delay, Next(quest, sequence)];
                    else
                        return [complete, delay];
                }

                case EInteractionType.Interact:
                default:
                    return [serviceProvider.GetRequiredService<WaitDelay>(), Next(quest, sequence)];
            }
        }

        private static NextStep Next(Quest quest, QuestSequence sequence)
        {
            return new NextStep(quest.Id, sequence.Sequence);
        }
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

    internal sealed class WaitForCompletionFlags(QuestFunctions questFunctions) : ITask
    {
        public QuestId Quest { get; set; } = null!;
        public QuestStep Step { get; set; } = null!;
        public IList<QuestWorkValue?> Flags { get; set; } = null!;

        public ITask With(QuestId quest, QuestStep step)
        {
            Quest = quest;
            Step = step;
            Flags = step.CompletionQuestVariablesFlags;
            return this;
        }

        public bool Start() => true;

        public ETaskResult Update()
        {
            QuestProgressInfo? questWork = questFunctions.GetQuestProgressInfo(Quest);
            return questWork != null &&
                   QuestWorkUtils.MatchesQuestWork(Step.CompletionQuestVariablesFlags, questWork)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;
        }

        public override string ToString() =>
            $"Wait(QW: {string.Join(", ", Flags.Select(x => x?.ToString() ?? "-"))})";
    }

    internal sealed class WaitObjectAtPosition(GameFunctions gameFunctions) : ITask
    {
        public uint DataId { get; set; }
        public Vector3 Destination { get; set; }
        public float Distance { get; set; }

        public ITask With(uint dataId, Vector3 destination, float distance)
        {
            DataId = dataId;
            Destination = destination;
            Distance = distance;
            return this;
        }

        public bool Start() => true;

        public ETaskResult Update() =>
            gameFunctions.IsObjectAtPosition(DataId, Destination, Distance)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;

        public override string ToString() =>
            $"WaitObj({DataId} at {Destination.ToString("G", CultureInfo.InvariantCulture)} < {Distance})";
    }

    internal sealed class WaitQuestAccepted(QuestFunctions questFunctions) : ITask
    {
        public ElementId ElementId { get; set; } = null!;

        public ITask With(ElementId elementId)
        {
            ElementId = elementId;
            return this;
        }

        public bool Start() => true;

        public ETaskResult Update()
        {
            return questFunctions.IsQuestAccepted(ElementId)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;
        }

        public override string ToString() => $"WaitQuestAccepted({ElementId})";
    }

    internal sealed class WaitQuestCompleted(QuestFunctions questFunctions) : ITask
    {
        public ElementId ElementId { get; set; } = null!;

        public ITask With(ElementId elementId)
        {
            ElementId = elementId;
            return this;
        }

        public bool Start() => true;

        public ETaskResult Update()
        {
            return questFunctions.IsQuestComplete(ElementId) ? ETaskResult.TaskComplete : ETaskResult.StillRunning;
        }

        public override string ToString() => $"WaitQuestComplete({ElementId})";
    }

    internal sealed class NextStep(ElementId elementId, int sequence) : ILastTask
    {
        public ElementId ElementId { get; } = elementId;
        public int Sequence { get; } = sequence;

        public bool Start() => true;

        public ETaskResult Update() => ETaskResult.NextStep;

        public override string ToString() => "NextStep";
    }

    internal sealed class EndAutomation : ILastTask
    {
        public ElementId ElementId => throw new InvalidOperationException();
        public int Sequence => throw new InvalidOperationException();

        public bool Start() => true;

        public ETaskResult Update() => ETaskResult.End;

        public override string ToString() => "EndAutomation";
    }
}
