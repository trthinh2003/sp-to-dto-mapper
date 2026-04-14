namespace SpDtoGen.Models;

public record StoredProcedureInfo
{
    public string Schema { get; init; } = "dbo";
    public string Name { get; init; } = string.Empty;
    public List<ColumnMetadata> Columns { get; init; } = [];

    public string FullName => $"[{Schema}].[{Name}]";
    public bool HasUndescribableResultSet => UndescribableReason is not null;
    public string? UndescribableReason { get; init; }
}