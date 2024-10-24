using System.Text.Json.Serialization;
using Questionable.Model.Questing.Converter;

namespace Questionable.Model.Questing;

[JsonConverter(typeof(EmoteConverter))]
public enum EEmote
{
    None = 0,

    Bow = 5,
    Cheer = 6,
    Clap = 7,
    Comfort = 9,
    Dance = 11,
    Doubt = 12,
    Doze = 13,
    Wave = 16,
    Joy = 18,
    Kneel = 19,
    Lookout = 22,
    Me = 23,
    Deny = 25,
    Poke = 28,
    Psych = 30,
    Rally = 34,
    Soothe = 35,
    Stretch = 37,
    Welcome = 41,
    ExamineSelf = 44,
    Happy = 48,
    Disappointed = 49,
    Pray = 58,
    ImperialSalute = 59,
    Cry = 74,
    Pet = 105,
    Slap = 111,
    SundropDance = 120, 
    BattleStance = 121,
    VictoryPose = 122,
    Flex = 139,
    Respect = 140,
    Box = 166,
    Uchiwasshoi = 278
}

