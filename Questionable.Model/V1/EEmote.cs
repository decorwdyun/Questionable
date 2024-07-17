using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

[JsonConverter(typeof(EmoteConverter))]
public enum EEmote
{
    None = 0,

    Stretch = 37,
    Wave = 16,
    Rally = 34,
    Deny = 25,
    Pray = 58,
    Slap = 111,
    Doubt = 12,
    Psych = 30,
    Cheer = 6,
    Happy = 48,
    Poke = 28,
    Flex = 139,
    Soothe = 35,
    Me = 23,
    Welcome = 41,
    ImperialSalute = 59,
    Pet = 105,
    Dance = 11,
}
