using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.GeneratedSheets;

namespace Questionable.Data;

internal sealed class TerritoryData
{
    private readonly ImmutableDictionary<uint, string> _territoryNames;
    private readonly ImmutableHashSet<ushort> _territoriesWithMount;
    private readonly ImmutableHashSet<ushort> _dutyTerritories;

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
            .Select(x => (ushort)x.RowId)
            .ToImmutableHashSet();

        _dutyTerritories = dataManager.GetExcelSheet<TerritoryType>()!
            .Where(x => x.RowId > 0 && x.ContentFinderCondition.Row != 0)
            .Select(x => (ushort)x.RowId)
            .ToImmutableHashSet();
    }

    public string? GetName(ushort territoryId) => _territoryNames.GetValueOrDefault(territoryId);

    public string GetNameAndId(ushort territoryId)
    {
        string? territoryName = GetName(territoryId);
        if (territoryName != null)
            return string.Create(CultureInfo.InvariantCulture, $"{territoryName} ({territoryId})");
        else
            return territoryId.ToString(CultureInfo.InvariantCulture);
    }

    public bool CanUseMount(ushort territoryId) => _territoriesWithMount.Contains(territoryId);

    public bool IsDutyInstance(ushort territoryId) => _dutyTerritories.Contains(territoryId);
}
