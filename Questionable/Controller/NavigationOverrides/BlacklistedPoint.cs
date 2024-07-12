using System.Numerics;

namespace Questionable.Controller.NavigationOverrides;

public sealed record BlacklistedPoint(
    ushort TerritoryId,
    Vector3 Original,
    Vector3 Replacement,
    float CheckDistance = 0.05f) : IBlacklistedLocation
{
    public Vector3? AdjustPoint(Vector3 point)
    {
        float distance = (point - Original).Length();
        if (distance > CheckDistance)
            return null;

        return Replacement;
    }
}
