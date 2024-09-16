using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Shared;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Gathering;

namespace Questionable.Controller.Steps.Gathering;

internal sealed class MoveToLandingLocation(
    ushort territoryId,
    bool flyBetweenNodes,
    GatheringNode gatheringNode,
    MoveTo.Factory moveFactory,
    GameFunctions gameFunctions,
    IObjectTable objectTable,
    ILogger<MoveToLandingLocation> logger) : ITask
{
    private ITask _moveTask = null!;

    public bool Start()
    {
        var location = gatheringNode.Locations.First();
        if (gatheringNode.Locations.Count > 1)
        {
            var gameObject = objectTable.SingleOrDefault(x =>
                x.ObjectKind == ObjectKind.GatheringPoint && x.DataId == gatheringNode.DataId && x.IsTargetable);
            if (gameObject == null)
                return false;

            location = gatheringNode.Locations.Single(x => Vector3.Distance(x.Position, gameObject.Position) < 0.1f);
        }

        var (target, degrees, range) = GatheringMath.CalculateLandingLocation(location);
        logger.LogInformation("Preliminary landing location: {Location}, with degrees = {Degrees}, range = {Range}",
            target.ToString("G", CultureInfo.InvariantCulture), degrees, range);

        bool fly = flyBetweenNodes && gameFunctions.IsFlyingUnlocked(territoryId);
        _moveTask = moveFactory.Move(new MoveTo.MoveParams(territoryId, target, null, 0.25f,
            DataId: gatheringNode.DataId, Fly: fly, IgnoreDistanceToObject: true));
        return _moveTask.Start();
    }

    public ETaskResult Update() => _moveTask.Update();

    public override string ToString() => $"Land/{_moveTask}/{flyBetweenNodes}";
}
