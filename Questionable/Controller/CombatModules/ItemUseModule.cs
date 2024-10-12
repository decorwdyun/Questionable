using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Functions;
using Questionable.Model.Questing;

namespace Questionable.Controller.CombatModules;

internal sealed class ItemUseModule : ICombatModule
{
    private readonly IServiceProvider _serviceProvider;
    private readonly GameFunctions _gameFunctions;
    private readonly ICondition _condition;
    private readonly ILogger<ItemUseModule> _logger;

    private ICombatModule? _delegate;
    private CombatController.CombatData? _combatData;
    private bool _isDoingRotation;

    public ItemUseModule(IServiceProvider serviceProvider, GameFunctions gameFunctions, ICondition condition,
        ILogger<ItemUseModule> logger)
    {
        _serviceProvider = serviceProvider;
        _gameFunctions = gameFunctions;
        _condition = condition;
        _logger = logger;
    }

    public bool CanHandleFight(CombatController.CombatData combatData)
    {
        if (combatData.CombatItemUse == null)
            return false;

        _delegate = _serviceProvider.GetRequiredService<IEnumerable<ICombatModule>>()
            .Where(x => x is not ItemUseModule)
            .FirstOrDefault(x => x.CanHandleFight(combatData));
        _logger.LogInformation("ItemUse delegate: {Delegate}", _delegate?.GetType().Name);
        return _delegate != null;
    }

    public bool Start(CombatController.CombatData combatData)
    {
        if (_delegate!.Start(combatData))
        {
            _combatData = combatData;
            _isDoingRotation = true;
            return true;
        }

        return false;
    }

    public bool Stop()
    {
        if (_isDoingRotation)
        {
            _delegate!.Stop();
            _isDoingRotation = false;
            _combatData = null;
            _delegate = null;
        }

        return true;
    }

    public void Update(IGameObject nextTarget)
    {
        if (_delegate == null)
            return;

        if (_combatData?.CombatItemUse == null)
        {
            _delegate.Update(nextTarget);
            return;
        }

        if (_combatData.KillEnemyDataIds.Contains(nextTarget.DataId) ||
            _combatData.ComplexCombatDatas.Any(x => x.DataId == nextTarget.DataId))
        {
            if (_isDoingRotation)
            {
                unsafe
                {
                    InventoryManager* inventoryManager = InventoryManager.Instance();
                    if (inventoryManager->GetInventoryItemCount(_combatData.CombatItemUse.ItemId) == 0)
                    {
                        _isDoingRotation = false;
                        _delegate.Stop();
                    }
                }

                if (ShouldUseItem(nextTarget))
                {
                    _isDoingRotation = false;
                    _delegate.Stop();
                    _gameFunctions.UseItem(nextTarget.DataId, _combatData.CombatItemUse.ItemId);
                }
                else
                    _delegate.Update(nextTarget);
            }
            else if (_condition[ConditionFlag.Casting])
            {
                // do nothing
            }
            else
            {
                _isDoingRotation = true;
                _delegate.Start(_combatData);
            }
        }
        else if (_isDoingRotation)
        {
            _delegate.Update(nextTarget);
        }
    }

    private unsafe bool ShouldUseItem(IGameObject gameObject)
    {
        if (_combatData?.CombatItemUse == null)
            return false;

        if (gameObject is IBattleChara)
        {
            BattleChara* battleChara = (BattleChara*)gameObject.Address;
            if (_combatData.CombatItemUse.Condition == ECombatItemUseCondition.Incapacitated)
                return (battleChara->Flags2 & 128u) != 0;
        }

        return false;
    }

    public void MoveToTarget(IGameObject nextTarget) => _delegate!.MoveToTarget(nextTarget);

    public bool CanAttack(IBattleNpc target) => _delegate!.CanAttack(target);
}
