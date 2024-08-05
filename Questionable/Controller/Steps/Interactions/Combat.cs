﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps.Common;
using Questionable.Controller.Steps.Shared;
using Questionable.Controller.Utils;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class Combat
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Combat)
                yield break;

            ArgumentNullException.ThrowIfNull(step.EnemySpawnType);

            yield return serviceProvider.GetRequiredService<UnmountTask>();

            if (step.CombatDelaySecondsAtStart != null)
            {
                yield return serviceProvider.GetRequiredService<WaitAtStart.WaitDelay>()
                    .With(TimeSpan.FromSeconds(step.CombatDelaySecondsAtStart.Value));
            }

            switch (step.EnemySpawnType)
            {
                case EEnemySpawnType.AfterInteraction:
                {
                    ArgumentNullException.ThrowIfNull(step.DataId);

                    yield return serviceProvider.GetRequiredService<Interact.DoInteract>()
                        .With(step.DataId.Value, true);
                    yield return CreateTask(quest, sequence, step);
                    break;
                }

                case EEnemySpawnType.AfterItemUse:
                {
                    ArgumentNullException.ThrowIfNull(step.DataId);
                    ArgumentNullException.ThrowIfNull(step.ItemId);

                    yield return serviceProvider.GetRequiredService<UseItem.UseOnObject>()
                        .With(quest.Id, step.DataId.Value, step.ItemId.Value, step.CompletionQuestVariablesFlags,
                            true);
                    yield return CreateTask(quest, sequence, step);
                    break;
                }

                case EEnemySpawnType.AutoOnEnterArea:
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
            return serviceProvider.GetRequiredService<HandleCombat>()
                .With(quest.Id, isLastStep, step.EnemySpawnType.Value, step.KillEnemyDataIds,
                    step.CompletionQuestVariablesFlags, step.ComplexCombatData);
        }
    }

    internal sealed class HandleCombat(CombatController combatController, QuestFunctions questFunctions) : ITask
    {
        private bool _isLastStep;
        private CombatController.CombatData _combatData = null!;
        private IList<QuestWorkValue?> _completionQuestVariableFlags = null!;

        public ITask With(ElementId elementId, bool isLastStep, EEnemySpawnType enemySpawnType, IList<uint> killEnemyDataIds,
            IList<QuestWorkValue?> completionQuestVariablesFlags, IList<ComplexCombatData> complexCombatData)
        {
            _isLastStep = isLastStep;
            _combatData = new CombatController.CombatData
            {
                ElementId = elementId,
                SpawnType = enemySpawnType,
                KillEnemyDataIds = killEnemyDataIds.ToList(),
                ComplexCombatDatas = complexCombatData.ToList(),
            };
            _completionQuestVariableFlags = completionQuestVariablesFlags;
            return this;
        }

        public bool Start() => combatController.Start(_combatData);

        public ETaskResult Update()
        {
            if (combatController.Update() != CombatController.EStatus.Complete)
                return ETaskResult.StillRunning;

            // if our quest step has any completion flags, we need to check if they are set
            if (QuestWorkUtils.HasCompletionFlags(_completionQuestVariableFlags) && _combatData.ElementId is QuestId questId)
            {
                var questWork = questFunctions.GetQuestEx(questId);
                if (questWork == null)
                    return ETaskResult.StillRunning;

                if (QuestWorkUtils.MatchesQuestWork(_completionQuestVariableFlags, questWork.Value))
                    return ETaskResult.TaskComplete;
                else
                    return ETaskResult.StillRunning;
            }

            // the last step, by definition, can only be progressed by the game recognizing we're in a new sequence,
            // so this is an indefinite wait
            if (_isLastStep)
                return ETaskResult.StillRunning;
            else
            {
                combatController.Stop("Combat task complete");
                return ETaskResult.TaskComplete;
            }
        }

        public override string ToString()
        {
            if (QuestWorkUtils.HasCompletionFlags(_completionQuestVariableFlags))
                return "HandleCombat(wait: QW flags)";
            else if (_isLastStep)
                return "HandleCombat(wait: next sequence)";
            else
                return "HandleCombat(wait: not in combat)";
        }
    }
}
