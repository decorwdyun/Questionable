using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace Questionable.Controller.NavigationOverrides;

internal sealed class MovementOverrideController
{
    private static readonly List<IBlacklistedLocation> BlacklistedLocations = [
        new BlacklistedArea(1191, new(-223.0412f, 31.937134f, -584.03906f), 5f, 7.75f),
        new BlacklistedPoint(128, new Vector3(2f, 40.25f, 36.5f), new Vector3(0.25f, 40.25f, 36.5f))
    ];

    private readonly IClientState _clientState;
    private readonly ILogger<MovementOverrideController> _logger;

    public MovementOverrideController(IClientState clientState, ILogger<MovementOverrideController> logger)
    {
        _clientState = clientState;
        _logger = logger;
    }

    /// <summary>
    /// Certain areas shouldn't have navmesh points in them, e.g. the aetheryte in HF Outskirts can't be
    /// walked on without jumping, but if you teleport to the wrong side you're fucked otherwise.
    /// </summary>
    /// <param name="navPoints">list of points to check</param>
    public void AdjustPath(List<Vector3> navPoints)
    {
        foreach (var blacklistedArea in BlacklistedLocations)
        {
            if (_clientState.TerritoryType != blacklistedArea.TerritoryId)
                continue;

            for (int i = 0; i < navPoints.Count; ++i)
            {
                Vector3? updatedPoint = blacklistedArea.AdjustPoint(navPoints[i]);

                if (updatedPoint != null)
                {
                    _logger.LogInformation("Fudging navmesh point from {Original} to {Replacement} in blacklisted area",
                        navPoints[i].ToString("G", CultureInfo.InvariantCulture),
                        updatedPoint.Value.ToString("G", CultureInfo.InvariantCulture));

                    navPoints[i] = updatedPoint.Value;
                }
            }
        }
    }
}
