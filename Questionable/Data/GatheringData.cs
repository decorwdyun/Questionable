using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;

namespace Questionable.Data;

internal sealed class GatheringData
{
    private readonly Dictionary<uint, uint> _gatheringItemToItem;
    private readonly Dictionary<uint, ushort> _minerGatheringPoints = [];
    private readonly Dictionary<uint, ushort> _botanistGatheringPoints = [];

    public GatheringData(IDataManager dataManager)
    {
        _gatheringItemToItem = dataManager.GetExcelSheet<GatheringItem>()!
            .Where(x => x.RowId != 0 && x.Item != 0)
            .ToDictionary(x => x.RowId, x => (uint)x.Item);

        foreach (var gatheringPointBase in dataManager.GetExcelSheet<GatheringPointBase>()!)
        {
            foreach (var gatheringItemId in gatheringPointBase.Item.Where(x => x != 0))
            {
                if (_gatheringItemToItem.TryGetValue((uint)gatheringItemId, out uint itemId))
                {
                    if (gatheringPointBase.GatheringType.Row is 0 or 1)
                        _minerGatheringPoints[itemId] = (ushort)gatheringPointBase.RowId;
                    else if (gatheringPointBase.GatheringType.Row is 2 or 3)
                        _botanistGatheringPoints[itemId] = (ushort)gatheringPointBase.RowId;
                }
            }
        }
    }


    public bool TryGetGatheringPointId(uint itemId, uint classJobId, out ushort gatheringPointId)
    {
        if (classJobId == 16)
            return _minerGatheringPoints.TryGetValue(itemId, out gatheringPointId);
        else if (classJobId == 17)
            return _botanistGatheringPoints.TryGetValue(itemId, out gatheringPointId);
        else
        {
            gatheringPointId = 0;
            return false;
        }
    }
}
