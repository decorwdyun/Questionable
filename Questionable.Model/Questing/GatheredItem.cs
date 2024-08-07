namespace Questionable.Model.Questing;

public sealed class GatheredItem
{
    public uint ItemId { get; set; }
    public uint AlternativeItemId { get; set; }
    public int ItemCount { get; set; }
    public ushort Collectability { get; set; }

    /// <summary>
    /// Either miner or botanist; null if it is irrelevant (prefers current class/job, then any unlocked ones).
    /// </summary>
    public uint? ClassJob { get; set; }
}
