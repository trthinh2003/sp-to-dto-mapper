using SpDtoGen.Generators;
using SpDtoGen.Models;
using SpDtoGen.Parsers;
using Spectre.Console;
using System.CommandLine;

namespace SpDtoGen.Commands;

public static class DtoCommand
{  
    private static string Escape(string text) => text.Replace("[", "[[").Replace("]", "]]"); // Escape [ ] để Spectre.Console không nhầm thành markup tag

    public static Command Build()
    {
        var connectionOpt = new Option<string>(
            aliases: ["--connection", "-c"],
            description: "SQL Server connection string")
        { IsRequired = true };

        var spOpt = new Option<string?>(
            aliases: ["--sp", "-s"],
            description: "SP name or wildcard pattern (e.g. 'usp_Order_*'). Omit to generate for all SPs.");

        var outputOpt = new Option<string?>(
            aliases: ["--output", "-o"],
            description: "Output directory for generated .cs files. Defaults to current directory.");

        var namespaceOpt = new Option<string>(
            aliases: ["--namespace", "-n"],
            getDefaultValue: () => "Application.DTOs",
            description: "C# namespace for generated DTOs");

        var suffixOpt = new Option<string>(
            aliases: ["--suffix"],
            getDefaultValue: () => "Dto",
            description: "Suffix appended to DTO class names");

        var recordOpt = new Option<bool>(
            aliases: ["--record"],
            description: "Generate record instead of class");

        var dryRunOpt = new Option<bool>(
            aliases: ["--dry-run"],
            description: "Print generated code to console without writing files");

        var forceOpt = new Option<bool>(
            aliases: ["--force", "-f"],
            description: "Overwrite existing files");

        var cmd = new Command("dto", "Generate C# DTOs from SQL Server stored procedures")
        {
            connectionOpt, spOpt, outputOpt, namespaceOpt,
            suffixOpt, recordOpt, dryRunOpt, forceOpt
        };

        cmd.SetHandler(async ctx =>
        {
            var connection = ctx.ParseResult.GetValueForOption(connectionOpt)!;
            var spPattern = ctx.ParseResult.GetValueForOption(spOpt);
            var output = ctx.ParseResult.GetValueForOption(outputOpt);
            var ns = ctx.ParseResult.GetValueForOption(namespaceOpt)!;
            var suffix = ctx.ParseResult.GetValueForOption(suffixOpt)!;
            var useRecord = ctx.ParseResult.GetValueForOption(recordOpt);
            var dryRun = ctx.ParseResult.GetValueForOption(dryRunOpt);
            var force = ctx.ParseResult.GetValueForOption(forceOpt);
            var ct = ctx.GetCancellationToken();

            var opts = new GenerationOptions
            {
                Namespace = ns,
                DtoSuffix = suffix,
                OutputDirectory = output,
                UseRecord = useRecord,
                DryRun = dryRun,
                Force = force,
                AddGeneratedCodeAttribute = true,
            };

            var exitCode = await RunAsync(connection, spPattern, opts, ct);
            ctx.ExitCode = exitCode;
        });

        return cmd;
    }

    private static async Task<int> RunAsync(
        string connectionString,
        string? spPattern,
        GenerationOptions opts,
        CancellationToken ct)
    {
        var parser = new StoredProcedureParser(connectionString);
        var generator = new DtoCodeGenerator();
        var writer = new DtoFileWriter();

        List<(string Schema, string Name)> procedures = [];

        if (spPattern is not null && !spPattern.Contains('*') && !spPattern.Contains('%'))
        {
            var parts = spPattern.Split('.', 2);
            procedures = parts.Length == 2
                ? [(parts[0], parts[1])]
                : [("dbo", spPattern)];
        }
        else
        {
            await AnsiConsole.Status()
                .StartAsync("Listing stored procedures...", async _ =>
                {
                    procedures = await parser.ListStoredProceduresAsync(spPattern, ct);
                });

            procedures ??= [];
            AnsiConsole.MarkupLine($"[grey]Found [white]{procedures.Count}[/] stored procedures[/]");
        }

        if (procedures.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No stored procedures found matching the criteria.[/]");
            return 0;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Stored Procedure")
            .AddColumn("DTO Class")
            .AddColumn("Status");

        var written = 0;
        var skipped = 0;
        var warned = 0;

        await AnsiConsole.Progress()
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("Generating DTOs", maxValue: procedures.Count);

                foreach (var (schema, name) in procedures)
                {
                    task.Description = $"Processing {name}";

                    var sp = await parser.ParseAsync(schema, name, ct);
                    var code = generator.Generate(sp, opts);
                    var dtoName = DtoNameBuilder.Build(sp.Name, opts.DtoSuffix);

                    if (opts.DryRun)
                    {
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine($"[blue]// ---- {Escape(dtoName)}.cs ----[/]");
                        AnsiConsole.WriteLine(code);
                        table.AddRow(Escape(sp.FullName), Escape(dtoName), "[blue]dry-run[/]");
                        written++;
                    }
                    else
                    {
                        var result = await writer.WriteAsync(code, sp, opts, ct);

                        switch (result.Status)
                        {
                            case WriteStatus.Written:
                                table.AddRow(
                                    Escape(sp.FullName), Escape(dtoName),
                                    sp.HasUndescribableResultSet
                                        ? "[yellow]written (⚠ check manually)[/]"
                                        : "[green]written[/]");
                                if (sp.HasUndescribableResultSet) warned++;
                                else written++;
                                break;
                            case WriteStatus.Skipped:
                                table.AddRow(Escape(sp.FullName), Escape(dtoName), "[grey]skipped (use --force)[/]");
                                skipped++;
                                break;
                        }
                    }

                    task.Increment(1);
                }
            });

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[green]{written} generated[/]  [grey]{skipped} skipped[/]  [yellow]{warned} need review[/]");

        return 0;
    }
}
