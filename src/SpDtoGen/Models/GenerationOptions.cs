namespace SpDtoGen.Models;

public record GenerationOptions
{
    public string Namespace { get; init; } = "Application.DTOs";
    public string DtoSuffix { get; init; } = "Dto";
    public string? OutputDirectory { get; init; }
    public bool UseRecord { get; init; } = false;
    public bool AddGeneratedCodeAttribute { get; init; } = true;
    public bool DryRun { get; init; } = false;
    public bool Force { get; init; } = false;
}