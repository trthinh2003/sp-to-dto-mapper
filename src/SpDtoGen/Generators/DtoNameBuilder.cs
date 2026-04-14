using System.Text.RegularExpressions;

namespace SpDtoGen.Generators;

public static class DtoNameBuilder
{
    public static string Build(string spName, string suffix = "Dto")
    {
        var cleaned = Regex.Replace(spName, @"^(usp_|sp_|p_|proc_)", "", RegexOptions.IgnoreCase);

        var parts = cleaned
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(PascalCase);

        return string.Concat(parts) + suffix;
    }

    private static string PascalCase(string s)
        => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}