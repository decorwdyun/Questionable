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
using Questionable.Controller.NavigationOverrides;
using Questionable.Controller.Steps.Shared;
using Questionable.Controller.Steps.Common;
using Questionable.Controller.Steps.Interactions;
using Questionable.Data;
using Questionable.External;
using Questionable.Validation;
using Questionable.Validation.Validators;
using Questionable.Windows;
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
        IKeyState keyState)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);

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
        _serviceProvider.GetRequiredService<QuestRegistry>().Reload();
        _serviceProvider.GetRequiredService<CommandHandler>();
        _serviceProvider.GetRequiredService<DalamudInitializer>();
    }

    private static void AddBasicFunctionsAndData(ServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<GameFunctions>();
        serviceCollection.AddSingleton<ChatFunctions>();
        serviceCollection.AddSingleton<AetherCurrentData>();
        serviceCollection.AddSingleton<AetheryteData>();
        serviceCollection.AddSingleton<QuestData>();
        serviceCollection.AddSingleton<TerritoryData>();
        serviceCollection.AddSingleton<NavmeshIpc>();
        serviceCollection.AddSingleton<LifestreamIpc>();
        serviceCollection.AddSingleton<YesAlreadyIpc>();
    }

    private static void AddTaskFactories(ServiceCollection serviceCollection)
    {
        // individual tasks
        serviceCollection.AddTransient<MountTask>();
        serviceCollection.AddTransient<UnmountTask>();

        // task factories
        serviceCollection.AddTaskWithFactory<StepDisabled.Factory, StepDisabled.Task>();
        serviceCollection.AddTaskWithFactory<AetheryteShortcut.Factory, AetheryteShortcut.UseAetheryteShortcut>();
        serviceCollection.AddTaskWithFactory<SkipCondition.Factory, SkipCondition.CheckTask>();
        serviceCollection.AddTaskWithFactory<AethernetShortcut.Factory, AethernetShortcut.UseAethernetShortcut>();
        serviceCollection.AddTaskWithFactory<WaitAtStart.Factory, WaitAtStart.WaitDelay>();
        serviceCollection.AddTaskWithFactory<Move.Factory, Move.MoveInternal, Move.ExpectToBeNearDataId, Move.Land>();
        serviceCollection.AddTransient<Move.MoveBuilder>();

        serviceCollection.AddTaskWithFactory<NextQuest.Factory, NextQuest.SetQuest>();
        serviceCollection.AddTaskWithFactory<AetherCurrent.Factory, AetherCurrent.DoAttune>();
        serviceCollection.AddTaskWithFactory<AethernetShard.Factory, AethernetShard.DoAttune>();
        serviceCollection.AddTaskWithFactory<Aetheryte.Factory, Aetheryte.DoAttune>();
        serviceCollection.AddTaskWithFactory<Combat.Factory, Combat.HandleCombat>();
        serviceCollection.AddTaskWithFactory<Duty.Factory, Duty.OpenDutyFinder>();
        serviceCollection.AddTaskWithFactory<Emote.Factory, Emote.UseOnObject, Emote.Use>();
        serviceCollection.AddTaskWithFactory<Action.Factory, Action.UseOnObject>();
        serviceCollection.AddTaskWithFactory<Interact.Factory, Interact.DoInteract>();
        serviceCollection.AddTaskWithFactory<Jump.Factory, Jump.SingleJump, Jump.RepeatedJumps>();
        serviceCollection.AddTaskWithFactory<Dive.Factory, Dive.DoDive>();
        serviceCollection.AddTaskWithFactory<Say.Factory, Say.UseChat>();
        serviceCollection.AddTaskWithFactory<UseItem.Factory, UseItem.UseOnGround, UseItem.UseOnObject, UseItem.Use, UseItem.UseOnPosition>();
        serviceCollection.AddTaskWithFactory<EquipItem.Factory, EquipItem.DoEquip>();
        serviceCollection
            .AddTaskWithFactory<SinglePlayerDuty.Factory, SinglePlayerDuty.DisableYesAlready,
                SinglePlayerDuty.RestoreYesAlready>();

        serviceCollection
            .AddTaskWithFactory<WaitAtEnd.Factory,
                WaitAtEnd.WaitDelay,
                WaitAtEnd.WaitNextStepOrSequence,
                WaitAtEnd.WaitForCompletionFlags,
                WaitAtEnd.WaitObjectAtPosition>();
        serviceCollection.AddTransient<WaitAtEnd.WaitQuestAccepted>();
        serviceCollection.AddTransient<WaitAtEnd.WaitQuestCompleted>();
    }

    private static void AddControllers(ServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<MovementController>();
        serviceCollection.AddSingleton<MovementOverrideController>();
        serviceCollection.AddSingleton<QuestRegistry>();
        serviceCollection.AddSingleton<QuestController>();
        serviceCollection.AddSingleton<GameUiController>();
        serviceCollection.AddSingleton<NavigationShortcutController>();
        serviceCollection.AddSingleton<CombatController>();

        serviceCollection.AddSingleton<ICombatModule, RotationSolverRebornModule>();
    }

    private static void AddWindows(ServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<UiUtils>();

        serviceCollection.AddSingleton<ActiveQuestComponent>();
        serviceCollection.AddSingleton<ARealmRebornComponent>();
        serviceCollection.AddSingleton<CreationUtilsComponent>();
        serviceCollection.AddSingleton<QuickAccessButtonsComponent>();
        serviceCollection.AddSingleton<RemainingTasksComponent>();

        serviceCollection.AddSingleton<QuestWindow>();
        serviceCollection.AddSingleton<ConfigWindow>();
        serviceCollection.AddSingleton<DebugOverlay>();
        serviceCollection.AddSingleton<QuestSelectionWindow>();
        serviceCollection.AddSingleton<QuestValidationWindow>();
    }

    private static void AddQuestValidators(ServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<QuestValidator>();
        serviceCollection.AddSingleton<IQuestValidator, QuestDisabledValidator>();
        serviceCollection.AddSingleton<IQuestValidator, BasicSequenceValidator>();
        serviceCollection.AddSingleton<IQuestValidator, UniqueStartStopValidator>();
        serviceCollection.AddSingleton<IQuestValidator, NextQuestValidator>();
        serviceCollection.AddSingleton<IQuestValidator, CompletionFlagsValidator>();
        serviceCollection.AddSingleton<JsonSchemaValidator>();
        serviceCollection.AddSingleton<IQuestValidator>(sp => sp.GetRequiredService<JsonSchemaValidator>());
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
