using System.Linq;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Common.Math;
using Questionable.Data;
using Questionable.Model.Questing;

namespace Questionable.Windows.QuestComponents;

internal sealed class ARealmRebornComponent
{
    private static readonly QuestId ATimeForEveryPurpose = new(425);
    private static readonly QuestId TheUltimateWeapon = new(524);
    private static readonly QuestId GoodIntentions = new(363);
    private static readonly ushort[] RequiredPrimalInstances = [20004, 20006, 20005];

    private static readonly QuestId[] RequiredAllianceRaidQuests =
        [new(1709), new(1200), new(1201), new(1202), new(1203), new(1474), new(494), new(495)];

    private readonly GameFunctions _gameFunctions;
    private readonly QuestData _questData;
    private readonly TerritoryData _territoryData;
    private readonly UiUtils _uiUtils;

    public ARealmRebornComponent(GameFunctions gameFunctions, QuestData questData, TerritoryData territoryData,
        UiUtils uiUtils)
    {
        _gameFunctions = gameFunctions;
        _questData = questData;
        _territoryData = territoryData;
        _uiUtils = uiUtils;
    }

    public bool ShouldDraw => !_gameFunctions.IsQuestAcceptedOrComplete(ATimeForEveryPurpose) &&
                              _gameFunctions.IsQuestComplete(TheUltimateWeapon);

    public void Draw()
    {
        if (!_gameFunctions.IsQuestAcceptedOrComplete(GoodIntentions))
            DrawPrimals();

        DrawAllianceRaids();
    }

    private void DrawPrimals()
    {
        bool complete = UIState.IsInstanceContentCompleted(RequiredPrimalInstances.Last());
        bool hover = _uiUtils.ChecklistItem("Hard Mode Primals", complete);
        if (complete || !hover)
            return;

        using var tooltip = ImRaii.Tooltip();
        if (!tooltip)
            return;

        foreach (var instanceId in RequiredPrimalInstances)
        {
            (Vector4 color, FontAwesomeIcon icon) = UiUtils.GetInstanceStyle(instanceId);
            _uiUtils.ChecklistItem(_territoryData.GetInstanceName(instanceId) ?? "?", color, icon);
        }
    }

    private void DrawAllianceRaids()
    {
        bool complete = _gameFunctions.IsQuestComplete(RequiredAllianceRaidQuests.Last());
        bool hover = _uiUtils.ChecklistItem("Crystal Tower Raids", complete);
        if (complete || !hover)
            return;

        using var tooltip = ImRaii.Tooltip();
        if (!tooltip)
            return;

        foreach (var questId in RequiredAllianceRaidQuests)
        {
            (Vector4 color, FontAwesomeIcon icon, _) = _uiUtils.GetQuestStyle(questId);
            _uiUtils.ChecklistItem(_questData.GetQuestInfo(questId).Name, color, icon);
        }
    }
}
