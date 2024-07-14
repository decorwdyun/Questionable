using System.Collections.Immutable;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets2;

namespace Questionable.Data;

internal sealed class AetherCurrentData
{
    private readonly ImmutableDictionary<ushort, ImmutableList<uint>> _overworldCurrents;

    public AetherCurrentData(IDataManager dataManager)
    {
        _overworldCurrents = dataManager.GetExcelSheet<AetherCurrentCompFlgSet>()!
            .Where(x => x.RowId > 0)
            .Where(x => x.Territory.Value != null)
            .ToImmutableDictionary(
                x => (ushort)x.Territory.Row,
                x => x.AetherCurrents
                    .Where(y => y.Row > 0 && y.Value?.Quest.Row == 0)
                    .Select(y => y.Row)
                    .ToImmutableList());
    }

    public bool IsValidAetherCurrent(ushort territoryId, uint aetherCurrentId)
    {
        return _overworldCurrents.TryGetValue(territoryId, out ImmutableList<uint>? currentIds) &&
               currentIds.Contains(aetherCurrentId);
    }
}
