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

    public static ElementId FromString(string value)
    {
        if (value.StartsWith("L"))
            return new LeveId(ushort.Parse(value.Substring(1), CultureInfo.InvariantCulture));
        else if (value.StartsWith("S"))
            return new SatisfactionSupplyNpcId(ushort.Parse(value.Substring(1), CultureInfo.InvariantCulture));
        else
            return new QuestId(ushort.Parse(value, CultureInfo.InvariantCulture));
    }

    public static bool TryFromString(string value, out ElementId? elementId)
    {
        try
        {
            elementId = FromString(value);
            return true;
        }
        catch (Exception)
        {
            elementId = null;
            return false;
        }
    }
}

public sealed class QuestId(ushort value) : ElementId(value)
{
    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}

public sealed class LeveId(ushort value) : ElementId(value)
{
    public override string ToString()
    {
        return "L" + Value.ToString(CultureInfo.InvariantCulture);
    }
}

public sealed class SatisfactionSupplyNpcId(ushort value) : ElementId(value)
{
    public override string ToString()
    {
        return "S" + Value.ToString(CultureInfo.InvariantCulture);
    }
}
