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
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Common.Math;
using Microsoft.Extensions.Logging;
using Questionable.Controller.CombatModules;
using Questionable.Controller.Utils;
using Questionable.Functions;
using Questionable.Model.Questing;

namespace Questionable.Controller;

internal sealed class CombatController : IDisposable
{
    private readonly List<ICombatModule> _combatModules;
    private readonly MovementController _movementController;
    private readonly ITargetManager _targetManager;
    private readonly IObjectTable _objectTable;
    private readonly ICondition _condition;
    private readonly IClientState _clientState;
    private readonly QuestFunctions _questFunctions;
    private readonly ILogger<CombatController> _logger;

    private CurrentFight? _currentFight;
    private bool _wasInCombat;

    public CombatController(
        IEnumerable<ICombatModule> combatModules,
        MovementController movementController,
        ITargetManager targetManager,
        IObjectTable objectTable,
        ICondition condition,
        IClientState clientState,
        QuestFunctions questFunctions,
        ILogger<CombatController> logger)
    {
        _combatModules = combatModules.ToList();
        _movementController = movementController;
        _targetManager = targetManager;
        _objectTable = objectTable;
        _condition = condition;
        _clientState = clientState;
        _questFunctions = questFunctions;
        _logger = logger;

        _clientState.TerritoryChanged += TerritoryChanged;
    }

    public bool IsRunning => _currentFight != null;

    public bool Start(CombatData combatData)
    {
        Stop("Starting combat");

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

    public EStatus Update()
    {
        if (_currentFight == null)
            return EStatus.Complete;

        if (_movementController.IsPathfinding || _movementController.IsPathRunning)
            return EStatus.Moving;

        var target = _targetManager.Target;
        if (target != null)
        {
            int currentTargetPriority = GetKillPriority(target);
            var nextTarget = FindNextTarget();
            int nextTargetPriority = GetKillPriority(target);

            if (nextTarget != null && nextTarget.Equals(target))
            {
                _currentFight.Module.Update(target);
            }
            else if (nextTarget != null)
            {
                if (nextTargetPriority > currentTargetPriority)
                {
                    _logger.LogInformation("Changing next target to {TargetName} ({TargetId:X8})",
                        nextTarget.Name.ToString(), nextTarget.GameObjectId);
                    _targetManager.Target = nextTarget;
                    _currentFight.Module.SetTarget(nextTarget);
                }
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
            if (nextTarget is { IsDead: false })
            {
                _logger.LogInformation("Setting next target to {TargetName} ({TargetId:X8})",
                    nextTarget.Name.ToString(), nextTarget.GameObjectId);
                _targetManager.Target = nextTarget;
                _currentFight.Module.SetTarget(nextTarget);
            }
        }

        if (_condition[ConditionFlag.InCombat])
        {
            _wasInCombat = true;
            return EStatus.InCombat;
        }
        else if (_wasInCombat)
            return EStatus.Complete;
        else
            return EStatus.InCombat;
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

                if (QuestWorkUtils.HasCompletionFlags(condition.CompletionQuestVariablesFlags) &&
                    _currentFight.Data.ElementId is QuestId questId)
                {
                    var questWork = _questFunctions.GetQuestProgressInfo(questId);
                    if (questWork != null &&
                        QuestWorkUtils.MatchesQuestWork(condition.CompletionQuestVariablesFlags, questWork))
                    {
                        _logger.LogInformation("Complex combat condition fulfilled: QuestWork matches");
                        _currentFight.Data.CompletedComplexDatas.Add(i);
                        continue;
                    }
                }
            }
        }

        return _objectTable.Select(x => new
            {
                GameObject = x,
                Priority = GetKillPriority(x),
                Distance = Vector3.Distance(x.Position, _clientState.LocalPlayer!.Position),
            })
            .Where(x => x.Priority > 0)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Distance)
            .Select(x => x.GameObject)
            .FirstOrDefault();
    }

    private unsafe int GetKillPriority(IGameObject gameObject)
    {
        if (gameObject is IBattleNpc battleNpc)
        {
            // TODO this works as somewhat of a delay between killing enemies if certain items/flags are checked
            // but also delays killing the next enemy a little
            if (_currentFight == null || _currentFight.Data.SpawnType != EEnemySpawnType.OverworldEnemies ||
                _currentFight.Data.ComplexCombatDatas.Count == 0)
            {
                if (battleNpc.IsDead)
                    return 0;
            }

            if (!battleNpc.IsTargetable)
                return 0;

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

                // npc that starts a fate or does turn-ins; not sure why they're marked as hostile
                if (gameObjectStruct->NamePlateIconId is 60093 or 60732)
                    return 0;

                var enemyData =
                    _currentFight?.Data.ComplexCombatDatas.FirstOrDefault(x => x.DataId == battleNpc.DataId);
                if (enemyData is { IgnoreQuestMarker: true })
                {
                    if (battleNpc.StatusFlags.HasFlag(StatusFlags.InCombat))
                        return 20;
                }
                else if (enemyData != null)
                {
                    if (gameObjectStruct->NamePlateIconId != 0)
                        return 30;

                    // for enemies that are very far away, their nameplate doesn't render but they're in the object table
                    if (_currentFight?.Data.SpawnType == EEnemySpawnType.OverworldEnemies &&
                        Vector3.Distance(_clientState.LocalPlayer?.Position ?? Vector3.Zero, battleNpc.Position) > 50)
                        return 25;
                }
                else
                {
                    // as part of KillEnemyDataIds, not ComplexCombatData
                    // TODO maybe remove KillEnemyDataIds, rename ComplexCombatData to CombatData
                    if (gameObjectStruct->NamePlateIconId != 0)
                        return 29;
                }
            }

            // stuff trying to kill us
            if (battleNpc.TargetObjectId == _clientState.LocalPlayer?.GameObjectId)
                return 10;

            // stuff on our enmity list that's not necessarily targeting us
            var haters = UIState.Instance()->Hater;
            for (int i = 0; i < haters.HaterCount; ++i)
            {
                var hater = haters.Haters[i];
                if (hater.EntityId == battleNpc.GameObjectId)
                    return 5;
            }

            return 0;
        }
        else
            return 0;
    }

    public void Stop(string label)
    {
        using var scope = _logger.BeginScope(label);
        if (_currentFight != null)
        {
            _logger.LogInformation("Stopping current fight");
            _currentFight.Module.Stop();
        }

        _currentFight = null;
        _wasInCombat = false;
    }

    private void TerritoryChanged(ushort territoryId) => Stop("TerritoryChanged");

    public void Dispose()
    {
        _clientState.TerritoryChanged -= TerritoryChanged;
        Stop("Dispose");
    }

    private sealed class CurrentFight
    {
        public required ICombatModule Module { get; init; }
        public required CombatData Data { get; init; }
    }

    public sealed class CombatData
    {
        public required ElementId ElementId { get; init; }
        public required EEnemySpawnType SpawnType { get; init; }
        public required List<uint> KillEnemyDataIds { get; init; }
        public required List<ComplexCombatData> ComplexCombatDatas { get; init; }

        public HashSet<int> CompletedComplexDatas { get; } = new();
    }

    public enum EStatus
    {
        InCombat,
        Moving,
        Complete,
    }
}
