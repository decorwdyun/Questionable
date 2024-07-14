using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps.Common;
using Questionable.Controller.Utils;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.Interactions;

internal static class Combat
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Combat)
                return [];

            ArgumentNullException.ThrowIfNull(step.EnemySpawnType);

            var unmount = serviceProvider.GetRequiredService<UnmountTask>();
            switch (step.EnemySpawnType)
            {
                case EEnemySpawnType.AfterInteraction:
                {
                    ArgumentNullException.ThrowIfNull(step.DataId);

                    var interaction = serviceProvider.GetRequiredService<Interact.DoInteract>()
                        .With(step.DataId.Value, true);
                    return [unmount, interaction, CreateTask(quest, sequence, step)];
                }

                case EEnemySpawnType.AfterItemUse:
                {
                    ArgumentNullException.ThrowIfNull(step.DataId);
                    ArgumentNullException.ThrowIfNull(step.ItemId);

                    var useItem = serviceProvider.GetRequiredService<UseItem.UseOnObject>()
                        .With(step.DataId.Value, step.ItemId.Value);
                    return [unmount, useItem, CreateTask(quest, sequence, step)];
                }

                case EEnemySpawnType.AutoOnEnterArea:
                    // automatically triggered when entering area, i.e. only unmount
                    return [unmount, CreateTask(quest, sequence, step)];

                case EEnemySpawnType.OverworldEnemies:
                    return [unmount, CreateTask(quest, sequence, step)];

                default:
                    throw new ArgumentOutOfRangeException(nameof(step), $"Unknown spawn type {step.EnemySpawnType}");
            }
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            ArgumentNullException.ThrowIfNull(step.EnemySpawnType);

            bool isLastStep = sequence.Steps.Last() == step;
            return serviceProvider.GetRequiredService<HandleCombat>()
                .With(quest.QuestId, isLastStep, step.EnemySpawnType.Value, step.KillEnemyDataIds,
                    step.CompletionQuestVariablesFlags, step.ComplexCombatData);
        }
    }

    internal sealed class HandleCombat(CombatController combatController, GameFunctions gameFunctions) : ITask
    {
        private bool _isLastStep;
        private CombatController.CombatData _combatData = null!;
        private IList<short?> _completionQuestVariableFlags = null!;

        public ITask With(ushort questId, bool isLastStep, EEnemySpawnType enemySpawnType, IList<uint> killEnemyDataIds,
            IList<short?> completionQuestVariablesFlags, IList<ComplexCombatData> complexCombatData)
        {
            _isLastStep = isLastStep;
            _combatData = new CombatController.CombatData
            {
                QuestId = questId,
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
            if (combatController.Update())
                return ETaskResult.StillRunning;

            // if our quest step has any completion flags, we need to check if they are set
            if (QuestWorkUtils.HasCompletionFlags(_completionQuestVariableFlags))
            {
                var questWork = gameFunctions.GetQuestEx(_combatData.QuestId);
                if (questWork == null)
                    return ETaskResult.StillRunning;

                if (!QuestWorkUtils.MatchesQuestWork(_completionQuestVariableFlags, questWork.Value, false))
                    return ETaskResult.StillRunning;
            }

            // the last step, by definition, can only be progressed by the game recognizing we're in a new sequence,
            // so this is an indefinite wait
            if (_isLastStep)
                return ETaskResult.StillRunning;
            else
            {
                combatController.Stop();
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
