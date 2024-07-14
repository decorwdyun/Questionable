using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Microsoft.Extensions.Logging;
using Questionable.Controller.CombatModules;
using Questionable.Controller.Utils;
using Questionable.Model.V1;

namespace Questionable.Controller;

internal sealed class CombatController
{
    private readonly List<ICombatModule> _combatModules;
    private readonly ITargetManager _targetManager;
    private readonly IObjectTable _objectTable;
    private readonly ICondition _condition;
    private readonly IClientState _clientState;
    private readonly GameFunctions _gameFunctions;
    private readonly ILogger<CombatController> _logger;

    private CurrentFight? _currentFight;

    public CombatController(IEnumerable<ICombatModule> combatModules, ITargetManager targetManager,
        IObjectTable objectTable, ICondition condition, IClientState clientState, GameFunctions gameFunctions,
        ILogger<CombatController> logger)
    {
        _combatModules = combatModules.ToList();
        _targetManager = targetManager;
        _objectTable = objectTable;
        _condition = condition;
        _clientState = clientState;
        _gameFunctions = gameFunctions;
        _logger = logger;
    }

    public bool IsRunning => _currentFight != null;

    public bool Start(CombatData combatData)
    {
        Stop();

        var combatModule = _combatModules.FirstOrDefault(x => x.IsLoaded);
        if (combatModule == null)
            return false;

        if (combatModule.Start())
        {
            _currentFight = new CurrentFight
            {
                Module = combatModule,
                Data = combatData,
            };
            return true;
        }
        else
            return false;
    }

    /// <returns>true if still in combat, false otherwise</returns>
    public bool Update()
    {
        if (_currentFight == null)
            return false;

        var target = _targetManager.Target;
        if (target != null)
        {
            if (IsEnemyToKill(target))
                return true;

            var nextTarget = FindNextTarget();
            if (nextTarget != null)
            {
                _logger.LogInformation("Changing next target to {TargetName} ({TargetId:X8})",
                    nextTarget.Name.ToString(), nextTarget.GameObjectId);
                _targetManager.Target = nextTarget;
                _currentFight.Module.SetTarget(nextTarget);
            }
            else
            {
                _logger.LogInformation("Resetting next target");
                _targetManager.Target = null;
            }
        }
        else
        {
            var nextTarget = FindNextTarget();
            if (nextTarget != null)
            {
                _logger.LogInformation("Setting next target to {TargetName} ({TargetId:X8})",
                    nextTarget.Name.ToString(), nextTarget.GameObjectId);
                _targetManager.Target = nextTarget;
                _currentFight.Module.SetTarget(nextTarget);
            }
        }

        return _condition[ConditionFlag.InCombat];
    }

    private IGameObject? FindNextTarget()
    {
        if (_currentFight == null)
            return null;

        // check if any complex combat conditions are fulfilled
        var complexCombatData = _currentFight.Data.ComplexCombatDatas;
        if (complexCombatData.Count > 0)
        {
            for (int i = 0; i < complexCombatData.Count; ++i)
            {
                if (_currentFight.Data.CompletedComplexDatas.Contains(i))
                    continue;

                var condition = complexCombatData[i];
                if (condition.RewardItemId != null && condition.RewardItemCount != null)
                {
                    unsafe
                    {
                        var inventoryManager = InventoryManager.Instance();
                        if (inventoryManager->GetInventoryItemCount(condition.RewardItemId.Value) >=
                            condition.RewardItemCount.Value)
                        {
                            _logger.LogInformation(
                                "Complex combat condition fulfilled: itemCount({ItemId}) >= {ItemCount}",
                                condition.RewardItemId, condition.RewardItemCount);
                            _currentFight.Data.CompletedComplexDatas.Add(i);
                            continue;
                        }
                    }
                }

                if (QuestWorkUtils.HasCompletionFlags(condition.CompletionQuestVariablesFlags))
                {
                    var questWork = _gameFunctions.GetQuestEx(_currentFight.Data.QuestId);
                    if (questWork != null && QuestWorkUtils.MatchesQuestWork(condition.CompletionQuestVariablesFlags,
                            questWork.Value, false))
                    {
                        _logger.LogInformation("Complex combat condition fulfilled: QuestWork matches");
                        _currentFight.Data.CompletedComplexDatas.Add(i);
                        continue;
                    }
                }
            }
        }

        return _objectTable.Where(IsEnemyToKill).MinBy(x => (x.Position - _clientState.LocalPlayer!.Position).Length());
    }

    private unsafe bool IsEnemyToKill(IGameObject gameObject)
    {
        if (gameObject is IBattleChara battleChara)
        {
            // TODO this works as somewhat of a delay between killing enemies if certain items/flags are checked
            // but also delays killing the next enemy a little
            if (_currentFight == null || _currentFight.Data.SpawnType != EEnemySpawnType.OverworldEnemies ||
                _currentFight.Data.ComplexCombatDatas.Count == 0)
            {
                if (battleChara.IsDead)
                    return false;
            }

            if (!battleChara.IsTargetable)
                return false;

            if (battleChara.TargetObjectId == _clientState.LocalPlayer?.GameObjectId)
                return true;

            if (_currentFight != null)
            {
                var complexCombatData = _currentFight.Data.ComplexCombatDatas;
                if (complexCombatData.Count >= 0)
                {
                    for (int i = 0; i < complexCombatData.Count; ++i)
                    {
                        if (_currentFight.Data.CompletedComplexDatas.Contains(i))
                            continue;

                        if (complexCombatData[i].DataId == battleChara.DataId)
                            return true;
                    }
                }
                else
                {
                    if (_currentFight.Data.KillEnemyDataIds.Contains(battleChara.DataId))
                        return true;
                }
            }

            if (battleChara.StatusFlags.HasFlag(StatusFlags.Hostile))
            {
                var gameObjectStruct = (GameObject*)gameObject.Address;
                return gameObjectStruct->NamePlateIconId != 0;
            }
            else
                return false;
        }
        else
            return false;
    }

    public void Stop()
    {
        if (_currentFight != null)
        {
            _logger.LogInformation("Stopping current fight");
            _currentFight.Module.Stop();
        }

        _currentFight = null;
    }

    private sealed class CurrentFight
    {
        public required ICombatModule Module { get; init; }
        public required CombatData Data { get; init; }
    }

    public sealed class CombatData
    {
        public required ushort QuestId { get; init; }
        public required EEnemySpawnType SpawnType { get; init; }
        public required List<uint> KillEnemyDataIds { get; init; }
        public required List<ComplexCombatData> ComplexCombatDatas { get; init; }

        public HashSet<int> CompletedComplexDatas { get; } = new();
    }
}
