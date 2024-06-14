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

    public enum EType
    {
        None,
        Key,
        RowId,
    }
}
