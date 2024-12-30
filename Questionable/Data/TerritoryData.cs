using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.Sheets;

namespace Questionable.Data;

internal sealed class TerritoryData
{
    private readonly ImmutableDictionary<uint, string> _territoryNames;
    private readonly ImmutableHashSet<ushort> _territoriesWithMount;
    private readonly ImmutableDictionary<ushort, uint> _dutyTerritories;
    private readonly ImmutableDictionary<uint, string> _instanceNames;
    private readonly ImmutableDictionary<uint, string> _contentFinderConditionNames;
    private readonly ImmutableDictionary<uint, uint> _contentFinderConditionIds;

    public TerritoryData(IDataManager dataManager)
    {
        _territoryNames = dataManager.GetExcelSheet<TerritoryType>()
            .Where(x => x.RowId > 0)
            .Select(x =>
                new
                {
                    x.RowId,
                    Name = x.PlaceName.ValueNullable?.Name.ToString() ?? x.PlaceNameZone.ValueNullable?.Name.ToString(),
                })
            .Where(x => !string.IsNullOrEmpty(x.Name))
            .ToImmutableDictionary(x => x.RowId, x => x.Name!);

        _territoriesWithMount = dataManager.GetExcelSheet<TerritoryType>()
            .Where(x => x.RowId > 0 && x.Mount)
            .Select(x => (ushort)x.RowId)
            .ToImmutableHashSet();

        _dutyTerritories = dataManager.GetExcelSheet<TerritoryType>()
            .Where(x => x.RowId > 0 && x.ContentFinderCondition.RowId != 0)
            .ToImmutableDictionary(x => (ushort)x.RowId, x => x.ContentFinderCondition.Value.ContentType.RowId);

        _instanceNames = dataManager.GetExcelSheet<ContentFinderCondition>()
            .Where(x => x.RowId > 0 && x.Content.RowId != 0 && x.ContentLinkType == 1 && x.ContentType.RowId != 6)
            .ToImmutableDictionary(x => x.Content.RowId, x => x.Name.ToDalamudString().ToString());

        _contentFinderConditionNames = dataManager.GetExcelSheet<ContentFinderCondition>()
            .Where(x => x.RowId > 0 && x.Content.RowId != 0 && x.ContentLinkType == 1 && x.ContentType.RowId != 6)
            .ToImmutableDictionary(x => x.RowId, x => FixName(x.Name.ToDalamudString().ToString(), dataManager.Language));
        _contentFinderConditionIds = dataManager.GetExcelSheet<ContentFinderCondition>()
            .Where(x => x.RowId > 0 && x.Content.RowId != 0 && x.ContentLinkType == 1 && x.ContentType.RowId != 6)
            .ToImmutableDictionary(x => x.RowId, x => x.TerritoryType.RowId);
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

    public bool IsDutyInstance(ushort territoryId) => _dutyTerritories.ContainsKey(territoryId);

    public bool IsQuestBattleInstance(ushort territoryId) =>
        _dutyTerritories.TryGetValue(territoryId, out uint contentType) && contentType == 7;

    public string? GetInstanceName(ushort instanceId) => _instanceNames.GetValueOrDefault(instanceId);

    public string? GetContentFinderConditionName(uint cfcId) => _contentFinderConditionNames.GetValueOrDefault(cfcId);

    public bool TryGetTerritoryIdForContentFinderCondition(uint cfcId, out uint territoryId) =>
        _contentFinderConditionIds.TryGetValue(cfcId, out territoryId);

    private static string FixName(string name, ClientLanguage language)
    {
        if (string.IsNullOrEmpty(name) || language != ClientLanguage.English)
            return name;

        return string.Concat(name[0].ToString().ToUpper(CultureInfo.InvariantCulture), name.AsSpan(1));
    }
}
