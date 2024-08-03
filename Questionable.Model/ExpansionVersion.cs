using System.Collections.Generic;

namespace Questionable.Model;

public static class ExpansionData
{
    public static IReadOnlyDictionary<byte, string> ExpansionFolders = new Dictionary<byte, string>()
    {
        { 0, "2.x - A Realm Reborn" },
        { 1, "3.x - Heavensward" },
        { 2, "4.x - Stormblood" },
        { 3, "5.x - Shadowbringers" },
        { 4, "6.x - Endwalker" },
        { 5, "7.x - Dawntrail" }
    };
}
