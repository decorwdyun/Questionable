using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
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
        IClientState clientState,
        ICondition condition,
        TerritoryData territoryData,
        QuestFunctions questFunctions,
        GameFunctions gameFunctions)
        : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.CompletionQuestVariablesFlags.Count == 6 &&
                QuestWorkUtils.HasCompletionFlags(step.CompletionQuestVariablesFlags))
            {
                var task = new WaitForCompletionFlags((QuestId)quest.Id, step, questFunctions);
                var delay = new WaitDelay();
                return [task, delay, Next(quest, sequence)];
            }

            switch (step.InteractionType)
            {
                case EInteractionType.Combat:
                    var notInCombat =
                        new WaitConditionTask(() => !condition[ConditionFlag.InCombat], "Wait(not in combat)");
                    return
                    [
                        new WaitDelay(),
                        notInCombat,
                        new WaitDelay(),
                        Next(quest, sequence)
                    ];

                case EInteractionType.WaitForManualProgress:
                case EInteractionType.Instruction:
                    return [new WaitNextStepOrSequence()];

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
                        new WaitObjectAtPosition(step.DataId.Value, step.Position.Value, step.NpcWaitDistance ?? 0.05f,
                            gameFunctions),
                        new WaitDelay(),
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
                        new WaitDelay(),
                        Next(quest, sequence)
                    ];

                case EInteractionType.AcceptQuest:
                {
                    var accept = new WaitQuestAccepted(step.PickUpQuestId ?? quest.Id, questFunctions);
                    var delay = new WaitDelay();
                    if (step.PickUpQuestId != null)
                        return [accept, delay, Next(quest, sequence)];
                    else
                        return [accept, delay];
                }

                case EInteractionType.CompleteQuest:
                {
                    var complete = new WaitQuestCompleted(step.TurnInQuestId ?? quest.Id, questFunctions);
                    var delay = new WaitDelay();
                    if (step.TurnInQuestId != null)
                        return [complete, delay, Next(quest, sequence)];
                    else
                        return [complete, delay];
                }

                case EInteractionType.Interact:
                default:
                    return [new WaitDelay(), Next(quest, sequence)];
            }
        }

        private static NextStep Next(Quest quest, QuestSequence sequence)
        {
            return new NextStep(quest.Id, sequence.Sequence);
        }
    }

    internal sealed class WaitDelay(TimeSpan? delay = null) : AbstractDelayedTask(delay ?? TimeSpan.FromSeconds(1))
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

    internal sealed class WaitForCompletionFlags(QuestId quest, QuestStep step, QuestFunctions questFunctions) : ITask
    {
        public bool Start() => true;

        public ETaskResult Update()
        {
            QuestProgressInfo? questWork = questFunctions.GetQuestProgressInfo(quest);
            return questWork != null &&
                   QuestWorkUtils.MatchesQuestWork(step.CompletionQuestVariablesFlags, questWork)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;
        }

        public override string ToString() =>
            $"Wait(QW: {string.Join(", ", step.CompletionQuestVariablesFlags.Select(x => x?.ToString() ?? "-"))})";
    }

    private sealed class WaitObjectAtPosition(
        uint dataId,
        Vector3 destination,
        float distance,
        GameFunctions gameFunctions) : ITask
    {
        public bool Start() => true;

        public ETaskResult Update() =>
            gameFunctions.IsObjectAtPosition(dataId, destination, distance)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;

        public override string ToString() =>
            $"WaitObj({dataId} at {destination.ToString("G", CultureInfo.InvariantCulture)} < {distance})";
    }

    internal sealed class WaitQuestAccepted(ElementId elementId, QuestFunctions questFunctions) : ITask
    {
        public bool Start() => true;

        public ETaskResult Update()
        {
            return questFunctions.IsQuestAccepted(elementId)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;
        }

        public override string ToString() => $"WaitQuestAccepted({elementId})";
    }

    internal sealed class WaitQuestCompleted(ElementId elementId, QuestFunctions questFunctions) : ITask
    {
        public bool Start() => true;

        public ETaskResult Update()
        {
            return questFunctions.IsQuestComplete(elementId) ? ETaskResult.TaskComplete : ETaskResult.StillRunning;
        }

        public override string ToString() => $"WaitQuestComplete({elementId})";
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
