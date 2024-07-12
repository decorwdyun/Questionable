using System.Numerics;

namespace Questionable.Controller.NavigationOverrides;

internal interface IBlacklistedLocation
{
    ushort TerritoryId { get; }

    Vector3? AdjustPoint(Vector3 point);
}
