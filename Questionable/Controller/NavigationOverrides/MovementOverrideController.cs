using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace Questionable.Controller.NavigationOverrides;

internal sealed class MovementOverrideController
{
    private static readonly List<IBlacklistedLocation> BlacklistedLocations =
    [
        new BlacklistedArea(1191, new(-223.0412f, 31.937134f, -584.03906f), 5f, 7.75f),

        // New Gridania Navmesh workaround
        new BlacklistedPoint(128, new(2f, 40.25f, 36.5f), new(0.25f, 40.25f, 36.5f)),

        // lotus stand
        new BlacklistedPoint(205, new(26.75f, 0.5f, 20.75f), new(27.179117f, 0.26728272f, 19.714373f)),

        new BlacklistedPoint(132, new(45.5f, -8f, 101f), new(50.53978f, -8.046954f, 101.06045f)),

        // eastern thanalan
        new BlacklistedPoint(145, new(-139.75f, -32.25f, 75.25f), new(-139.57748f, -33.785175f, 77.87906f)),

        // southern thanalan
        new BlacklistedPoint(146, new(-201.75f, 10.5f, -265.5f), new(-203.75235f, 10.130764f, -265.15314f)),

        // lower la noscea - moraby drydocks aetheryte
        new BlacklistedArea(135, new(156.11499f, 15.518433f, 673.21277f), 0.5f, 5f),

        // coerthas central highlands
        new BlacklistedPoint(155, new(-478.75f, 149.25f, -305.75f), new(-476.1802f, 149.06573f, -304.7811f)),

        new BlacklistedPoint(1189, new(574f, -142.25f, 504.25f), new(574.44183f, -142.12766f, 507.60065f)),

        // heritage found: yyupye's halo (farm, npc: Mahuwsa)
        new BlacklistedPoint(1191, new(-108f, 29.25f, -350.75f), new(-107.56289f, 29.008266f, -348.80087f)),
        new BlacklistedPoint(1191, new(-105.75f, 29.75f, -351f), new(-105.335304f, 29.017048f, -348.85077f)),
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
