using System.Buffers.Binary;
using System.Text;

namespace PunchPoint.Core.Jet;

/// <summary>Access/Jet column data types (Jet 4 codes).</summary>
public enum JetColumnType : byte
{
    Boolean = 0x01,
    Byte = 0x02,
    Int16 = 0x03,
    Int32 = 0x04,
    Currency = 0x05,
    Single = 0x06,
    Double = 0x07,
    DateTime = 0x08,
    Binary = 0x09,
    Text = 0x0A,
    Ole = 0x0B,
    Memo = 0x0C,
    Guid = 0x0F,
    Numeric = 0x10,
    BigBinary = 0x11,
    Complex = 0x12,
    BigInt = 0x13,
}

public sealed class Jet4Column
{
    public required string Name { get; init; }
    public required JetColumnType Type { get; init; }
    public required int Number { get; init; }
    public required int FixedOffset { get; init; }
    public required int VarIndex { get; init; }
    public required bool IsVariable { get; init; }
    public required int Length { get; init; }
    public int Precision { get; init; }
    public int Scale { get; init; }

    public int FixedSize => Type switch
    {
        JetColumnType.Boolean => 0,
        JetColumnType.Byte => 1,
        JetColumnType.Int16 => 2,
        JetColumnType.Int32 => 4,
        JetColumnType.Currency => 8,
        JetColumnType.Single => 4,
        JetColumnType.Double => 8,
        JetColumnType.DateTime => 8,
        JetColumnType.Guid => 16,
        JetColumnType.Memo or JetColumnType.Ole => 12, // long-value definition
        _ => IsVariable ? 0 : Math.Max(Length, 0),
    };

    public override string ToString() => $"{Name} ({Type})";
}
