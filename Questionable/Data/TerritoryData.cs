using System.Collections.Immutable;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;

namespace Questionable.Data;

internal sealed class TerritoryData
{
    private readonly ImmutableDictionary<uint, string> _territoryNames;
    private readonly ImmutableHashSet<uint> _territoriesWithMount;

    public TerritoryData(IDataManager dataManager)
    {
        _territoryNames = dataManager.GetExcelSheet<TerritoryType>()!
            .Where(x => x.RowId > 0)
            .Select(x =>
                new
                {
                    x.RowId,
                    Name = x.PlaceName.Value?.Name?.ToString() ?? x.PlaceNameZone?.Value?.Name?.ToString(),
                })
            .Where(x => !string.IsNullOrEmpty(x.Name))
            .ToImmutableDictionary(x => x.RowId, x => x.Name!);

        _territoriesWithMount = dataManager.GetExcelSheet<TerritoryType>()!
            .Where(x => x.RowId > 0 && x.Mount)
            .Select(x => x.RowId)
            .ToImmutableHashSet();
    }

    public string? GetName(ushort territoryId) => _territoryNames.GetValueOrDefault(territoryId);

    public bool CanUseMount(ushort territoryId) => _territoriesWithMount.Contains(territoryId);
}
