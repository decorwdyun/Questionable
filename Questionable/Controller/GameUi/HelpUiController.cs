using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Microsoft.Extensions.Logging;

namespace Questionable.Controller.GameUi;

internal sealed class HelpUiController : IDisposable
{
    private readonly QuestController _questController;
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly ILogger<HelpUiController> _logger;

    public HelpUiController(QuestController questController, IAddonLifecycle addonLifecycle, ILogger<HelpUiController> logger)
    {
        _questController = questController;
        _addonLifecycle = addonLifecycle;
        _logger = logger;

        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "AkatsukiNote", UnendingCodexPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "ContentsTutorial", ContentsTutorialPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "MultipleHelpWindow", MultipleHelpWindowPostSetup);
    }

    private unsafe void UnendingCodexPostSetup(AddonEvent type, AddonArgs args)
    {
        if (_questController.StartedQuest?.Quest.Id.Value == 4526)
        {
            _logger.LogInformation("Closing Unending Codex");
            AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
            addon->FireCallbackInt(-2);
        }
    }

    private unsafe void ContentsTutorialPostSetup(AddonEvent type, AddonArgs args)
    {
        if (_questController.StartedQuest?.Quest.Id.Value == 245)
        {
            _logger.LogInformation("Closing ContentsTutorial");
            AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
            addon->FireCallbackInt(13);
        }
    }

    /// <summary>
    /// Opened e.g. the first time you open the duty finder window during Sastasha.
    /// </summary>
    private unsafe void MultipleHelpWindowPostSetup(AddonEvent type, AddonArgs args)
    {
        if (_questController.StartedQuest?.Quest.Id.Value == 245)
        {
            _logger.LogInformation("Closing MultipleHelpWindow");
            AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
            addon->FireCallbackInt(-2);
            addon->FireCallbackInt(-1);
        }
    }

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "MultipleHelpWindow", MultipleHelpWindowPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "ContentsTutorial", ContentsTutorialPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "AkatsukiNote", UnendingCodexPostSetup);
    }
}
