using System;
using System.Collections.Generic;
using System.Linq;
using Questionable.Controller.CombatModules;
using Questionable.Controller.Steps.Common;
using Questionable.Controller.Steps.Shared;
using Questionable.Controller.Utils;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class Combat
{
    internal sealed class Factory(
        CombatController combatController,
        Interact.Factory interactFactory,
        Mount.Factory mountFactory,
        UseItem.Factory useItemFactory,
        Action.Factory actionFactory,
        QuestFunctions questFunctions,
        GameFunctions gameFunctions) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Combat)
                yield break;

            ArgumentNullException.ThrowIfNull(step.EnemySpawnType);

            if (gameFunctions.GetMountId() != Mount128Module.MountId)
                yield return mountFactory.Unmount();

            if (step.CombatDelaySecondsAtStart != null)
            {
                yield return new WaitAtStart.WaitDelay(TimeSpan.FromSeconds(step.CombatDelaySecondsAtStart.Value));
            }

            switch (step.EnemySpawnType)
            {
                case EEnemySpawnType.AfterInteraction:
                {
                    ArgumentNullException.ThrowIfNull(step.DataId);

                    yield return interactFactory.Interact(step.DataId.Value, quest, EInteractionType.None, true);
                    yield return new WaitAtEnd.WaitDelay(TimeSpan.FromSeconds(1));
                    yield return CreateTask(quest, sequence, step);
                    break;
                }

                case EEnemySpawnType.AfterItemUse:
                {
                    ArgumentNullException.ThrowIfNull(step.DataId);
                    ArgumentNullException.ThrowIfNull(step.ItemId);

                    yield return useItemFactory.OnObject(quest.Id, step.DataId.Value, step.ItemId.Value,
                        step.CompletionQuestVariablesFlags, true);
                    yield return new WaitAtEnd.WaitDelay(TimeSpan.FromSeconds(1));
                    yield return CreateTask(quest, sequence, step);
                    break;
                }

                case EEnemySpawnType.AfterAction:
                {
                    ArgumentNullException.ThrowIfNull(step.DataId);
                    ArgumentNullException.ThrowIfNull(step.Action);

                    if (!step.Action.Value.RequiresMount())
                        yield return mountFactory.Unmount();
                    yield return actionFactory.OnObject(step.DataId.Value, step.Action.Value);
                    yield return new WaitAtEnd.WaitDelay(TimeSpan.FromSeconds(1));
                    yield return CreateTask(quest, sequence, step);
                    break;
                }

                case EEnemySpawnType.AutoOnEnterArea:
                    if (step.CombatDelaySecondsAtStart == null)
                        yield return new WaitAtEnd.WaitDelay(TimeSpan.FromSeconds(1));

                    // automatically triggered when entering area, i.e. only unmount
                    yield return CreateTask(quest, sequence, step);
                    break;

                case EEnemySpawnType.OverworldEnemies:
                    yield return CreateTask(quest, sequence, step);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(step), $"Unknown spawn type {step.EnemySpawnType}");
            }
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            ArgumentNullException.ThrowIfNull(step.EnemySpawnType);

            bool isLastStep = sequence.Steps.Last() == step;
            return CreateTask(quest.Id, isLastStep, step.EnemySpawnType.Value, step.KillEnemyDataIds,
                step.CompletionQuestVariablesFlags, step.ComplexCombatData);
        }

        private HandleCombat CreateTask(ElementId elementId, bool isLastStep, EEnemySpawnType enemySpawnType,
            IList<uint> killEnemyDataIds, IList<QuestWorkValue?> completionQuestVariablesFlags,
            IList<ComplexCombatData> complexCombatData)
        {
            return new HandleCombat(isLastStep, new CombatController.CombatData
            {
                ElementId = elementId,
                SpawnType = enemySpawnType,
                KillEnemyDataIds = killEnemyDataIds.ToList(),
                ComplexCombatDatas = complexCombatData.ToList(),
            }, completionQuestVariablesFlags, combatController, questFunctions);
        }
    }

    private sealed class HandleCombat(
        bool isLastStep,
        CombatController.CombatData combatData,
        IList<QuestWorkValue?> completionQuestVariableFlags,
        CombatController combatController,
        QuestFunctions questFunctions) : ITask
    {
        public bool Start() => combatController.Start(combatData);

        public ETaskResult Update()
        {
            if (combatController.Update() != CombatController.EStatus.Complete)
                return ETaskResult.StillRunning;

            // if our quest step has any completion flags, we need to check if they are set
            if (QuestWorkUtils.HasCompletionFlags(completionQuestVariableFlags) &&
                combatData.ElementId is QuestId questId)
            {
                var questWork = questFunctions.GetQuestProgressInfo(questId);
                if (questWork == null)
                    return ETaskResult.StillRunning;

                if (QuestWorkUtils.MatchesQuestWork(completionQuestVariableFlags, questWork))
                    return ETaskResult.TaskComplete;
                else
                    return ETaskResult.StillRunning;
            }

            // the last step, by definition, can only be progressed by the game recognizing we're in a new sequence,
            // so this is an indefinite wait
            if (isLastStep)
                return ETaskResult.StillRunning;
            else
            {
                combatController.Stop("Combat task complete");
                return ETaskResult.TaskComplete;
            }
        }

        public override string ToString()
        {
            if (QuestWorkUtils.HasCompletionFlags(completionQuestVariableFlags))
                return "HandleCombat(wait: QW flags)";
            else if (isLastStep)
                return "HandleCombat(wait: next sequence)";
            else
                return "HandleCombat(wait: not in combat)";
        }
    }
}
