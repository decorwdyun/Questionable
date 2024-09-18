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
using Questionable.Controller.Utils;
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
        serviceCollection.AddSingleton<AutoSnipeHandler>();

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
        serviceCollection.AddTaskExecutor<MoveToLandingLocation.Task, MoveToLandingLocation.Executor>();
        serviceCollection.AddTaskExecutor<DoGather.Task, DoGather.Executor>();
        serviceCollection.AddTaskExecutor<DoGatherCollectable.Task, DoGatherCollectable.Executor>();
        serviceCollection.AddTaskExecutor<SwitchClassJob.Task, SwitchClassJob.Executor>();
        serviceCollection.AddTaskExecutor<Mount.MountTask, Mount.MountExecutor>();
        serviceCollection.AddTaskExecutor<Mount.UnmountTask, Mount.UnmountExecutor>();

        // task factories
        serviceCollection
            .AddTaskFactoryAndExecutor<StepDisabled.SkipRemainingTasks, StepDisabled.Factory, StepDisabled.Executor>();
        serviceCollection.AddTaskFactory<EquipRecommended.BeforeDutyOrInstance>();
        serviceCollection.AddTaskFactoryAndExecutor<Gather.GatheringTask, Gather.Factory, Gather.StartGathering>();
        serviceCollection.AddTaskExecutor<Gather.SkipMarker, Gather.DoSkip>();
        serviceCollection
            .AddTaskFactoryAndExecutor<AetheryteShortcut.Task, AetheryteShortcut.Factory,
                AetheryteShortcut.UseAetheryteShortcut>();
        serviceCollection
            .AddTaskFactoryAndExecutor<SkipCondition.SkipTask, SkipCondition.Factory, SkipCondition.CheckSkip>();
        serviceCollection
            .AddTaskFactoryAndExecutor<AethernetShortcut.Task, AethernetShortcut.Factory,
                AethernetShortcut.UseAethernetShortcut>();
        serviceCollection
            .AddTaskFactoryAndExecutor<WaitAtStart.WaitDelay, WaitAtStart.Factory, WaitAtStart.WaitDelayExecutor>();
        serviceCollection.AddTaskFactoryAndExecutor<MoveTo.MoveTask, MoveTo.Factory, MoveTo.MoveExecutor>();
        serviceCollection.AddTaskExecutor<MoveTo.WaitForNearDataId, MoveTo.WaitForNearDataIdExecutor>();
        serviceCollection.AddTaskExecutor<MoveTo.LandTask, MoveTo.LandExecutor>();

        serviceCollection.AddTaskFactoryAndExecutor<NextQuest.SetQuestTask, NextQuest.Factory, NextQuest.Executor>();
        serviceCollection
            .AddTaskFactoryAndExecutor<AetherCurrent.Attune, AetherCurrent.Factory, AetherCurrent.DoAttune>();
        serviceCollection
            .AddTaskFactoryAndExecutor<AethernetShard.Attune, AethernetShard.Factory, AethernetShard.DoAttune>();
        serviceCollection.AddTaskFactoryAndExecutor<Aetheryte.Attune, Aetheryte.Factory, Aetheryte.DoAttune>();
        serviceCollection.AddTaskFactoryAndExecutor<Combat.Task, Combat.Factory, Combat.HandleCombat>();
        serviceCollection.AddTaskFactoryAndExecutor<Duty.Task, Duty.Factory, Duty.Executor>();
        serviceCollection.AddTaskFactory<Emote.Factory>();
        serviceCollection.AddTaskExecutor<Emote.UseOnObject, Emote.UseOnObjectExecutor>();
        serviceCollection.AddTaskExecutor<Emote.UseOnSelf, Emote.UseOnSelfExecutor>();
        serviceCollection.AddTaskFactoryAndExecutor<Action.UseOnObject, Action.Factory, Action.UseOnObjectExecutor>();
        serviceCollection.AddTaskFactoryAndExecutor<Interact.Task, Interact.Factory, Interact.DoInteract>();
        serviceCollection.AddTaskFactory<Jump.Factory>();
        serviceCollection.AddTaskExecutor<Jump.SingleJumpTask, Jump.DoSingleJump>();
        serviceCollection.AddTaskExecutor<Jump.RepeatedJumpTask, Jump.DoRepeatedJumps>();
        serviceCollection.AddTaskFactoryAndExecutor<Dive.Task, Dive.Factory, Dive.DoDive>();
        serviceCollection.AddTaskFactoryAndExecutor<Say.Task, Say.Factory, Say.UseChat>();
        serviceCollection.AddTaskFactory<UseItem.Factory>();
        serviceCollection.AddTaskExecutor<UseItem.UseOnGround, UseItem.UseOnGroundExecutor>();
        serviceCollection.AddTaskExecutor<UseItem.UseOnPosition, UseItem.UseOnPositionExecutor>();
        serviceCollection.AddTaskExecutor<UseItem.UseOnObject, UseItem.UseOnObjectExecutor>();
        serviceCollection.AddTaskExecutor<UseItem.UseOnSelf, UseItem.UseOnSelfExecutor>();
        serviceCollection.AddTaskFactoryAndExecutor<EquipItem.Task, EquipItem.Factory, EquipItem.Executor>();
        serviceCollection
            .AddTaskFactoryAndExecutor<EquipRecommended.EquipTask, EquipRecommended.Factory,
                EquipRecommended.DoEquipRecommended>();
        serviceCollection.AddTaskFactoryAndExecutor<Craft.CraftTask, Craft.Factory, Craft.DoCraft>();
        serviceCollection
            .AddTaskFactoryAndExecutor<TurnInDelivery.Task, TurnInDelivery.Factory,
                TurnInDelivery.SatisfactionSupplyTurnIn>();

        serviceCollection.AddTaskFactory<InitiateLeve.Factory>();
        serviceCollection.AddTaskExecutor<InitiateLeve.SkipInitiateIfActive, InitiateLeve.SkipInitiateIfActiveExecutor>();
        serviceCollection.AddTaskExecutor<InitiateLeve.OpenJournal, InitiateLeve.OpenJournalExecutor>();
        serviceCollection.AddTaskExecutor<InitiateLeve.Initiate, InitiateLeve.InitiateExecutor>();
        serviceCollection.AddTaskExecutor<InitiateLeve.SelectDifficulty, InitiateLeve.SelectDifficultyExecutor>();

        serviceCollection.AddTaskExecutor<WaitCondition.Task, WaitCondition.Executor>();
        serviceCollection.AddTaskFactory<WaitAtEnd.Factory>();
        serviceCollection.AddTaskExecutor<WaitAtEnd.WaitDelay, WaitAtEnd.WaitDelayExecutor>();
        serviceCollection.AddTaskExecutor<WaitAtEnd.WaitNextStepOrSequence, WaitAtEnd.WaitNextStepOrSequenceExecutor>();
        serviceCollection.AddTaskExecutor<WaitAtEnd.WaitForCompletionFlags, WaitAtEnd.WaitForCompletionFlagsExecutor>();
        serviceCollection.AddTaskExecutor<WaitAtEnd.WaitObjectAtPosition, WaitAtEnd.WaitObjectAtPositionExecutor>();
        serviceCollection.AddTaskExecutor<WaitAtEnd.WaitQuestAccepted, WaitAtEnd.WaitQuestAcceptedExecutor>();
        serviceCollection.AddTaskExecutor<WaitAtEnd.WaitQuestCompleted, WaitAtEnd.WaitQuestCompletedExecutor>();
        serviceCollection.AddTaskExecutor<WaitAtEnd.NextStep, WaitAtEnd.NextStepExecutor>();
        serviceCollection.AddTaskExecutor<WaitAtEnd.EndAutomation, WaitAtEnd.EndAutomationExecutor>();

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

        serviceCollection.AddSingleton<ICombatModule, Mount128Module>();
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
        serviceCollection.AddSingleton<IQuestValidator, ClassQuestShouldHaveShortcutValidator>();
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
        serviceProvider.GetRequiredService<AutoSnipeHandler>().Enable();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
