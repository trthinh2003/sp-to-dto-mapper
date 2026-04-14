using SpDtoGen.Models;

namespace SpDtoGen.Generators;

public class DtoFileWriter
{
    public async Task<WriteResult> WriteAsync(
        string code,
        StoredProcedureInfo sp,
        GenerationOptions opts,
        CancellationToken ct = default)
    {
        var className = DtoNameBuilder.Build(sp.Name, opts.DtoSuffix);
        var fileName = $"{className}.cs";
        var directory = opts.OutputDirectory ?? Directory.GetCurrentDirectory();
        var filePath = Path.Combine(directory, fileName);

        if (!opts.DryRun)
        {
            if (File.Exists(filePath) && !opts.Force)
                return new WriteResult(filePath, WriteStatus.Skipped);

            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(filePath, code, ct);
        }

        return new WriteResult(filePath, opts.DryRun ? WriteStatus.DryRun : WriteStatus.Written);
    }
}

public record WriteResult(string FilePath, WriteStatus Status);

public enum WriteStatus { Written, Skipped, DryRun }