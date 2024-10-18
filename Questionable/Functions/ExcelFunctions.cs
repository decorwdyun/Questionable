using System;
using System.Linq;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using LLib;
using Lumina.Excel.CustomSheets;
using Lumina.Excel.GeneratedSheets;
using Lumina.Text;
using Microsoft.Extensions.Logging;
using Questionable.Model;
using Quest = Questionable.Model.Quest;
using GimmickYesNo = Lumina.Excel.GeneratedSheets2.GimmickYesNo;

namespace Questionable.Functions;

internal sealed class ExcelFunctions
{
    private readonly IDataManager _dataManager;
    private readonly ILogger<ExcelFunctions> _logger;

    public ExcelFunctions(IDataManager dataManager, ILogger<ExcelFunctions> logger)
    {
        _dataManager = dataManager;
        _logger = logger;
    }

    public StringOrRegex GetDialogueText(Quest? currentQuest, string? excelSheetName, string key, bool isRegex)
    {
        var seString = GetRawDialogueText(currentQuest, excelSheetName, key);
        if (isRegex)
            return new StringOrRegex(seString.ToRegex());
        else
            return new StringOrRegex(seString?.ToDalamudString().ToString());
    }

    public SeString? GetRawDialogueText(Quest? currentQuest, string? excelSheetName, string key)
    {
        if (currentQuest != null && excelSheetName == null)
        {
            var questRow =
                _dataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets2.Quest>()!.GetRow((uint)currentQuest.Id.Value +
                    0x10000);
            if (questRow == null)
            {
                _logger.LogError("Could not find quest row for {QuestId}", currentQuest.Id);
                return null;
            }

            excelSheetName = $"quest/{(currentQuest.Id.Value / 100):000}/{questRow.Id}";
        }

        ArgumentNullException.ThrowIfNull(excelSheetName);
        var excelSheet = _dataManager.Excel.GetSheet<QuestDialogueText>(excelSheetName);
        if (excelSheet == null)
        {
            _logger.LogError("Unknown excel sheet '{SheetName}'", excelSheetName);
            return null;
        }

        return excelSheet.FirstOrDefault(x => x.Key == key)?.Value;
    }

    public StringOrRegex GetDialogueTextByRowId(string? excelSheet, uint rowId, bool isRegex)
    {
        var seString = GetRawDialogueTextByRowId(excelSheet, rowId);
        if (isRegex)
            return new StringOrRegex(seString.ToRegex());
        else
            return new StringOrRegex(seString?.ToDalamudString().ToString());
    }

    public SeString? GetRawDialogueTextByRowId(string? excelSheet, uint rowId)
    {
        if (excelSheet == "GimmickYesNo")
        {
            var questRow = _dataManager.GetExcelSheet<GimmickYesNo>()!.GetRow(rowId);
            return questRow?.Unknown0;
        }
        else if (excelSheet == "Warp")
        {
            var questRow = _dataManager.GetExcelSheet<Warp>()!.GetRow(rowId);
            return questRow?.Name;
        }
        else if (excelSheet is "Addon")
        {
            var questRow = _dataManager.GetExcelSheet<Addon>()!.GetRow(rowId);
            return questRow?.Text;
        }
        else if (excelSheet is "EventPathMove")
        {
            var questRow = _dataManager.GetExcelSheet<EventPathMove>()!.GetRow(rowId);
            return questRow?.Unknown10;
        }
        else if (excelSheet is "GilShop")
        {
            var questRow = _dataManager.GetExcelSheet<GilShop>()!.GetRow(rowId);
            return questRow?.Name;
        }
        else if (excelSheet is "ContentTalk" or null)
        {
            var questRow = _dataManager.GetExcelSheet<ContentTalk>()!.GetRow(rowId);
            return questRow?.Text;
        }
        else
            throw new ArgumentOutOfRangeException(nameof(excelSheet), $"Unsupported excel sheet {excelSheet}");
    }
}
