using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Questionable.Controller.Utils;

internal sealed class PartyWatchDog : IDisposable
{
    private readonly QuestController _questController;
    private readonly IClientState _clientState;
    private readonly IChatGui _chatGui;
    private readonly ILogger<PartyWatchDog> _logger;

    private ushort? _uncheckedTeritoryId;

    public PartyWatchDog(QuestController questController, IClientState clientState, IChatGui chatGui,
        ILogger<PartyWatchDog> logger)
    {
        _questController = questController;
        _clientState = clientState;
        _chatGui = chatGui;
        _logger = logger;

        _clientState.TerritoryChanged += TerritoryChanged;
    }

    private unsafe void TerritoryChanged(ushort newTerritoryId)
    {
        var intendedUse = (ETerritoryIntendedUseEnum)GameMain.Instance()->CurrentTerritoryIntendedUseId;
        switch (intendedUse)
        {
            case ETerritoryIntendedUseEnum.Gaol:
            case ETerritoryIntendedUseEnum.Frontline:
            case ETerritoryIntendedUseEnum.LordOfVerminion:
            case ETerritoryIntendedUseEnum.Diadem:
            case ETerritoryIntendedUseEnum.CrystallineConflict:
            case ETerritoryIntendedUseEnum.Battlehall:
            case ETerritoryIntendedUseEnum.CrystallineConflict2:
            case ETerritoryIntendedUseEnum.DeepDungeon:
            case ETerritoryIntendedUseEnum.TreasureMapDuty:
            case ETerritoryIntendedUseEnum.Diadem2:
            case ETerritoryIntendedUseEnum.RivalWings:
            case ETerritoryIntendedUseEnum.Eureka:
            case ETerritoryIntendedUseEnum.LeapOfFaith:
            case ETerritoryIntendedUseEnum.OceanFishing:
            case ETerritoryIntendedUseEnum.Diadem3:
            case ETerritoryIntendedUseEnum.Bozja:
            case ETerritoryIntendedUseEnum.Battlehall2:
            case ETerritoryIntendedUseEnum.Battlehall3:
            case ETerritoryIntendedUseEnum.LargeScaleRaid:
            case ETerritoryIntendedUseEnum.LargeScaleSavageRaid:
            case ETerritoryIntendedUseEnum.Blunderville:
                StopIfRunning($"Unsupported Area entered ({newTerritoryId})");
                break;

            case ETerritoryIntendedUseEnum.Dungeon:
            case ETerritoryIntendedUseEnum.VariantDungeon:
            case ETerritoryIntendedUseEnum.AllianceRaid:
            case ETerritoryIntendedUseEnum.Trial:
            case ETerritoryIntendedUseEnum.Raid:
            case ETerritoryIntendedUseEnum.Raid2:
            case ETerritoryIntendedUseEnum.SeasonalEvent:
            case ETerritoryIntendedUseEnum.SeasonalEvent2:
            case ETerritoryIntendedUseEnum.CriterionDuty:
            case ETerritoryIntendedUseEnum.CriterionSavageDuty:
                _uncheckedTeritoryId = newTerritoryId;
                _logger.LogInformation("Will check territory {TerritoryId} after loading", newTerritoryId);
                break;
        }
    }

    public unsafe void Update()
    {
        if (_uncheckedTeritoryId == _clientState.TerritoryType && GameMain.Instance()->TerritoryLoadState == 2)
        {
            var groupManager = GroupManager.Instance();
            if (groupManager == null)
                return;

            byte memberCount = groupManager->MainGroup.MemberCount;
            _logger.LogDebug("Terrritory {TerritoryId} with {MemberCount} members", _uncheckedTeritoryId, memberCount);
            if (memberCount > 1)
                StopIfRunning("Other party members present");

            _uncheckedTeritoryId = null;
        }
    }

    private void StopIfRunning(string reason)
    {
        if (_questController.IsRunning || _questController.AutomationType != QuestController.EAutomationType.Manual)
        {
            _chatGui.PrintError(
                $"Stopping Questionable: {reason}. If you believe this to be correct, please restart Questionable manually.",
                CommandHandler.MessageTag, CommandHandler.TagColor);
            _questController.Stop(reason);
        }
    }

    public void Dispose()
    {
        _clientState.TerritoryChanged -= TerritoryChanged;
    }

    // from https://github.com/NightmareXIV/ECommons/blob/f69e460e95134c72592654059843b138b4c01a9e/ECommons/ExcelServices/TerritoryIntendedUseEnum.cs#L5
    [UsedImplicitly(ImplicitUseTargetFlags.Members, Reason = "game data")]
    private enum ETerritoryIntendedUseEnum : byte
    {
        CityArea = 0,
        OpenWorld = 1,
        Inn = 2,
        Dungeon = 3,
        VariantDungeon = 4,
        Gaol = 5,
        StartingArea = 6,
        QuestArea = 7,
        AllianceRaid = 8,
        QuestBattle = 9,
        Trial = 10,
        QuestArea2 = 12,
        ResidentialArea = 13,
        HousingInstances = 14,
        QuestArea3 = 15,
        Raid = 16,
        Raid2 = 17,
        Frontline = 18,
        ChocoboSquare = 20,
        RestorationEvent = 21,
        Sanctum = 22,
        GoldSaucer = 23,
        LordOfVerminion = 25,
        Diadem = 26,
        HallOfTheNovice = 27,
        CrystallineConflict = 28,
        QuestBattle2 = 29,
        Barracks = 30,
        DeepDungeon = 31,
        SeasonalEvent = 32,
        TreasureMapDuty = 33,
        SeasonalEventDuty = 34,
        Battlehall = 35,
        CrystallineConflict2 = 37,
        Diadem2 = 38,
        RivalWings = 39,
        Unknown1 = 40,
        Eureka = 41,
        SeasonalEvent2 = 43,
        LeapOfFaith = 44,
        MaskedCarnivale = 45,
        OceanFishing = 46,
        Diadem3 = 47,
        Bozja = 48,
        IslandSanctuary = 49,
        Battlehall2 = 50,
        Battlehall3 = 51,
        LargeScaleRaid = 52,
        LargeScaleSavageRaid = 53,
        QuestArea4 = 54,
        TribalInstance = 56,
        CriterionDuty = 57,
        CriterionSavageDuty = 58,
        Blunderville = 59,
    }
}
