using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace Questionable.Data.Sheets;

// TODO Remove once fixed in dalamud
[Sheet("Quest", 0x1F8C7430)]
public readonly unsafe struct QuestEx(ExcelPage page, uint offset, uint row) : IExcelRow<QuestEx>
{
    public uint RowId => row;

    public Quest Original { get; } = new(page, offset, row);

    public RowRef IssuerStart => RowRef.GetFirstValidRowOrUntyped(page.Module, page.ReadUInt32(offset + 2456), [typeof(EObjName), typeof(ENpcResident)], 882056187, page.Language);
    public RowRef<Level> IssuerLocation => new(page.Module, page.ReadUInt32(offset + 2460), page.Language);
    public RowRef<JournalGenre> JournalGenre => new(page.Module, page.ReadUInt32(offset + 2468), page.Language);
    public ushort SortKey => page.ReadUInt16(offset + 2502);
    public readonly RowRef<ExVersion> Expansion => new(page.Module, (uint)page.ReadUInt8(offset + 2504), page.Language);
    public readonly byte PreviousQuestJoin => page.ReadUInt8(offset + 2508);
    public readonly RowRef<ClassJobCategory> ClassJobCategory0 => new(page.Module, (uint)page.ReadUInt8(offset + 2505), page.Language);
    public readonly RowRef<ClassJobCategory> ClassJobCategory1 => new(page.Module, (uint)page.ReadUInt8(offset + 2507), page.Language);
    public readonly RowRef<Festival> Festival => new(page.Module, (uint)page.ReadUInt8(offset + 2517), page.Language);
    public readonly byte Unknown7 => page.ReadUInt8(offset + 2509);
    public readonly byte QuestLockJoin => page.ReadUInt8(offset + 2510);
    public readonly RowRef<GrandCompany> GrandCompany => new(page.Module, (uint)page.ReadUInt8(offset + 2514), page.Language);
    public readonly byte InstanceContentJoin => page.ReadUInt8(offset + 2516);
    public readonly RowRef<BeastTribe> BeastTribe => new(page.Module, (uint)page.ReadUInt8(offset + 2520), page.Language);
    public bool IsRepeatable => page.ReadPackedBool(offset + 2535, 1);

    public readonly Collection<RowRef<Quest>> PreviousQuest => new(page, offset, offset, &PreviousQuestCtor, 3);
    private static RowRef<Quest> PreviousQuestCtor(ExcelPage page, uint parentOffset, uint offset, uint i) => new(page.Module, page.ReadUInt32(offset + 2424 + i * 4), page.Language);

    public readonly Collection<RowRef<Quest>> QuestLock => new(page, offset, offset, &QuestLockCtor, 2);
    private static RowRef<Quest> QuestLockCtor(ExcelPage page, uint parentOffset, uint offset, uint i) => new(page.Module, page.ReadUInt32(offset + 2436 + i * 4), page.Language);

    public readonly Collection<ushort> ClassJobLevel => new(page, offset, offset, &ClassJobLevelCtor, 2);
    private static ushort ClassJobLevelCtor(ExcelPage page, uint parentOffset, uint offset, uint i) => page.ReadUInt16(offset + 2484 + i * 2);

    public Collection<RowRef<InstanceContent>> InstanceContent => new(page, offset, offset, &InstanceContentCtor, 3);
    private static RowRef<InstanceContent> InstanceContentCtor(ExcelPage page, uint parentOffset, uint offset, uint i) => new(page.Module, page.ReadUInt32(offset + 2444 + i * 4), page.Language);

    static QuestEx IExcelRow<QuestEx>.Create(ExcelPage page, uint offset, uint row) =>
        new(page, offset, row);
}
