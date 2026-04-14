using System.CommandLine;
using SpDtoGen.Commands;

var root = new RootCommand("sp-dto-gen — Generate C# DTOs from SQL Server stored procedures");
root.AddCommand(DtoCommand.Build());

root.SetHandler(() =>
{
    Console.WriteLine("Usage: sp-dto-gen dto --connection <conn> [--sp <pattern>] [--output <dir>]");
    Console.WriteLine("Run 'sp-dto-gen dto --help' for full options.");
});

return await root.InvokeAsync(args);