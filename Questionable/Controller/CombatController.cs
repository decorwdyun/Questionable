using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Common.Math;
using Microsoft.Extensions.Logging;
using Questionable.Controller.CombatModules;
using Questionable.Controller.Utils;
using Questionable.Model.V1;

namespace Questionable.Controller;

internal sealed class CombatController : IDisposable
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

        _clientState.TerritoryChanged += TerritoryChanged;
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
            if (GetKillPriority(target) is >= 50)
                return true;

            var nextTarget = FindNextTarget();
            if (nextTarget != null && nextTarget.Equals(target))
            {
                _currentFight.Module.Update(target);
            }
            else if (nextTarget != null)
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

    [SuppressMessage("ReSharper", "RedundantJumpStatement")]
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

        return _objectTable.Select(x => (GameObject: x, Priority: GetKillPriority(x)))
            .Where(x => x.Priority != null)
            .OrderByDescending(x => x.Priority!.Value)
            .ThenByDescending(x => Vector3.Distance(x.GameObject.Position, _clientState.LocalPlayer!.Position))
            .Select(x => x.GameObject)
            .FirstOrDefault();
    }

    private unsafe int? GetKillPriority(IGameObject gameObject)
    {
        if (gameObject is IBattleNpc battleNpc)
        {
            // TODO this works as somewhat of a delay between killing enemies if certain items/flags are checked
            // but also delays killing the next enemy a little
            if (_currentFight == null || _currentFight.Data.SpawnType != EEnemySpawnType.OverworldEnemies ||
                _currentFight.Data.ComplexCombatDatas.Count == 0)
            {
                if (battleNpc.IsDead)
                    return null;
            }

            if (!battleNpc.IsTargetable)
                return null;

            if (_currentFight != null)
            {
                var complexCombatData = _currentFight.Data.ComplexCombatDatas;
                if (complexCombatData.Count >= 0)
                {
                    for (int i = 0; i < complexCombatData.Count; ++i)
                    {
                        if (_currentFight.Data.CompletedComplexDatas.Contains(i))
                            continue;

                        if (complexCombatData[i].DataId == battleNpc.DataId)
                            return 100;
                    }
                }
                else
                {
                    if (_currentFight.Data.KillEnemyDataIds.Contains(battleNpc.DataId))
                        return 90;
                }
            }

            // enemies that we have aggro on
            if (battleNpc.BattleNpcKind is BattleNpcSubKind.BattleNpcPart or BattleNpcSubKind.Enemy)
            {
                var gameObjectStruct = (GameObject*)gameObject.Address;
                if (gameObjectStruct->NamePlateIconId is 60093 or 60732) // npc that starts a fate or does turn-ins
                    return null;

                var enemyData = _currentFight?.Data.ComplexCombatDatas.FirstOrDefault(x => x.DataId == battleNpc.DataId);
                if (enemyData is { IgnoreQuestMarker: true })
                    return battleNpc.StatusFlags.HasFlag(StatusFlags.InCombat) ? 20 : null;
                else
                    return gameObjectStruct->NamePlateIconId != 0 ? 30 : null;
            }

            // stuff trying to kill us
            if (battleNpc.TargetObjectId == _clientState.LocalPlayer?.GameObjectId)
                return 0;

        }

        return null;
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

    private void TerritoryChanged(ushort territoryId) => Stop();

    public void Dispose()
    {
        _clientState.TerritoryChanged -= TerritoryChanged;
        Stop();
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
