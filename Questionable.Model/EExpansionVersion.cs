using System.Collections.Generic;

namespace Questionable.Model;

public enum EExpansionVersion : byte
{
    ARealmReborn = 0,
    Heavensward = 1,
    Stormblood = 2,
    Shadowbringers = 3,
    Endwalker = 4,
    Dawntrail = 5
}

public static class ExpansionData
{
    public static IReadOnlyDictionary<EExpansionVersion, string> ExpansionFolders =
        new Dictionary<EExpansionVersion, string>
        {
            { EExpansionVersion.ARealmReborn, "2.x - A Realm Reborn" },
            { EExpansionVersion.Heavensward, "3.x - Heavensward" },
            { EExpansionVersion.Stormblood, "4.x - Stormblood" },
            { EExpansionVersion.Shadowbringers, "5.x - Shadowbringers" },
            { EExpansionVersion.Endwalker, "6.x - Endwalker" },
            { EExpansionVersion.Dawntrail, "7.x - Dawntrail" }
        };

    public static string ToFriendlyString(this EExpansionVersion expansionVersion)
    {
        return expansionVersion switch
        {
            EExpansionVersion.ARealmReborn => "重生之境",
            EExpansionVersion.Heavensward => "苍穹之禁城",
            EExpansionVersion.Stormblood => "红莲之狂潮",
            EExpansionVersion.Shadowbringers => "暗影之逆焰",
            EExpansionVersion.Endwalker => "晓月之终途",
            EExpansionVersion.Dawntrail => "金曦之遗辉",
            _ => expansionVersion.ToString(),
        };
    }
}
