using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;
using Microsoft.Extensions.Logging;

namespace Questionable.Data;

internal sealed class GatheringData
{
    private readonly Dictionary<uint, ushort> _minerGatheringPoints = [];
    private readonly Dictionary<uint, ushort> _botanistGatheringPoints = [];
    private readonly Dictionary<uint, ushort> _itemIdToCollectability;
    private readonly Dictionary<uint, uint> _npcForCustomDeliveries;

    public GatheringData(IDataManager dataManager)
    {
        Dictionary<uint, uint> gatheringItemToItem = dataManager.GetExcelSheet<GatheringItem>()!
            .Where(x => x.RowId != 0 && x.Item != 0)
            .ToDictionary(x => x.RowId, x => (uint)x.Item);

        foreach (var gatheringPointBase in dataManager.GetExcelSheet<GatheringPointBase>()!)
        {
            foreach (var gatheringItemId in gatheringPointBase.Item.Where(x => x != 0))
            {
                if (gatheringItemToItem.TryGetValue((uint)gatheringItemId, out uint itemId))
                {
                    if (gatheringPointBase.GatheringType.Row is 0 or 1)
                        _minerGatheringPoints[itemId] = (ushort)gatheringPointBase.RowId;
                    else if (gatheringPointBase.GatheringType.Row is 2 or 3)
                        _botanistGatheringPoints[itemId] = (ushort)gatheringPointBase.RowId;
                }
            }
        }

        _itemIdToCollectability = dataManager.GetExcelSheet<SatisfactionSupply>()!
            .Where(x => x.RowId > 0)
            .Where(x => x.Slot is 2)
            .Select(x => new
            {
                ItemId = x.Item.Row,
                Collectability = x.CollectabilityHigh,
            })
            .Distinct()
            .ToDictionary(x => x.ItemId, x => x.Collectability);

        _npcForCustomDeliveries = dataManager.GetExcelSheet<SatisfactionNpc>()!
            .Where(x => x.RowId > 0)
            .SelectMany(x => dataManager.GetExcelSheet<SatisfactionSupply>()!
                .Where(y => y.RowId == x.SupplyIndex.Last())
                .Select(y => new
                {
                    ItemId = y.Item.Row,
                    NpcId = x.Npc.Row
                }))
            .Where(x => x.ItemId > 0)
            .Distinct()
            .ToDictionary(x => x.ItemId, x => x.NpcId);
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

    public ushort GetRecommendedCollectability(uint itemId)
        => _itemIdToCollectability.GetValueOrDefault(itemId);

    public bool TryGetCustomDeliveryNpc(uint itemId, out uint npcId)
        => _npcForCustomDeliveries.TryGetValue(itemId, out npcId);
}
