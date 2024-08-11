using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameData;
using LLib.GameUI;
using Microsoft.Extensions.Logging;
using Questionable.Functions;
using Questionable.Model.Gathering;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Gathering;

internal sealed class DoGatherCollectable(
    GatheringController gatheringController,
    GameFunctions gameFunctions,
    IClientState clientState,
    IGameGui gameGui,
    ILogger<DoGatherCollectable> logger) : ITask, IRevisitAware
{
    private GatheringController.GatheringRequest _currentRequest = null!;
    private GatheringNode _currentNode = null!;
    private bool _revisitRequired;
    private bool _revisitTriggered;
    private Queue<EAction>? _actionQueue;

    public ITask With(GatheringController.GatheringRequest currentRequest, GatheringNode currentNode, bool revisitRequired)
    {
        _currentRequest = currentRequest;
        _currentNode = currentNode;
        _revisitRequired = revisitRequired;
        return this;
    }

    public bool Start() => true;

    public unsafe ETaskResult Update()
    {
        if (_revisitRequired && !_revisitTriggered)
        {
            logger.LogInformation("No revisit");
            return ETaskResult.TaskComplete;
        }

        if (gatheringController.HasNodeDisappeared(_currentNode))
        {
            logger.LogInformation("Node disappeared");
            return ETaskResult.TaskComplete;
        }

        if (gatheringController.HasRequestedItems())
        {
            if (gameGui.TryGetAddonByName("GatheringMasterpiece", out AtkUnitBase* atkUnitBase))
            {
                atkUnitBase->FireCallbackInt(1);
                return ETaskResult.StillRunning;
            }

            if (gameGui.TryGetAddonByName("Gathering", out atkUnitBase))
            {
                atkUnitBase->FireCallbackInt(-1);
                return ETaskResult.TaskComplete;
            }
        }

        if (gameFunctions.GetFreeInventorySlots() == 0)
            throw new TaskException("Inventory full");

        NodeCondition? nodeCondition = GetNodeCondition();
        if (nodeCondition == null)
            return ETaskResult.TaskComplete;

        if (_actionQueue != null && _actionQueue.TryPeek(out EAction nextAction))
        {
            if (gameFunctions.UseAction(nextAction))
            {
                logger.LogInformation("Used action {Action} on node", nextAction);
                _actionQueue.Dequeue();
            }

            return ETaskResult.StillRunning;
        }

        if (nodeCondition.CollectabilityToGoal(_currentRequest.Collectability) > 0)
        {
            _actionQueue = GetNextActions(nodeCondition);
            if (_actionQueue != null)
            {
                foreach (var action in _actionQueue)
                    logger.LogInformation("Next Actions {Action}", action);
                return ETaskResult.StillRunning;
            }
        }

        _actionQueue = new Queue<EAction>();
        _actionQueue.Enqueue(PickAction(EAction.CollectMiner, EAction.CollectBotanist));
        return ETaskResult.StillRunning;
    }

    private unsafe NodeCondition? GetNodeCondition()
    {
        if (gameGui.TryGetAddonByName("GatheringMasterpiece", out AtkUnitBase* atkUnitBase))
        {
            var atkValues = atkUnitBase->AtkValues;
            return new NodeCondition(
                CurrentCollectability: atkValues[13].UInt,
                MaxCollectability: atkValues[14].UInt,
                CurrentIntegrity: atkValues[62].UInt,
                MaxIntegrity: atkValues[63].UInt,
                ScrutinyActive: atkValues[80].Bool,
                CollectabilityFromScour: atkValues[48].UInt,
                CollectabilityFromMeticulous: atkValues[51].UInt
            );
        }

        return null;
    }

    private Queue<EAction> GetNextActions(NodeCondition nodeCondition)
    {
        uint gp = clientState.LocalPlayer!.CurrentGp;
        Queue<EAction> actions = new();

        uint neededCollectability = nodeCondition.CollectabilityToGoal(_currentRequest.Collectability);
        if (neededCollectability <= nodeCondition.CollectabilityFromMeticulous)
        {
            actions.Enqueue(PickAction(EAction.MeticulousMiner, EAction.MeticulousBotanist));
            return actions;
        }

        if (neededCollectability <= nodeCondition.CollectabilityFromScour)
        {
            actions.Enqueue(PickAction(EAction.ScourMiner, EAction.ScourBotanist));
            return actions;
        }

        // neither action directly solves our problem
        if (!nodeCondition.ScrutinyActive && gp >= 200)
        {
            actions.Enqueue(PickAction(EAction.ScrutinyMiner, EAction.ScrutinyBotanist));
            return actions;
        }

        if (nodeCondition.ScrutinyActive)
        {
            actions.Enqueue(PickAction(EAction.MeticulousMiner, EAction.MeticulousBotanist));
            return actions;
        }
        else
        {
            actions.Enqueue(PickAction(EAction.ScourMiner, EAction.ScourBotanist));
            return actions;
        }
    }

    private EAction PickAction(EAction minerAction, EAction botanistAction)
    {
        if ((EClassJob?)clientState.LocalPlayer?.ClassJob.Id == EClassJob.Miner)
            return minerAction;
        else
            return botanistAction;
    }

    public void OnRevisit()
    {
        _revisitTriggered = true;
    }

    public override string ToString() =>
        $"DoGatherCollectable({SeIconChar.Collectible.ToIconString()} {_currentRequest.Collectability}){(_revisitRequired ? " if revist" : "")}";

    [SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Local")]
    private sealed record NodeCondition(
        uint CurrentCollectability,
        uint MaxCollectability,
        uint CurrentIntegrity,
        uint MaxIntegrity,
        bool ScrutinyActive,
        uint CollectabilityFromScour,
        uint CollectabilityFromMeticulous)
    {
        public uint CollectabilityToGoal(uint goal)
        {
            if (goal >= CurrentCollectability)
                return goal - CurrentCollectability;
            return CurrentCollectability == 0 ? 1u : 0u;
        }
    }
}
