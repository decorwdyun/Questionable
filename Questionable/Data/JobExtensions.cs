using System;
using System.Linq;
using ECommons.ExcelServices;
using static Questionable.Utils.LocalizeShortcut;

namespace Questionable.Data;

internal static class JobExtensions
{
    public static bool IsClass(this Job classJob)
    {
        return classJob is >= Job.GLA and <= Job.THM
                   or Job.ACN
                   or Job.ROG
               || classJob.IsCrafter()
               || classJob.IsGatherer();
    }

    public static bool HasBaseClass(this Job classJob)
    {
        return Enum.GetValues<Job>()
            .Where(x => x.IsClass())
            .Any(x => x.AsJob() == classJob);
    }

    public static Job AsJob(this Job classJob)
    {
        return classJob switch
        {
            Job.GLA => Job.PLD,
            Job.MRD => Job.WAR,
            Job.PGL => Job.MNK,
            Job.LNC => Job.DRG,
            Job.ROG => Job.NIN,
            Job.ARC => Job.BRD,
            Job.CNJ => Job.WHM,
            Job.THM => Job.BLM,
            Job.ACN => Job.SMN,
            var _ => classJob
        };
    }

    public static bool IsMelee(this Job classJob)
    {
        return classJob is Job.PGL
            or Job.MNK
            or Job.LNC
            or Job.DRG
            or Job.ROG
            or Job.NIN
            or Job.SAM
            or Job.RPR
            or Job.VPR;
    }

    public static bool IsPhysicalRanged(this Job classJob)
    {
        return classJob is Job.ARC
            or Job.BRD
            or Job.MCH
            or Job.DNC;
    }

    public static bool IsCaster(this Job classJob)
    {
        return classJob is Job.THM
            or Job.BLM
            or Job.ACN
            or Job.SMN
            or Job.RDM
            or Job.BLU
            or Job.PCT;
    }

    public static bool DealsPhysicalDamage(this Job classJob) => classJob.IsTank() || classJob.IsMelee() || classJob.IsPhysicalRanged();

    public static bool DealsMagicDamage(this Job classJob) => classJob.IsHealer() || classJob.IsCaster();

    public static bool IsCrafter(this Job classJob) => classJob.IsDoh();

    public static bool IsGatherer(this Job classJob) => classJob.IsDol();

    public static string ToFriendlyString(this Job classJob)
    {
        return classJob switch
        {
            Job.ADV => "冒险者",
            Job.GLA => "剑术师",
            Job.PGL => "格斗家",
            Job.MRD => "斧术师",
            Job.LNC => "枪术师",
            Job.ARC => "弓箭手",
            Job.CNJ => "幻术师",
            Job.THM => "咒术师",
            Job.CRP => "刻木匠",
            Job.BSM => "锻铁匠",
            Job.ARM => "铸甲匠",
            Job.GSM => "雕金匠",
            Job.LTW => "制革匠",
            Job.WVR => "裁衣匠",
            Job.ALC => "炼金术士",
            Job.CUL => "烹调师",
            Job.MIN => "采矿工",
            Job.BTN => "园艺工",
            Job.FSH => "捕鱼人",
            Job.PLD => "骑士",
            Job.MNK => "武僧",
            Job.WAR => "战士",
            Job.DRG => "龙骑士",
            Job.BRD => "吟游诗人",
            Job.WHM => "白魔法师",
            Job.BLM => "黑魔法师",
            Job.ACN => "秘术师",
            Job.SMN => "召唤师",
            Job.SCH => "学者",
            Job.ROG => "双剑师",
            Job.NIN => "忍者",
            Job.MCH => "机工士",
            Job.DRK => "暗黑骑士",
            Job.AST => "占星术士",
            Job.SAM => "武士",
            Job.RDM => "赤魔法师",
            Job.BLU => "青魔法师",
            Job.GNB => "绝枪战士",
            Job.DNC => "舞者",
            Job.RPR => "钐镰客",
            Job.SGE => "贤者",
            Job.VPR => "蝰蛇剑士",
            Job.PCT => "绘灵法师",
            var _ => classJob.ToString()
        };
    }
}
