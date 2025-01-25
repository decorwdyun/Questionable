using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Schedulers;
using ECommons.SplatoonAPI;
using GatheringPathRenderer.Windows;
using LLib.GameData;
using Questionable.Model.Gathering;

namespace GatheringPathRenderer;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class RendererPlugin : IDalamudPlugin
{
    private const long OnTerritoryChange = -2;

    private readonly WindowSystem _windowSystem = new(nameof(RendererPlugin));
    private readonly List<uint> _colors = [0xFFFF2020, 0xFF20FF20, 0xFF2020FF, 0xFFFFFF20, 0xFFFF20FF, 0xFF20FFFF];

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IClientState _clientState;
    private readonly IPluginLog _pluginLog;

    private readonly EditorCommands _editorCommands;
    private readonly EditorWindow _editorWindow;

    private readonly List<GatheringLocationContext> _gatheringLocations = [];
    private EClassJob _currentClassJob;

    public RendererPlugin(IDalamudPluginInterface pluginInterface, IClientState clientState,
        ICommandManager commandManager, IDataManager dataManager, ITargetManager targetManager, IChatGui chatGui,
        IObjectTable objectTable, IPluginLog pluginLog)
    {
        _pluginInterface = pluginInterface;
        _clientState = clientState;
        _pluginLog = pluginLog;

        Configuration? configuration = (Configuration?)pluginInterface.GetPluginConfig();
        if (configuration == null)
        {
            configuration = new Configuration();
            pluginInterface.SavePluginConfig(configuration);
        }

        _editorCommands = new EditorCommands(this, dataManager, commandManager, targetManager, clientState, chatGui,
            configuration);
        var configWindow = new ConfigWindow(pluginInterface, configuration);
        _editorWindow = new EditorWindow(this, _editorCommands, dataManager, targetManager, clientState, objectTable, configWindow)
            { IsOpen = true };
        _windowSystem.AddWindow(configWindow);
        _windowSystem.AddWindow(_editorWindow);
        _currentClassJob = (EClassJob?)_clientState.LocalPlayer?.ClassJob.RowId ?? EClassJob.Adventurer;

        _pluginInterface.GetIpcSubscriber<object>("Questionable.ReloadData")
            .Subscribe(Reload);

        ECommonsMain.Init(pluginInterface, this, Module.SplatoonAPI);
        LoadGatheringLocationsFromDirectory();

        _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _clientState.TerritoryChanged += TerritoryChanged;
        _clientState.ClassJobChanged += ClassJobChanged;
        if (_clientState.IsLoggedIn)
            TerritoryChanged(_clientState.TerritoryType);
    }

    internal DirectoryInfo PathsDirectory
    {
        get
        {
#if DEBUG
            DirectoryInfo? solutionDirectory = _pluginInterface.AssemblyLocation.Directory?.Parent?.Parent;
            if (solutionDirectory != null)
            {
                DirectoryInfo pathProjectDirectory =
                    new DirectoryInfo(Path.Combine(solutionDirectory.FullName, "GatheringPaths"));
                if (pathProjectDirectory.Exists)
                    return pathProjectDirectory;
            }

            throw new Exception($"Unable to resolve project path ({_pluginInterface.AssemblyLocation.Directory})");
#else
            var allPluginsDirectory = _pluginInterface.ConfigFile.Directory ?? throw new Exception("Unknown directory for plugin configs");
            return allPluginsDirectory
                .CreateSubdirectory("Questionable")
                .CreateSubdirectory("GatheringPaths");
#endif
        }
    }

    internal void Reload()
    {
        LoadGatheringLocationsFromDirectory();
        Redraw();
    }

    private void LoadGatheringLocationsFromDirectory()
    {
        _gatheringLocations.Clear();

        try
        {
#if DEBUG
            foreach (var expansionFolder in Questionable.Model.ExpansionData.ExpansionFolders.Values)
                LoadFromDirectory(
                    new DirectoryInfo(Path.Combine(PathsDirectory.FullName, expansionFolder)));
            _pluginLog.Information(
                $"Loaded {_gatheringLocations.Count} gathering root locations from project directory");
#else
            LoadFromDirectory(PathsDirectory);
            _pluginLog.Information(
                $"Loaded {_gatheringLocations.Count} gathering root locations from {PathsDirectory.FullName} directory");
#endif

        }
        catch (Exception e)
        {
            _pluginLog.Error(e, "Failed to load paths from project directory");
        }
    }

    private void LoadFromDirectory(DirectoryInfo directory)
    {
        if (!directory.Exists)
            return;

        //_pluginLog.Information($"Loading locations from {directory}");
        foreach (FileInfo fileInfo in directory.GetFiles("*.json"))
        {
            try
            {
                using FileStream stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
                LoadLocationFromStream(fileInfo, stream);
            }
            catch (Exception e)
            {
                throw new InvalidDataException($"Unable to load file {fileInfo.FullName}", e);
            }
        }

        foreach (DirectoryInfo childDirectory in directory.GetDirectories())
            LoadFromDirectory(childDirectory);
    }

    private void LoadLocationFromStream(FileInfo fileInfo, Stream stream)
    {
        var locationNode = JsonNode.Parse(stream)!;
        GatheringRoot root = locationNode.Deserialize<GatheringRoot>()!;
        _gatheringLocations.Add(new GatheringLocationContext(fileInfo, ushort.Parse(fileInfo.Name.Split('_')[0]),
            root));
    }

    internal IEnumerable<GatheringLocationContext> GetLocationsInTerritory(ushort territoryId)
        => _gatheringLocations.Where(x => x.Root.Steps.LastOrDefault()?.TerritoryId == territoryId);

    internal void Save(FileInfo targetFile, GatheringRoot root)
    {
        JsonSerializerOptions options = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { NoEmptyCollectionModifier }
            },
        };
        using (var stream = File.Create(targetFile.FullName))
        {
            var jsonNode = (JsonObject)JsonSerializer.SerializeToNode(root, options)!;
            var newNode = new JsonObject();
            newNode.Add("$schema",
                "https://git.carvel.li/liza/Questionable/raw/branch/master/GatheringPaths/gatheringlocation-v1.json");
            foreach (var (key, value) in jsonNode)
                newNode.Add(key, value?.DeepClone());

            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = true
            });
            newNode.WriteTo(writer, options);
        }

        Reload();
    }

    private static void NoEmptyCollectionModifier(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties)
        {
            if (typeof(ICollection).IsAssignableFrom(property.PropertyType))
            {
                property.ShouldSerialize = (_, val) => val is ICollection { Count: > 0 };
            }
        }
    }

    private void TerritoryChanged(ushort territoryId) => Redraw();

    private void ClassJobChanged(uint classJobId)
    {
        _currentClassJob = (EClassJob)classJobId;
        Redraw(_currentClassJob);
    }

    internal void Redraw() => Redraw(_currentClassJob);

    private void Redraw(EClassJob classJob)
    {
        Splatoon.RemoveDynamicElements("GatheringPathRenderer");
        if (!classJob.IsGatherer())
            return;

        var elements = GetLocationsInTerritory(_clientState.TerritoryType)
            .SelectMany(location =>
                location.Root.Groups.SelectMany(group =>
                    group.Nodes.SelectMany(node => node.Locations
                        .SelectMany(x =>
                        {
                            bool isUnsaved = false;
                            bool isCone = false;
                            int minimumAngle = 0;
                            int maximumAngle = 0;
                            if (_editorWindow.TryGetOverride(x.InternalId, out LocationOverride? locationOverride) &&
                                locationOverride != null)
                            {
                                isUnsaved = locationOverride.NeedsSave();
                                if (locationOverride.IsCone())
                                {
                                    isCone = true;
                                    minimumAngle = locationOverride.MinimumAngle.GetValueOrDefault();
                                    maximumAngle = locationOverride.MaximumAngle.GetValueOrDefault();
                                }
                            }

                            if (!isCone && x.IsCone())
                            {
                                isCone = true;
                                minimumAngle = x.MinimumAngle.GetValueOrDefault();
                                maximumAngle = x.MaximumAngle.GetValueOrDefault();
                            }

#if false
                            var a = GatheringMath.CalculateLandingLocation(x, 0, 0);
                            var b = GatheringMath.CalculateLandingLocation(x, 1, 1);
#endif
                            return new List<Element>
                            {
                                new Element(isCone
                                    ? ElementType.ConeAtFixedCoordinates
                                    : ElementType.CircleAtFixedCoordinates)
                                {
                                    refX = x.Position.X,
                                    refY = x.Position.Z,
                                    refZ = x.Position.Y,
                                    Filled = true,
                                    radius = locationOverride?.MinimumDistance ?? x.CalculateMinimumDistance(),
                                    Donut = (locationOverride?.MaximumDistance ?? x.CalculateMaximumDistance()) -
                                            (locationOverride?.MinimumDistance ?? x.CalculateMinimumDistance()),
                                    color = _colors[location.Root.Groups.IndexOf(group) % _colors.Count],
                                    Enabled = true,
                                    coneAngleMin = minimumAngle,
                                    coneAngleMax = maximumAngle,
                                    tether = false,
                                },
                                new Element(ElementType.CircleAtFixedCoordinates)
                                {
                                    refX = x.Position.X,
                                    refY = x.Position.Z,
                                    refZ = x.Position.Y,
                                    color = 0xFFFFFFFF,
                                    radius = 0.1f,
                                    Enabled = true,
                                    overlayText =
                                        $"{location.Root.Groups.IndexOf(group)} // {node.DataId} / {node.Locations.IndexOf(x)}",
                                    overlayBGColor = isUnsaved ? 0xFF2020FF : 0xFF000000,
                                },
#if false
                                new Element(ElementType.CircleAtFixedCoordinates)
                                {
                                    refX = a.X,
                                    refY = a.Z,
                                    refZ = a.Y,
                                    color = _colors[0],
                                    radius = 0.1f,
                                    Enabled = true,
                                    overlayText = "Min Angle"
                                },
                                new Element(ElementType.CircleAtFixedCoordinates)
                                {
                                    refX = b.X,
                                    refY = b.Z,
                                    refZ = b.Y,
                                    color = _colors[1],
                                    radius = 0.1f,
                                    Enabled = true,
                                    overlayText = "Max Angle"
                                }
#endif
                            };
                        }))))
            .ToList();

        if (elements.Count == 0)
        {
            _pluginLog.Information("No new elements to render.");
            return;
        }

        _ = new TickScheduler(delegate
        {
            try
            {
                Splatoon.AddDynamicElements("GatheringPathRenderer",
                    elements.ToArray(),
                    new[] { OnTerritoryChange });
                _pluginLog.Information($"Created {elements.Count} splatoon elements.");
            }
            catch (Exception e)
            {
                _pluginLog.Error(e, "Unable to create splatoon layer");
            }
        });
    }

    public void Dispose()
    {
        _clientState.ClassJobChanged -= ClassJobChanged;
        _clientState.TerritoryChanged -= TerritoryChanged;
        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;

        Splatoon.RemoveDynamicElements("GatheringPathRenderer");
        ECommonsMain.Dispose();

        _pluginInterface.GetIpcSubscriber<object>("Questionable.ReloadData")
            .Unsubscribe(Reload);

        _editorCommands.Dispose();
    }

    internal sealed record GatheringLocationContext(FileInfo File, ushort Id, GatheringRoot Root);
}
