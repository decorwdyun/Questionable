using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Logging;
using Questionable.Data;
using Questionable.Model.Common;

namespace Questionable.External;

internal sealed class LifestreamIpc
{
    private readonly AetheryteData _aetheryteData;
    private readonly IDataManager _dataManager;
    private readonly ILogger<LifestreamIpc> _logger;
    private readonly ICallGateSubscriber<string, bool> _aethernetTeleport;

    public LifestreamIpc(IDalamudPluginInterface pluginInterface, AetheryteData aetheryteData, IDataManager dataManager, ILogger<LifestreamIpc> logger)
    {
        _aetheryteData = aetheryteData;
        _dataManager = dataManager;
        _logger = logger;
        _aethernetTeleport = pluginInterface.GetIpcSubscriber<string, bool>("Lifestream.AethernetTeleport");
    }

    public bool Teleport(EAetheryteLocation aetheryteLocation)
    {
        string? name = aetheryteLocation switch
        {
            EAetheryteLocation.IshgardFirmament => "Firmament",
            EAetheryteLocation.FirmamentMendicantsCourt => GetPlaceName(3436),
            EAetheryteLocation.FirmamentMattock => GetPlaceName(3473),
            EAetheryteLocation.FirmamentNewNest => GetPlaceName(3475),
            EAetheryteLocation.FirmanentSaintRoellesDais => GetPlaceName(3474),
            EAetheryteLocation.FirmamentFeatherfall => GetPlaceName(3525),
            EAetheryteLocation.FirmamentHoarfrostHall => GetPlaceName(3528),
            EAetheryteLocation.FirmamentWesternRisensongQuarter => GetPlaceName(3646),
            EAetheryteLocation.FIrmamentEasternRisensongQuarter => GetPlaceName(3645),
            _ => _aetheryteData.AethernetNames.GetValueOrDefault(aetheryteLocation),
        };

        if (name == null)
            return false;

        _logger.LogInformation("Teleporting to '{Name}'", name);
        return _aethernetTeleport.InvokeFunc(name);
    }

    private string GetPlaceName(uint rowId) => _dataManager.GetExcelSheet<PlaceName>().GetRow(rowId).Name.ToString();
}
