using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Questionable.Model;

[SuppressMessage("Design", "CA1028", Justification = "Game type")]
[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public enum EAlliedSociety : byte
{
    None = 0,
    Amaljaa = 1,
    Sylphs = 2,
    Kobolds = 3,
    Sahagin = 4,
    Ixal = 5,
    VanuVanu = 6,
    Vath = 7,
    Moogles = 8,
    Kojin = 9,
    Ananta = 10,
    Namazu = 11,
    Pixies = 12,
    Qitari = 13,
    Dwarves = 14,
    Arkasodara = 15,
    Omicrons = 16,
    Loporrits = 17,
    Pelupelu = 18,
    MamoolJa = 19,
}

public static class EAlliedSocietyExtensions
{
    public static string ToFriendlyString(this EAlliedSociety eAlliedSociety)
    {
        return eAlliedSociety switch
        {
            EAlliedSociety.Amaljaa => "蜥蜴人族",
            EAlliedSociety.Sylphs => "妖精族",
            EAlliedSociety.Kobolds => "地灵族",
            EAlliedSociety.Sahagin => "鱼人族",
            EAlliedSociety.Ixal => "鸟人族",
            EAlliedSociety.VanuVanu => "瓦努族",
            EAlliedSociety.Vath => "骨颌族",
            EAlliedSociety.Moogles => "莫古力族 ",
            EAlliedSociety.Kojin => "甲人族",
            EAlliedSociety.Ananta => "阿难陀族",
            EAlliedSociety.Namazu => "鲶鱼精族",
            EAlliedSociety.Pixies => "仙子族",
            EAlliedSociety.Qitari => "奇塔利族",
            EAlliedSociety.Dwarves => "矮人族",
            EAlliedSociety.Arkasodara => "悌阳象族",
            EAlliedSociety.Omicrons => "奥密克戎族",
            EAlliedSociety.Loporrits => "兔兔族",
            EAlliedSociety.Pelupelu => "佩鲁佩鲁族",
            _ => eAlliedSociety.ToString(),
        };
    }
}
