using Microsoft.Data.SqlClient;
using SpDtoGen.Models;

namespace SpDtoGen.Parsers;

public class StoredProcedureParser(string connectionString)
{
    private readonly string _connectionString = connectionString;

    /// fetches metadata for a single SP using sp_describe_first_result_set.
    public async Task<StoredProcedureInfo> ParseAsync(string schema, string spName,
        CancellationToken ct = default)
    {
        var columns = new List<ColumnMetadata>();
        var fullName = $"[{schema}].[{spName}]";
        string? undescribableReason = null;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // sp_describe_first_result_set does static analysis — no data is executed
        var sql = "EXEC sp_describe_first_result_set @tsql = @sp, @params = NULL, @browse_information_mode = 0";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sp", fullName);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                columns.Add(new ColumnMetadata
                {
                    ColumnOrdinal = reader.GetInt32(reader.GetOrdinal("column_ordinal")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    IsNullable = reader.GetBoolean(reader.GetOrdinal("is_nullable")),
                    SystemTypeName = reader.GetString(reader.GetOrdinal("system_type_name")),
                    MaxLength = reader.GetInt16(reader.GetOrdinal("max_length")),
                    Precision = reader.GetByte(reader.GetOrdinal("precision")),
                    Scale = reader.GetByte(reader.GetOrdinal("scale")),
                    IsIdentity = reader.GetBoolean(reader.GetOrdinal("is_identity_column")),
                    IsComputed = reader.GetBoolean(reader.GetOrdinal("is_computed_column")),
                });
            }
        }
        catch (SqlException ex) when (ex.Number is 11501 or 11526 or 11505 or 11502)
        {
            undescribableReason = ex.Number switch
            {
                11526 => "SP uses temp tables (#tempTable)",
                11501 => "SP contains dynamic SQL (EXEC(@sql))",
                11502 => "SP has multiple result sets",
                _ => "Metadata could not be determined"
            };
        }
        catch (SqlException ex)
        {
            // lỗi khác (connection, permission...) — throw ra
            throw new InvalidOperationException($"Failed to describe SP [{schema}].[{spName}]: {ex.Message}", ex);
        }

        return new StoredProcedureInfo
        {
            Schema = schema,
            Name = spName,
            Columns = columns,
            UndescribableReason = undescribableReason
        };
    }

    public async Task<List<(string Schema, string Name)>> ListStoredProceduresAsync(
        string? pattern = null, CancellationToken ct = default)
    {
        var results = new List<(string, string)>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = """
            SELECT ROUTINE_SCHEMA, ROUTINE_NAME
            FROM INFORMATION_SCHEMA.ROUTINES
            WHERE ROUTINE_TYPE = 'PROCEDURE'
              AND (@pattern IS NULL OR ROUTINE_NAME LIKE @pattern)
            ORDER BY ROUTINE_SCHEMA, ROUTINE_NAME
            """;

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@pattern",
            pattern is null ? DBNull.Value : (object)pattern.Replace("*", "%"));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add((reader.GetString(0), reader.GetString(1)));

        return results;
    }
}