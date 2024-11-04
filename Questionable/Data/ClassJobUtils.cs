using System;
using System.Collections.Generic;
using System.Linq;
using LLib.GameData;
using Questionable.Model.Questing;

namespace Questionable.Data;

public static class ClassJobUtils
{
    public static IEnumerable<EClassJob> AsIndividualJobs(EExtendedClassJob classJob)
    {
        return classJob switch
        {
            EExtendedClassJob.Gladiator => [EClassJob.Gladiator],
            EExtendedClassJob.Pugilist => [EClassJob.Pugilist],
            EExtendedClassJob.Marauder => [EClassJob.Marauder],
            EExtendedClassJob.Lancer => [EClassJob.Lancer],
            EExtendedClassJob.Archer => [EClassJob.Archer],
            EExtendedClassJob.Conjurer => [EClassJob.Conjurer],
            EExtendedClassJob.Thaumaturge => [EClassJob.Thaumaturge],
            EExtendedClassJob.Carpenter => [EClassJob.Carpenter],
            EExtendedClassJob.Blacksmith => [EClassJob.Blacksmith],
            EExtendedClassJob.Armorer => [EClassJob.Armorer],
            EExtendedClassJob.Goldsmith => [EClassJob.Goldsmith],
            EExtendedClassJob.Leatherworker => [EClassJob.Leatherworker],
            EExtendedClassJob.Weaver => [EClassJob.Weaver],
            EExtendedClassJob.Alchemist => [EClassJob.Alchemist],
            EExtendedClassJob.Culinarian => [EClassJob.Culinarian],
            EExtendedClassJob.Miner => [EClassJob.Miner],
            EExtendedClassJob.Botanist => [EClassJob.Botanist],
            EExtendedClassJob.Fisher => [EClassJob.Fisher],
            EExtendedClassJob.Paladin => [EClassJob.Paladin],
            EExtendedClassJob.Monk => [EClassJob.Monk],
            EExtendedClassJob.Warrior => [EClassJob.Warrior],
            EExtendedClassJob.Dragoon => [EClassJob.Dragoon],
            EExtendedClassJob.Bard => [EClassJob.Bard],
            EExtendedClassJob.WhiteMage => [EClassJob.WhiteMage],
            EExtendedClassJob.BlackMage => [EClassJob.BlackMage],
            EExtendedClassJob.Arcanist => [EClassJob.Arcanist],
            EExtendedClassJob.Summoner => [EClassJob.Summoner],
            EExtendedClassJob.Scholar => [EClassJob.Scholar],
            EExtendedClassJob.Rogue => [EClassJob.Rogue],
            EExtendedClassJob.Ninja => [EClassJob.Ninja],
            EExtendedClassJob.Machinist => [EClassJob.Machinist],
            EExtendedClassJob.DarkKnight => [EClassJob.DarkKnight],
            EExtendedClassJob.Astrologian => [EClassJob.Astrologian],
            EExtendedClassJob.Samurai => [EClassJob.Samurai],
            EExtendedClassJob.RedMage => [EClassJob.RedMage],
            EExtendedClassJob.BlueMage => [EClassJob.BlueMage],
            EExtendedClassJob.Gunbreaker => [EClassJob.Gunbreaker],
            EExtendedClassJob.Dancer => [EClassJob.Dancer],
            EExtendedClassJob.Reaper => [EClassJob.Reaper],
            EExtendedClassJob.Sage => [EClassJob.Sage],
            EExtendedClassJob.Viper => [EClassJob.Viper],
            EExtendedClassJob.Pictomancer => [EClassJob.Pictomancer],

            EExtendedClassJob.DoW => Enum.GetValues<EClassJob>().Where(x => x.DealsPhysicalDamage()),
            EExtendedClassJob.DoM => Enum.GetValues<EClassJob>().Where(x => x.DealsMagicDamage()),
            EExtendedClassJob.DoH => Enum.GetValues<EClassJob>().Where(x => x.IsCrafter()),
            EExtendedClassJob.DoL => Enum.GetValues<EClassJob>().Where(x => x.IsGatherer()),

            _ => throw new ArgumentOutOfRangeException(nameof(classJob), classJob, null)
        };
    }
}
