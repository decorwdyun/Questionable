﻿using System.Collections.Generic;
using Questionable.Model.Common.Converter;

namespace Questionable.Model.Questing.Converter;

internal sealed class ExtendedClassJobConverter() : EnumConverter<EExtendedClassJob>(Values)
{
    private static readonly Dictionary<EExtendedClassJob, string> Values = new()
    {
        { EExtendedClassJob.None, "None" },
        { EExtendedClassJob.Gladiator, "Gladiator" },
        { EExtendedClassJob.Pugilist, "Pugilist" },
        { EExtendedClassJob.Marauder, "Marauder" },
        { EExtendedClassJob.Lancer, "Lancer" },
        { EExtendedClassJob.Archer, "Archer" },
        { EExtendedClassJob.Conjurer, "Conjurer" },
        { EExtendedClassJob.Thaumaturge, "Thaumaturge" },
        { EExtendedClassJob.Carpenter, "Carpenter" },
        { EExtendedClassJob.Blacksmith, "Blacksmith" },
        { EExtendedClassJob.Armorer, "Armorer" },
        { EExtendedClassJob.Goldsmith, "Goldsmith" },
        { EExtendedClassJob.Leatherworker, "Leatherworker" },
        { EExtendedClassJob.Weaver, "Weaver" },
        { EExtendedClassJob.Alchemist, "Alchemist" },
        { EExtendedClassJob.Culinarian, "Culinarian" },
        { EExtendedClassJob.Miner, "Miner" },
        { EExtendedClassJob.Botanist, "Botanist" },
        { EExtendedClassJob.Fisher, "Fisher" },
        { EExtendedClassJob.Paladin, "Paladin" },
        { EExtendedClassJob.Monk, "Monk" },
        { EExtendedClassJob.Warrior, "Warrior" },
        { EExtendedClassJob.Dragoon, "Dragoon" },
        { EExtendedClassJob.Bard, "Bard" },
        { EExtendedClassJob.WhiteMage, "White Mage" },
        { EExtendedClassJob.BlackMage, "Black Mage" },
        { EExtendedClassJob.Arcanist, "Arcanist" },
        { EExtendedClassJob.Summoner, "Summoner" },
        { EExtendedClassJob.Scholar, "Scholar" },
        { EExtendedClassJob.Rogue, "Rogue" },
        { EExtendedClassJob.Ninja, "Ninja" },
        { EExtendedClassJob.Machinist, "Machinist" },
        { EExtendedClassJob.DarkKnight, "Dark Knight" },
        { EExtendedClassJob.Astrologian, "Astrologian" },
        { EExtendedClassJob.Samurai, "Samurai" },
        { EExtendedClassJob.RedMage, "Red Mage" },
        { EExtendedClassJob.BlueMage, "Blue Mage" },
        { EExtendedClassJob.Gunbreaker, "Gunbreaker" },
        { EExtendedClassJob.Dancer, "Dancer" },
        { EExtendedClassJob.Reaper, "Reaper" },
        { EExtendedClassJob.Sage, "Sage" },
        { EExtendedClassJob.Viper, "Viper" },
        { EExtendedClassJob.Pictomancer, "Pictomancer" },
        { EExtendedClassJob.DoW, "DoW" },
        { EExtendedClassJob.DoM, "DoM" },
        { EExtendedClassJob.DoH, "DoH" },
        { EExtendedClassJob.DoL, "DoL" },
    };
}
