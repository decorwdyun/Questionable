using System;
using Dalamud.Game.Text;
using ExcelQuest = Lumina.Excel.GeneratedSheets.Quest;

namespace Questionable.Model;

internal sealed class QuestInfo
{
    public QuestInfo(ExcelQuest quest)
    {
        QuestId = (ushort)(quest.RowId & 0xFFFF);
        Name = quest.Name.ToString();
        Level = quest.ClassJobLevel0;
        IssuerDataId = quest.IssuerStart;
        IsRepeatable = quest.IsRepeatable;
    }

    public ushort QuestId { get; }
    public string Name { get; }
    public ushort Level { get; }
    public uint IssuerDataId { get; }
    public bool IsRepeatable { get; }

    public string SimplifiedName => Name
        .TrimStart(SeIconChar.QuestSync.ToIconChar(), SeIconChar.QuestRepeatable.ToIconChar(), ' ');
}
