using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Schedulers;
using ECommons.SplatoonAPI;
using Questionable.Model.Gathering;

namespace GatheringPathRenderer;

public sealed class RendererPlugin : IDalamudPlugin
{
    private const long OnTerritoryChange = -2;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IClientState _clientState;
    private readonly IPluginLog _pluginLog;
    private readonly List<(ushort Id, GatheringRoot Root)> _gatheringLocations = [];

    public RendererPlugin(IDalamudPluginInterface pluginInterface, IClientState clientState, IPluginLog pluginLog)
    {
        _pluginInterface = pluginInterface;
        _clientState = clientState;
        _pluginLog = pluginLog;

        _pluginInterface.GetIpcSubscriber<object>("Questionable.ReloadData")
            .Subscribe(Reload);

        ECommonsMain.Init(pluginInterface, this, Module.SplatoonAPI);
        LoadGatheringLocationsFromDirectory();

        _clientState.TerritoryChanged += TerritoryChanged;
        if (_clientState.IsLoggedIn)
            TerritoryChanged(_clientState.TerritoryType);
    }

    private void Reload()
    {
        LoadGatheringLocationsFromDirectory();
        TerritoryChanged(_clientState.TerritoryType);
    }

    private void LoadGatheringLocationsFromDirectory()
    {
        _gatheringLocations.Clear();

        DirectoryInfo? solutionDirectory = _pluginInterface.AssemblyLocation.Directory?.Parent?.Parent?.Parent;
        if (solutionDirectory != null)
        {
            DirectoryInfo pathProjectDirectory =
                new DirectoryInfo(Path.Combine(solutionDirectory.FullName, "GatheringPaths"));
            if (pathProjectDirectory.Exists)
            {
                try
                {
                    LoadFromDirectory(
                        new DirectoryInfo(Path.Combine(pathProjectDirectory.FullName, "2.x - A Realm Reborn")));
                    LoadFromDirectory(
                        new DirectoryInfo(Path.Combine(pathProjectDirectory.FullName, "3.x - Heavensward")));
                    LoadFromDirectory(
                        new DirectoryInfo(Path.Combine(pathProjectDirectory.FullName, "4.x - Stormblood")));
                    LoadFromDirectory(
                        new DirectoryInfo(Path.Combine(pathProjectDirectory.FullName, "5.x - Shadowbringers")));
                    LoadFromDirectory(
                        new DirectoryInfo(Path.Combine(pathProjectDirectory.FullName, "6.x - Endwalker")));
                    LoadFromDirectory(
                        new DirectoryInfo(Path.Combine(pathProjectDirectory.FullName, "7.x - Dawntrail")));

                    _pluginLog.Information(
                        $"Loaded {_gatheringLocations.Count} gathering root locations from project directory");
                }
                catch (Exception e)
                {
                    _pluginLog.Error(e, "Failed to load quests from project directory");
                }
            }
            else
                _pluginLog.Warning($"Project directory {pathProjectDirectory} does not exist");
        }
        else
            _pluginLog.Warning($"Solution directory {solutionDirectory} does not exist");
    }

    private void LoadFromDirectory(DirectoryInfo directory)
    {
        if (!directory.Exists)
            return;

        _pluginLog.Information($"Loading locations from {directory}");
        foreach (FileInfo fileInfo in directory.GetFiles("*.json"))
        {
            try
            {
                using FileStream stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
                LoadLocationFromStream(fileInfo.Name, stream);
            }
            catch (Exception e)
            {
                throw new InvalidDataException($"Unable to load file {fileInfo.FullName}", e);
            }
        }

        foreach (DirectoryInfo childDirectory in directory.GetDirectories())
            LoadFromDirectory(childDirectory);
    }

    private void LoadLocationFromStream(string fileName, Stream stream)
    {
        var locationNode = JsonNode.Parse(stream)!;
        GatheringRoot root = locationNode.Deserialize<GatheringRoot>()!;
        _gatheringLocations.Add((ushort.Parse(fileName.Split('_')[0]), root));
    }

    private void TerritoryChanged(ushort territoryId)
    {
        Splatoon.RemoveDynamicElements("GatheringPathRenderer");

        var elements = _gatheringLocations
            .Where(x => x.Root.TerritoryId == territoryId)
            .SelectMany(v =>
                v.Root.Groups.SelectMany(group =>
                    group.Nodes.SelectMany(node => node.Locations
                        .SelectMany(x =>
                            new List<Element>
                            {
                                new Element(x.IsCone()
                                    ? ElementType.ConeAtFixedCoordinates
                                    : ElementType.CircleAtFixedCoordinates)
                                {
                                    refX = x.Position.X,
                                    refY = x.Position.Z,
                                    refZ = x.Position.Y,
                                    Filled = true,
                                    radius = x.MinimumDistance,
                                    Donut = x.MaximumDistance - x.MinimumDistance,
                                    color = 0x2020FF80,
                                    Enabled = true,
                                    coneAngleMin = x.IsCone() ? (int)x.MinimumAngle.GetValueOrDefault() : 0,
                                    coneAngleMax = x.IsCone() ? (int)x.MaximumAngle.GetValueOrDefault() : 0
                                },
                                new Element(ElementType.CircleAtFixedCoordinates)
                                {
                                    refX = x.Position.X,
                                    refY = x.Position.Z,
                                    refZ = x.Position.Y,
                                    color = 0x00000000,
                                    Enabled = true,
                                    overlayText = $"{v.Id} // {node.DataId} / {node.Locations.IndexOf(x)}"
                                }
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
        _clientState.TerritoryChanged -= TerritoryChanged;

        Splatoon.RemoveDynamicElements("GatheringPathRenderer");
        ECommonsMain.Dispose();

        _pluginInterface.GetIpcSubscriber<object>("Questionable.ReloadData")
            .Unsubscribe(Reload);
    }
}
