using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using GatheringPathRenderer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Shared;
using Questionable.External;
using Questionable.Model.Gathering;

namespace Questionable.Controller.Steps.Gathering;

internal sealed class MoveToLandingLocation(
    IServiceProvider serviceProvider,
    IObjectTable objectTable,
    NavmeshIpc navmeshIpc,
    ILogger<MoveToLandingLocation> logger) : ITask
{
    private ushort _territoryId;
    private GatheringNode _gatheringNode = null!;
    private ITask _moveTask = null!;

    public ITask With(ushort territoryId, GatheringNode gatheringNode)
    {
        _territoryId = territoryId;
        _gatheringNode = gatheringNode;
        return this;
    }

    public bool Start()
    {
        var location = _gatheringNode.Locations.First();
        if (_gatheringNode.Locations.Count > 1)
        {
            var gameObject = objectTable.Single(x =>
                x.ObjectKind == ObjectKind.GatheringPoint && x.DataId == _gatheringNode.DataId && x.IsTargetable);
            location = _gatheringNode.Locations.Single(x => Vector3.Distance(x.Position, gameObject.Position) < 0.1f);
        }

        var (target, degrees, range) = GatheringMath.CalculateLandingLocation(location);
        logger.LogInformation("Preliminary landing location: {Location}, with degrees = {Degrees}, range = {Range}",
            target.ToString("G", CultureInfo.InvariantCulture), degrees, range);

        Vector3? pointOnFloor = navmeshIpc.GetPointOnFloor(target with { Y = target.Y + 5f });
        if (pointOnFloor != null)
            pointOnFloor = pointOnFloor.Value with { Y = pointOnFloor.Value.Y + 0.5f };

        logger.LogInformation("Final landing location: {Location}",
            (pointOnFloor ?? target).ToString("G", CultureInfo.InvariantCulture));

        _moveTask = serviceProvider.GetRequiredService<Move.MoveInternal>()
            .With(_territoryId, pointOnFloor ?? target, 0.25f, dataId: _gatheringNode.DataId, fly: true,
                ignoreDistanceToObject: true);
        return _moveTask.Start();
    }

    public ETaskResult Update() => _moveTask.Update();

    public override string ToString() => $"Land/{_moveTask}";
}
