using SpDtoGen.Models;

namespace SpDtoGen.TypeMapping;

public static class SqlTypeToCSharpMapper
{
    public static string Map(ColumnMetadata col)
    {
        var baseType = GetBaseType(col);
        var isReferenceType = baseType is "string" or "byte[]" or "object"; // reference types (string, byte[], object) are already nullable in C#
        return col.IsNullable && !isReferenceType
            ? $"{baseType}?"
            : baseType;
    }

    private static string GetBaseType(ColumnMetadata col)
    {    
        var typeName = col.SystemTypeName.Split('(')[0].Trim().ToLowerInvariant(); // normalize: strip parenthetical parts like "nvarchar(100)" -> "nvarchar"

        return typeName switch
        {
            // Integer types
            "tinyint" => "byte",
            "smallint" => "short",
            "int" => "int",
            "bigint" => "long",

            // Floating point
            "real" => "float",
            "float" => "double",

            // Exact numeric
            "decimal" or "numeric" => FormatDecimal(col),
            "money" => "decimal",
            "smallmoney" => "decimal",

            // Boolean
            "bit" => "bool",

            // String types
            "char" or "varchar" => "string",
            "nchar" or "nvarchar" => "string",
            "text" or "ntext" => "string",
            "xml" => "string",

            // Date/time types
            "date" => "DateOnly",
            "time" => "TimeOnly",
            "datetime" => "DateTime",
            "datetime2" => "DateTime",
            "smalldatetime" => "DateTime",
            "datetimeoffset" => "DateTimeOffset",

            // Binary types
            "binary" or "varbinary" or "image" => "byte[]",
            "timestamp" or "rowversion" => "byte[]",

            // Special types
            "uniqueidentifier" => "Guid",
            "sql_variant" => "object",
            "hierarchyid" => "string",
            "geography" => "object",
            "geometry" => "object",

            // Unknown/unsupported → warn with comment
            _ => $"object /* unknown sql type: {col.SystemTypeName} */"
        };
    }

    private static string FormatDecimal(ColumnMetadata col)
    {
        // decimal(18,2) stays as decimal in C# — precision/scale are DB concerns
        // expose precision/scale as XML doc comment in the generated DTO
        return "decimal";
    }
 
    public static string? GetDecimalAnnotation(ColumnMetadata col) /// returns a doc comment annotation for decimal columns to preserve precision/scale info.
    {
        var typeName = col.SystemTypeName.Split('(')[0].Trim().ToLowerInvariant();
        if (typeName is "decimal" or "numeric" && col.Precision > 0)
            return $"/// <remarks>SQL: {col.SystemTypeName} — precision={col.Precision}, scale={col.Scale}</remarks>";
        return null;
    }
}