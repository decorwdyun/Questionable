using System;
using System.Globalization;

namespace Questionable.Model.Questing
{
    public interface IId : IComparable<IId>
    {
        public ushort Value { get; }
    }

    public static class Id
    {
        public static IId From(uint value)
        {
            if (value >= 100_000 && value < 200_000)
                return new LeveId((ushort)(value - 100_000));
            else
                return new QuestId((ushort)value);
        }
    }

    public sealed record QuestId(ushort Value) : IId
    {
        public override string ToString()
        {
            return "Q" + Value.ToString(CultureInfo.InvariantCulture);
        }

        public int CompareTo(IId? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return Value.CompareTo(other.Value);
        }
    }

    public sealed record LeveId(ushort Value) : IId
    {
        public override string ToString()
        {
            return "L" + Value.ToString(CultureInfo.InvariantCulture);
        }

        public int CompareTo(IId? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return Value.CompareTo(other.Value);
        }
    }
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
