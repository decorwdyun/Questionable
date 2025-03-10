﻿using System.Text.Json.Serialization;
using Questionable.Model.Questing.Converter;

namespace Questionable.Model.Questing;

[JsonConverter(typeof(ExtendedClassJobConverter))]
public enum EExtendedClassJob
{
    None,
    Gladiator,
    Pugilist,
    Marauder,
    Lancer,
    Archer,
    Conjurer,
    Thaumaturge,
    Carpenter,
    Blacksmith,
    Armorer,
    Goldsmith,
    Leatherworker,
    Weaver,
    Alchemist,
    Culinarian,
    Miner,
    Botanist,
    Fisher,
    Paladin,
    Monk,
    Warrior,
    Dragoon,
    Bard,
    WhiteMage,
    BlackMage,
    Arcanist,
    Summoner,
    Scholar,
    Rogue,
    Ninja,
    Machinist,
    DarkKnight,
    Astrologian,
    Samurai,
    RedMage,
    BlueMage,
    Gunbreaker,
    Dancer,
    Reaper,
    Sage,
    Viper,
    Pictomancer,
    DoW,
    DoM,
    DoH,
    DoL,
    ConfiguredCombatJob,
    QuestStartJob,
}

