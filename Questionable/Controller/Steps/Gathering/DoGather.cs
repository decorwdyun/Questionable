using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameUI;
using Questionable.Model.Gathering;

namespace Questionable.Controller.Steps.Gathering;

internal sealed class DoGather(
    GatheringController gatheringController,
    IGameGui gameGui,
    ICondition condition) : ITask
{
    private GatheringController.GatheringRequest _currentRequest = null!;
    private GatheringNode _currentNode = null!;
    private bool _wasGathering;
    private List<SlotInfo>? _slots;


    public ITask With(GatheringController.GatheringRequest currentRequest, GatheringNode currentNode)
    {
        _currentRequest = currentRequest;
        _currentNode = currentNode;
        return this;
    }

    public bool Start() => true;

    public unsafe ETaskResult Update()
    {
        if (gatheringController.HasNodeDisappeared(_currentNode))
            return ETaskResult.TaskComplete;

        if (condition[ConditionFlag.Gathering])
        {
            if (gameGui.TryGetAddonByName("GatheringMasterpiece", out AtkUnitBase* _))
                return ETaskResult.TaskComplete;

            _wasGathering = true;

            if (gameGui.TryGetAddonByName("Gathering", out AtkUnitBase* atkUnitBase))
            {
                _slots ??= ReadSlots(atkUnitBase);
                var slot = _slots.Single(x => x.ItemId == _currentRequest.ItemId);
                atkUnitBase->FireCallbackInt(slot.Index);
            }
        }

        return _wasGathering && !condition[ConditionFlag.Gathering]
            ? ETaskResult.TaskComplete
            : ETaskResult.StillRunning;
    }

    private unsafe List<SlotInfo> ReadSlots(AtkUnitBase* atkUnitBase)
    {
        var atkValues = atkUnitBase->AtkValues;
        List<SlotInfo> slots = new List<SlotInfo>();
        for (int i = 0; i < 8; ++i)
        {
            // +8 = new item?
            uint itemId = atkValues[i * 11 + 7].UInt;
            if (itemId == 0)
                continue;

            var slot = new SlotInfo(i, itemId);
            slots.Add(slot);
        }

        return slots;
    }

    public override string ToString() => "DoGather";

    private sealed record SlotInfo(int Index, uint ItemId);
}
