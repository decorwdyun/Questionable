namespace Questionable.Model.Questing;

public sealed class GatheredItem
{
    public uint ItemId { get; set; }
    public int ItemCount { get; set; }
    public short Collectability { get; set; }
}
