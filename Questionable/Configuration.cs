﻿using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Dalamud.Configuration;
using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using LLib.GameData;
using LLib.ImGui;

namespace Questionable;

internal sealed class Configuration : IPluginConfiguration
{
    public const int PluginSetupVersion = 5;

    public int Version { get; set; } = 1;
    public int PluginSetupCompleteVersion { get; set; }
    public GeneralConfiguration General { get; } = new();
    public DutyConfiguration Duties { get; } = new();
    public SinglePlayerDutyConfiguration SinglePlayerDuties { get; } = new();
    public NotificationConfiguration Notifications { get; } = new();
    public AdvancedConfiguration Advanced { get; } = new();
    public WindowConfig DebugWindowConfig { get; } = new();
    public WindowConfig ConfigWindowConfig { get; } = new();

    internal bool IsPluginSetupComplete() => PluginSetupCompleteVersion == PluginSetupVersion;

    internal void MarkPluginSetupComplete() => PluginSetupCompleteVersion = PluginSetupVersion;

    internal sealed class GeneralConfiguration
    {
        public ECombatModule CombatModule { get; set; } = ECombatModule.None;
        public uint MountId { get; set; } = 71;
        public GrandCompany GrandCompany { get; set; } = GrandCompany.None;
        public EClassJob CombatJob { get; set; } = EClassJob.Adventurer;
        public bool HideInAllInstances { get; set; } = true;
        public bool UseEscToCancelQuesting { get; set; } = true;
        public bool ShowIncompleteSeasonalEvents { get; set; } = true;
        public bool SkipLowPriorityDuties { get; set; }
        public bool ConfigureTextAdvance { get; set; } = true;
        public bool ConfigureDailyRoutines { get; set; } = true;
        public bool UsingDailyRoutinesTeleport { get; set; }
    }

    internal sealed class DutyConfiguration
    {
        public bool RunInstancedContentWithAutoDuty { get; set; }
        public HashSet<uint> WhitelistedDutyCfcIds { get; set; } = [];
        public HashSet<uint> BlacklistedDutyCfcIds { get; set; } = [];
    }

    internal sealed class SinglePlayerDutyConfiguration
    {
        public bool RunSoloInstancesWithBossMod { get; set; }

        [SuppressMessage("Performance", "CA1822", Justification = "Will be fixed when no longer WIP")]
        public byte RetryDifficulty => 0;

        public HashSet<uint> WhitelistedSinglePlayerDutyCfcIds { get; set; } = [];
        public HashSet<uint> BlacklistedSinglePlayerDutyCfcIds { get; set; } = [];
    }

    internal sealed class NotificationConfiguration
    {
        public bool Enabled { get; set; } = true;
        public XivChatType ChatType { get; set; } = XivChatType.Debug;
        public bool ShowTrayMessage { get; set; }
        public bool FlashTaskbar { get; set; }
    }

    internal sealed class AdvancedConfiguration
    {
        public bool DebugOverlay { get; set; }
        public bool CombatDataOverlay { get; set; }
        public bool NeverFly { get; set; }
        public bool AdditionalStatusInformation { get; set; }
        public bool DisableAutoDutyBareMode { get; set; }
        public bool SkipAetherCurrents { get; set; }
        public bool SkipClassJobQuests { get; set; }
        public bool SkipARealmRebornHardModePrimals { get; set; }
        public bool SkipCrystalTowerRaids { get; set; }
    }

    internal enum ECombatModule
    {
        None,
        BossMod,
        WrathCombo,
        RotationSolverReborn,
        AEAssist,
    }
}
