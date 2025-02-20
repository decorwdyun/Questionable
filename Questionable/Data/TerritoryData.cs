using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Lumina.Excel.Sheets;

namespace Questionable.Data;

internal sealed class TerritoryData
{
    private readonly ImmutableDictionary<uint, string> _territoryNames;
    private readonly ImmutableHashSet<ushort> _territoriesWithMount;
    private readonly ImmutableDictionary<ushort, uint> _dutyTerritories;
    private readonly ImmutableDictionary<uint, string> _instanceNames;
    private readonly ImmutableDictionary<uint, ContentFinderConditionData> _contentFinderConditions;

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

        _contentFinderConditions = dataManager.GetExcelSheet<ContentFinderCondition>()
            .Where(x => x.RowId > 0 && x.Content.RowId != 0 && x.ContentLinkType is 1 or 5 && x.ContentType.RowId != 6)
            .Select(x => new ContentFinderConditionData(x, dataManager.Language))
            .ToImmutableDictionary(x => x.ContentFinderConditionId, x => x);
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

    public ContentFinderConditionData? GetContentFinderCondition(uint cfcId) =>
        _contentFinderConditions.GetValueOrDefault(cfcId);

    public bool TryGetContentFinderCondition(uint cfcId,
        [NotNullWhen(true)] out ContentFinderConditionData? contentFinderConditionData) =>
        _contentFinderConditions.TryGetValue(cfcId, out contentFinderConditionData);

    private static string FixName(string name, ClientLanguage language)
    {
        if (string.IsNullOrEmpty(name) || language != ClientLanguage.English)
            return name;

        return string.Concat(name[0].ToString().ToUpper(CultureInfo.InvariantCulture), name.AsSpan(1));
    }

    public sealed record ContentFinderConditionData(
        uint ContentFinderConditionId,
        string Name,
        uint TerritoryId,
        ushort RequiredItemLevel)
    {
        public ContentFinderConditionData(ContentFinderCondition condition, ClientLanguage clientLanguage)
            : this(condition.RowId, FixName(condition.Name.ToDalamudString().ToString(), clientLanguage),
                condition.TerritoryType.RowId, condition.ItemLevelRequired)
        {
        }
    }
}
