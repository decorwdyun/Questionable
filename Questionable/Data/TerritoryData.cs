using System.Collections.Immutable;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;

namespace Questionable.Data;

internal sealed class TerritoryData
{
    private readonly ImmutableHashSet<uint> _territoriesWithMount;

    public TerritoryData(IDataManager dataManager)
    {
        _territoriesWithMount = dataManager.GetExcelSheet<TerritoryType>()!
            .Where(x => x.RowId > 0 && x.Mount)
            .Select(x => x.RowId)
            .ToImmutableHashSet();
    }

    public bool CanUseMount(ushort territoryId) => _territoriesWithMount.Contains(territoryId);
}
