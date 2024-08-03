using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps;
using Questionable.Controller.Steps.Common;
using Questionable.Controller.Steps.Gathering;
using Questionable.Controller.Steps.Interactions;
using Questionable.Controller.Steps.Shared;
using Questionable.Data;
using Questionable.External;
using Questionable.GatheringPaths;
using Questionable.Model.Gathering;

namespace Questionable.Controller;

internal sealed unsafe class GatheringController : MiniTaskController<GatheringController>
{
    private readonly MovementController _movementController;
    private readonly GatheringData _gatheringData;
    private readonly GameFunctions _gameFunctions;
    private readonly NavmeshIpc _navmeshIpc;
    private readonly IObjectTable _objectTable;
    private readonly IServiceProvider _serviceProvider;

    private CurrentRequest? _currentRequest;

    public GatheringController(MovementController movementController, GatheringData gatheringData,
        GameFunctions gameFunctions, NavmeshIpc navmeshIpc, IObjectTable objectTable, IChatGui chatGui,
        ILogger<GatheringController> logger, IServiceProvider serviceProvider)
        : base(chatGui, logger)
    {
        _movementController = movementController;
        _gatheringData = gatheringData;
        _gameFunctions = gameFunctions;
        _navmeshIpc = navmeshIpc;
        _objectTable = objectTable;
        _serviceProvider = serviceProvider;
    }

    public bool Start(GatheringRequest gatheringRequest)
    {
        if (!AssemblyGatheringLocationLoader.GetLocations()
                .TryGetValue(gatheringRequest.GatheringPointId, out GatheringRoot? gatheringRoot))
        {
            _logger.LogError("Unable to resolve gathering point, no path found for {ItemId} / point {PointId}",
                gatheringRequest.ItemId, gatheringRequest.GatheringPointId);
            return false;
        }

        _currentRequest = new CurrentRequest
        {
            Data = gatheringRequest,
            Root = gatheringRoot,
            Nodes = gatheringRoot.Groups
                .SelectMany(x => x.Nodes)
                .ToList(),
        };

        if (HasRequestedItems())
        {
            _currentRequest = null;
            return false;
        }

        return true;
    }

    public EStatus Update()
    {
        if (_currentRequest == null)
            return EStatus.Complete;

        if (_movementController.IsPathfinding || _movementController.IsPathfinding)
            return EStatus.Moving;

        if (HasRequestedItems())
            return EStatus.Complete;

        if (_currentTask == null && _taskQueue.Count == 0)
            GoToNextNode();

        UpdateCurrentTask();
        return EStatus.Gathering;
    }

    protected override void OnTaskComplete(ITask task) => GoToNextNode();

    public override void Stop(string label)
    {
        _currentRequest = null;
        _currentTask = null;
        _taskQueue.Clear();
    }

    private void GoToNextNode()
    {
        if (_currentRequest == null)
            return;

        if (_taskQueue.Count > 0)
            return;

        var currentNode = _currentRequest.Nodes[_currentRequest.CurrentIndex++ % _currentRequest.Nodes.Count];

        _taskQueue.Enqueue(_serviceProvider.GetRequiredService<MountTask>()
            .With(_currentRequest.Root.TerritoryId, MountTask.EMountIf.Always));
        if (currentNode.Locations.Count > 1)
        {
            Vector3 averagePosition = new Vector3
            {
                X = currentNode.Locations.Sum(x => x.Position.X) / currentNode.Locations.Count,
                Y = currentNode.Locations.Select(x => x.Position.Y).Max() + 5f,
                Z = currentNode.Locations.Sum(x => x.Position.Z) / currentNode.Locations.Count,
            };
            Vector3? pointOnFloor = _navmeshIpc.GetPointOnFloor(averagePosition);
            if (pointOnFloor != null)
                pointOnFloor = pointOnFloor.Value with { Y = pointOnFloor.Value.Y + 3f };

            _taskQueue.Enqueue(_serviceProvider.GetRequiredService<Move.MoveInternal>()
                .With(_currentRequest.Root.TerritoryId, pointOnFloor ?? averagePosition, 50f, fly: true,
                    ignoreDistanceToObject: true));
        }

        _taskQueue.Enqueue(_serviceProvider.GetRequiredService<MoveToLandingLocation>()
            .With(_currentRequest.Root.TerritoryId, currentNode));
        _taskQueue.Enqueue(_serviceProvider.GetRequiredService<Interact.DoInteract>()
            .With(currentNode.DataId, true));
        _taskQueue.Enqueue(_serviceProvider.GetRequiredService<WaitGather>());
    }

    private bool HasRequestedItems()
    {
        if (_currentRequest == null)
            return true;

        InventoryManager* inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
            return false;

        return inventoryManager->GetInventoryItemCount(_currentRequest.Data.ItemId,
            minCollectability: _currentRequest.Data.Collectability) >= _currentRequest.Data.Quantity;
    }

    public override IList<string> GetRemainingTaskNames()
    {
        if (_currentTask != null)
            return [_currentTask.ToString() ?? "?", .. base.GetRemainingTaskNames()];
        else
            return base.GetRemainingTaskNames();
    }

    private sealed class CurrentRequest
    {
        public required GatheringRequest Data { get; init; }
        public required GatheringRoot Root { get; init; }

        /// <summary>
        /// To make indexing easy with <see cref="CurrentIndex"/>, we flatten the list of gathering locations.
        /// </summary>
        public required List<GatheringNode> Nodes { get; init; }

        public int CurrentIndex { get; set; }
    }

    public sealed record GatheringRequest(ushort GatheringPointId, uint ItemId, int Quantity, short Collectability = 0);

    public enum EStatus
    {
        Gathering,
        Moving,
        Complete,
    }
}
