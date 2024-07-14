using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Microsoft.Extensions.Logging;
using Questionable.Controller.CombatModules;

namespace Questionable.Controller;

internal sealed class CombatController
{
    private readonly List<ICombatModule> _combatModules;
    private readonly ITargetManager _targetManager;
    private readonly IObjectTable _objectTable;
    private readonly ICondition _condition;
    private readonly IClientState _clientState;
    private readonly ILogger<CombatController> _logger;

    private CurrentFight? _currentFight;

    public CombatController(IEnumerable<ICombatModule> combatModules, ITargetManager targetManager,
        IObjectTable objectTable, ICondition condition, IClientState clientState, ILogger<CombatController> logger)
    {
        _combatModules = combatModules.ToList();
        _targetManager = targetManager;
        _objectTable = objectTable;
        _condition = condition;
        _clientState = clientState;
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
        return _objectTable.Where(IsEnemyToKill).MinBy(x => (x.Position - _clientState.LocalPlayer!.Position).Length());
    }

    private unsafe bool IsEnemyToKill(IGameObject gameObject)
    {
        if (gameObject is IBattleChara battleChara)
        {
            if (battleChara.IsDead)
                return false;

            if (!battleChara.IsTargetable)
                return false;

            if (battleChara.TargetObjectId == _clientState.LocalPlayer?.GameObjectId)
                return true;

            if (_currentFight != null && _currentFight.Data.KillEnemyDataIds.Contains(battleChara.DataId))
                return true;

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
        public required ReadOnlyCollection<uint> KillEnemyDataIds { get; init; }
    }
}
