using Lumina.Excel.GeneratedSheets;
using Questionable.Model.Questing;

namespace Questionable.Model;

internal sealed class SatisfactionSupplyInfo : IQuestInfo
{
    public SatisfactionSupplyInfo(SatisfactionNpc npc)
    {
        QuestId = new SatisfactionSupplyNpcId((ushort)npc.RowId);
        Name = npc.Npc.Value!.Singular;
        IssuerDataId = npc.Npc.Row;
        Level = npc.LevelUnlock;
    }

    public ElementId QuestId { get; }
    public string Name { get; }
    public uint IssuerDataId { get; }
    public bool IsRepeatable => true;
    public ushort Level { get; }
    public EBeastTribe BeastTribe => EBeastTribe.None;
    public bool IsMainScenarioQuest => false;
}
