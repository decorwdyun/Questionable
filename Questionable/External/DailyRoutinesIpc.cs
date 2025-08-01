using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Logging;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Model.Common;

namespace Questionable.External;

internal sealed class DailyRoutinesIpc: IDisposable
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ILogger<DailyRoutinesIpc> _logger;
    private readonly Configuration _configuration;
    private readonly IFramework _frameworkManager;
    private readonly QuestController _questController;
    private readonly AetheryteData _aetheryteData;
    private readonly IDataManager _dataManager;
    private readonly ICommandManager _commandManager;


    private DateTime _nextCheckTime = DateTime.MinValue;

    public readonly string[] ConflictingModules =
    [
        "AutoTalkSkip",
        "AutoCutsceneSkip",
        "OptimizedInteraction",
        "AutoQuestComplete",
        "AutoRequestItemSubmit",
        "AutoFateSync",
        "InstantDismount",
        "AutoHideGameObjects",
        "AutoUnlockAllContents"
    ];

    private HashSet<string> _suppressedModules = [];

    private readonly ICallGateSubscriber<string, bool?> _isModuleEnabled;
    private readonly ICallGateSubscriber<Version?> _dailyRoutinesVersion;
    private readonly ICallGateSubscriber<string, bool, bool> _loadModule;
    private readonly ICallGateSubscriber<string, bool, bool, bool> _unloadModule;

    public bool IsDailyRoutinesEnabled => _pluginInterface.InstalledPlugins.Any(x => x is { InternalName: "DailyRoutines", IsLoaded: true });

    public DailyRoutinesIpc(
        IDalamudPluginInterface pluginInterface,
        ILogger<DailyRoutinesIpc> logger,
        Configuration configuration,
        IFramework frameworkManager,
        QuestController questController,
        AetheryteData aetheryteData,
        IDataManager dataManager,
        ICommandManager commandManager
        )
    {
        _pluginInterface = pluginInterface;
        _logger = logger;
        _configuration = configuration;
        _frameworkManager = frameworkManager;
        _questController = questController;
        _aetheryteData = aetheryteData;
        _dataManager = dataManager;
        _commandManager = commandManager;
        _isModuleEnabled = pluginInterface.GetIpcSubscriber<string, bool?>("DailyRoutines.IsModuleEnabled");
        _dailyRoutinesVersion = pluginInterface.GetIpcSubscriber<Version?>("DailyRoutines.Version");
        _loadModule = pluginInterface.GetIpcSubscriber<string, bool, bool>("DailyRoutines.LoadModule");
        _unloadModule = pluginInterface.GetIpcSubscriber<string, bool, bool, bool>("DailyRoutines.UnloadModule");
        
        _frameworkManager.Update += OnUpdate;
    }

    private void OnUpdate(IFramework framework)
    {
        if (DateTime.Now < _nextCheckTime)
            return;
        _nextCheckTime = DateTime.Now.AddMilliseconds(500);
        
        if (IsDailyRoutinesEnabled == false) return;

        var hasActiveQuest = _questController.IsRunning ||
                             _questController.AutomationType != QuestController.EAutomationType.Manual;
        try
        {
            if (_configuration.General.ConfigureDailyRoutines && hasActiveQuest)
            {

                var version = _dailyRoutinesVersion.InvokeFunc();
                if (version == null) return;


                foreach (var conflictingModule in ConflictingModules)
                {
                    var rt = IsModuleEnabled(conflictingModule);
                    if (rt.HasValue && rt.Value)
                    {
                        UnloadModule(conflictingModule, false, false);
                        _suppressedModules.Add(conflictingModule);
                        _logger.LogInformation("Suppressed conflicting module: {Module}", conflictingModule);
                    }
                }
            }
            else
            {
                RecoverSuppressedModules();
            }
        }
        catch
        {
            // ignored
        }
    }

    private void RecoverSuppressedModules()
    {
        if (IsDailyRoutinesEnabled && _suppressedModules.Count > 0)
        {
            foreach (var suppressedModule in _suppressedModules)
            {
                LoadModule(suppressedModule, false);
                _logger.LogInformation("Re-enabled suppressed module: {Module}", suppressedModule);
            }
            _suppressedModules = [];
        }
    }
    
    private bool? IsModuleEnabled(string moduleName)
    {
        return !IsDailyRoutinesEnabled ? null : _isModuleEnabled.InvokeFunc(moduleName);
        ;
    }
    private bool LoadModule(string moduleName, bool affectConfig)
    {
        return IsDailyRoutinesEnabled && _loadModule.InvokeFunc(moduleName, affectConfig);
    }
    
    private bool UnloadModule(string moduleName, bool affectConfig, bool force)
    {
        return IsDailyRoutinesEnabled && _unloadModule.InvokeFunc(moduleName, affectConfig, force);
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
            _ => aetheryteLocation.ToFriendlyString(),
        };
        
        if (string.IsNullOrEmpty(name))
            return false;
        _logger.LogInformation("Teleporting to '{Name}'", name);
        _frameworkManager.RunOnTick(() =>
        {
            var moduleState = IsModuleEnabled("BetterTeleport");
    
            if (moduleState == null)
            {
                _logger.LogError("'BetterTeleport' 模块不存在，请检查 DailyRoutines。");
                return;
            };
            if (moduleState == false) LoadModule("BetterTeleport", false);
            
            _commandManager.ProcessCommand($"/pdrtelepo {name}");
        }, TimeSpan.FromMilliseconds(800));
        return true;
    }

    private string GetPlaceName(uint rowId) => _dataManager.GetExcelSheet<PlaceName>().GetRow(rowId).Name.ToString();
    public void Dispose()
    {
        _frameworkManager.Update -= OnUpdate;
        RecoverSuppressedModules();
    }
}