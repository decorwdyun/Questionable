using System;

namespace Questionable.Model.V1;

public class ExcelRef
{
    private readonly string? _stringValue;
    private readonly uint? _rowIdValue;

    public ExcelRef(string value)
    {
        _stringValue = value;
        _rowIdValue = null;
        Type = EType.Key;
    }

    public ExcelRef(uint value)
    {
        _stringValue = null;
        _rowIdValue = value;
        Type = EType.RowId;
    }

    /// <summary>
    /// Only used internally (not serialized) with specific values that have been read from the sheets already.
    /// </summary>
    private ExcelRef(string value, bool v)
    {
        if (!v)
            throw new ArgumentException(nameof(v));

        _stringValue = value;
        _rowIdValue = null;
        Type = EType.RawString;
    }

    public static ExcelRef FromSheetValue(string value) => new(value, true);

    public EType Type { get; }

    public string AsKey()
    {
        if (Type != EType.Key)
            throw new InvalidOperationException();

        return _stringValue!;
    }

    public uint AsRowId()
    {
        if (Type != EType.RowId)
            throw new InvalidOperationException();

        return _rowIdValue!.Value;
    }

    public string AsRawString()
    {
        if (Type != EType.RawString)
            throw new InvalidOperationException();

        return _stringValue!;
    }

    public enum EType
    {
        None,
        Key,
        RowId,
        RawString,
    }
}
