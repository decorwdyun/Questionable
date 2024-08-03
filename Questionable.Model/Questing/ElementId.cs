using System;
using System.Globalization;

namespace Questionable.Model.Questing;

public abstract class ElementId : IComparable<ElementId>, IEquatable<ElementId>
{
    protected ElementId(ushort value)
    {
        Value = value;
    }

    public ushort Value { get; }

    public int CompareTo(ElementId? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (ReferenceEquals(null, other)) return 1;
        return Value.CompareTo(other.Value);
    }

    public bool Equals(ElementId? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        if (other.GetType() != GetType()) return false;
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ElementId)obj);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public static bool operator ==(ElementId? left, ElementId? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ElementId? left, ElementId? right)
    {
        return !Equals(left, right);
    }

    public static ElementId From(uint value)
    {
        if (value >= 100_000 && value < 200_000)
            return new LeveId((ushort)(value - 100_000));
        else
            return new QuestId((ushort)value);
    }
}

public sealed class QuestId : ElementId
{
    public QuestId(ushort value)
        : base(value)
    {
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}

public sealed class LeveId : ElementId
{
    public LeveId(ushort value)
        : base(value)
    {
    }

    public override string ToString()
    {
        return "L" + Value.ToString(CultureInfo.InvariantCulture);
    }
}
