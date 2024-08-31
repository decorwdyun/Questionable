using System;
using System.Diagnostics.CodeAnalysis;
using Dalamud.Extensions.MicrosoftLogging;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller;
using Questionable.Controller.CombatModules;
using Questionable.Controller.GameUi;
using Questionable.Controller.NavigationOverrides;
using Questionable.Controller.Steps;
using Questionable.Controller.Steps.Shared;
using Questionable.Controller.Steps.Common;
using Questionable.Controller.Steps.Gathering;
using Questionable.Controller.Steps.Interactions;
using Questionable.Controller.Steps.Leves;
using Questionable.Data;
using Questionable.External;
using Questionable.Functions;
using Questionable.Validation;
using Questionable.Validation.Validators;
using Questionable.Windows;
using Questionable.Windows.JournalComponents;
using Questionable.Windows.QuestComponents;
using Action = Questionable.Controller.Steps.Interactions.Action;

namespace Questionable;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public sealed class QuestionablePlugin : IDalamudPlugin
{
    private readonly ServiceProvider? _serviceProvider;

    public QuestionablePlugin(IDalamudPluginInterface pluginInterface,
        IClientState clientState,
        ITargetManager targetManager,
        IFramework framework,
        IGameGui gameGui,
        IDataManager dataManager,
        ISigScanner sigScanner,
        IObjectTable objectTable,
        IPluginLog pluginLog,
        ICondition condition,
        IChatGui chatGui,
        ICommandManager commandManager,
        IAddonLifecycle addonLifecycle,
        IKeyState keyState,
        IContextMenu contextMenu,
        IToastGui toastGui,
        IGameInteropProvider gameInteropProvider)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        ArgumentNullException.ThrowIfNull(chatGui);
        try
        {
            ServiceCollection serviceCollection = new();
            serviceCollection.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace)
                .ClearProviders()
                .AddDalamudLogger(pluginLog, t => t[(t.LastIndexOf('.') + 1)..]));
            serviceCollection.AddSingleton<IDalamudPlugin>(this);
            serviceCollection.AddSingleton(pluginInterface);
            serviceCollection.AddSingleton(clientState);
            serviceCollection.AddSingleton(targetManager);
            serviceCollection.AddSingleton(framework);
            serviceCollection.AddSingleton(gameGui);
            serviceCollection.AddSingleton(dataManager);
            serviceCollection.AddSingleton(sigScanner);
            serviceCollection.AddSingleton(objectTable);
            serviceCollection.AddSingleton(pluginLog);
            serviceCollection.AddSingleton(condition);
            serviceCollection.AddSingleton(chatGui);
            serviceCollection.AddSingleton(commandManager);
            serviceCollection.AddSingleton(addonLifecycle);
            serviceCollection.AddSingleton(keyState);
            serviceCollection.AddSingleton(contextMenu);
            serviceCollection.AddSingleton(toastGui);
            serviceCollection.AddSingleton(gameInteropProvider);
            serviceCollection.AddSingleton(new WindowSystem(nameof(Questionable)));
            serviceCollection.AddSingleton((Configuration?)pluginInterface.GetPluginConfig() ?? new Configuration());

            AddBasicFunctionsAndData(serviceCollection);
            AddTaskFactories(serviceCollection);
            AddControllers(serviceCollection);
            AddWindows(serviceCollection);
            AddQuestValidators(serviceCollection);

            serviceCollection.AddSingleton<CommandHandler>();
            serviceCollection.AddSingleton<DalamudInitializer>();

            _serviceProvider = serviceCollection.BuildServiceProvider();
            Initialize(_serviceProvider);
        }
        catch (Exception)
        {
            chatGui.PrintError("Unable to load plugin, check /xllog for details", "Questionable");
            throw;
        }
    }

    private static void AddBasicFunctionsAndData(ServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<AetheryteFunctions>();
        serviceCollection.AddSingleton<ExcelFunctions>();
        serviceCollection.AddSingleton<GameFunctions>();
        serviceCollection.AddSingleton<ChatFunctions>();
        serviceCollection.AddSingleton<QuestFunctions>();

        serviceCollection.AddSingleton<AetherCurrentData>();
        serviceCollection.AddSingleton<AetheryteData>();
        serviceCollection.AddSingleton<GatheringData>();
        serviceCollection.AddSingleton<LeveData>();
        serviceCollection.AddSingleton<JournalData>();
        serviceCollection.AddSingleton<QuestData>();
        serviceCollection.AddSingleton<TerritoryData>();
        serviceCollection.AddSingleton<NavmeshIpc>();
        serviceCollection.AddSingleton<LifestreamIpc>();
        serviceCollection.AddSingleton<YesAlreadyIpc>();
        serviceCollection.AddSingleton<ArtisanIpc>();
        serviceCollection.AddSingleton<QuestionableIpc>();
    }

    private static void AddTaskFactories(ServiceCollection serviceCollection)
    {
        // individual tasks
        serviceCollection.AddTransient<MoveToLandingLocation>();
        serviceCollection.AddTransient<DoGather>();
        serviceCollection.AddTransient<DoGatherCollectable>();
        serviceCollection.AddTransient<SwitchClassJob>();
        serviceCollection.AddSingleton<Mount.Factory>();

        // task factories
        serviceCollection.AddTaskFactory<StepDisabled.Factory>();
        serviceCollection.AddTaskFactory<EquipRecommended.BeforeDutyOrInstance>();
        serviceCollection.AddTaskFactory<GatheringRequiredItems.Factory>();
        serviceCollection.AddTaskFactory<AetheryteShortcut.Factory>();
        serviceCollection.AddTaskFactory<SkipCondition.Factory>();
        serviceCollection.AddTaskFactory<AethernetShortcut.Factory>();
        serviceCollection.AddTaskFactory<WaitAtStart.Factory>();
        serviceCollection.AddTaskFactory<MoveTo.Factory>();

        serviceCollection.AddTaskFactory<NextQuest.Factory>();
        serviceCollection.AddTaskFactory<AetherCurrent.Factory>();
        serviceCollection.AddTaskFactory<AethernetShard.Factory>();
        serviceCollection.AddTaskFactory<Aetheryte.Factory>();
        serviceCollection.AddTaskFactory<Combat.Factory>();
        serviceCollection.AddTaskFactory<Duty.Factory>();
        serviceCollection.AddTaskFactory<Emote.Factory>();
        serviceCollection.AddTaskFactory<Action.Factory>();
        serviceCollection.AddTaskFactory<Interact.Factory>();
        serviceCollection.AddTaskFactory<Jump.Factory>();
        serviceCollection.AddTaskFactory<Dive.Factory>();
        serviceCollection.AddTaskFactory<Say.Factory>();
        serviceCollection.AddTaskFactory<UseItem.Factory>();
        serviceCollection.AddTaskFactory<EquipItem.Factory>();
        serviceCollection.AddTaskFactory<EquipRecommended.Factory>();
        serviceCollection.AddTaskFactory<Craft.Factory>();
        serviceCollection.AddTaskFactory<TurnInDelivery.Factory>();
        serviceCollection.AddTaskFactory<InitiateLeve.Factory>();

        serviceCollection.AddTaskFactory<WaitAtEnd.Factory>();
        serviceCollection.AddTransient<WaitAtEnd.WaitQuestAccepted>();
        serviceCollection.AddTransient<WaitAtEnd.WaitQuestCompleted>();

        serviceCollection.AddSingleton<TaskCreator>();
    }

    private static void AddControllers(ServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<MovementController>();
        serviceCollection.AddSingleton<MovementOverrideController>();
        serviceCollection.AddSingleton<GatheringPointRegistry>();
        serviceCollection.AddSingleton<QuestRegistry>();
        serviceCollection.AddSingleton<QuestController>();
        serviceCollection.AddSingleton<CombatController>();
        serviceCollection.AddSingleton<GatheringController>();
        serviceCollection.AddSingleton<ContextMenuController>();

        serviceCollection.AddSingleton<CraftworksSupplyController>();
        serviceCollection.AddSingleton<CreditsController>();
        serviceCollection.AddSingleton<HelpUiController>();
        serviceCollection.AddSingleton<InteractionUiController>();
        serviceCollection.AddSingleton<LeveUiController>();

        serviceCollection.AddSingleton<ICombatModule, RotationSolverRebornModule>();
    }

    private static void AddWindows(ServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<UiUtils>();

        serviceCollection.AddSingleton<ActiveQuestComponent>();
        serviceCollection.AddSingleton<ARealmRebornComponent>();
        serviceCollection.AddSingleton<CreationUtilsComponent>();
        serviceCollection.AddSingleton<EventInfoComponent>();
        serviceCollection.AddSingleton<QuestTooltipComponent>();
        serviceCollection.AddSingleton<QuickAccessButtonsComponent>();
        serviceCollection.AddSingleton<RemainingTasksComponent>();

        serviceCollection.AddSingleton<QuestJournalComponent>();
        serviceCollection.AddSingleton<GatheringJournalComponent>();

        serviceCollection.AddSingleton<QuestWindow>();
        serviceCollection.AddSingleton<ConfigWindow>();
        serviceCollection.AddSingleton<DebugOverlay>();
        serviceCollection.AddSingleton<QuestSelectionWindow>();
        serviceCollection.AddSingleton<QuestValidationWindow>();
        serviceCollection.AddSingleton<JournalProgressWindow>();
        serviceCollection.AddSingleton<PriorityWindow>();
    }

    private static void AddQuestValidators(ServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<QuestValidator>();
        serviceCollection.AddSingleton<IQuestValidator, QuestDisabledValidator>();
        serviceCollection.AddSingleton<IQuestValidator, BasicSequenceValidator>();
        serviceCollection.AddSingleton<IQuestValidator, UniqueStartStopValidator>();
        serviceCollection.AddSingleton<IQuestValidator, NextQuestValidator>();
        serviceCollection.AddSingleton<IQuestValidator, CompletionFlagsValidator>();
        serviceCollection.AddSingleton<IQuestValidator, AethernetShortcutValidator>();
        serviceCollection.AddSingleton<IQuestValidator, DialogueChoiceValidator>();
        serviceCollection.AddSingleton<JsonSchemaValidator>();
        serviceCollection.AddSingleton<IQuestValidator>(sp => sp.GetRequiredService<JsonSchemaValidator>());
    }

    private static void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.GetRequiredService<QuestRegistry>().Reload();
        serviceProvider.GetRequiredService<GatheringPointRegistry>().Reload();
        serviceProvider.GetRequiredService<CommandHandler>();
        serviceProvider.GetRequiredService<ContextMenuController>();
        serviceProvider.GetRequiredService<CraftworksSupplyController>();
        serviceProvider.GetRequiredService<CreditsController>();
        serviceProvider.GetRequiredService<HelpUiController>();
        serviceProvider.GetRequiredService<LeveUiController>();
        serviceProvider.GetRequiredService<QuestionableIpc>();
        serviceProvider.GetRequiredService<DalamudInitializer>();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
