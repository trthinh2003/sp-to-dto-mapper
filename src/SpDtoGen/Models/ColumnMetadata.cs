namespace SpDtoGen.Models;

public record ColumnMetadata
{
    public int ColumnOrdinal { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsNullable { get; init; }
    public string SystemTypeName { get; init; } = string.Empty;
    public int MaxLength { get; init; }
    public byte Precision { get; init; }
    public byte Scale { get; init; }
    public bool IsIdentity { get; init; }
    public bool IsComputed { get; init; }
}