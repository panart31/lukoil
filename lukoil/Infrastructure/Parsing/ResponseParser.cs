using System.Data;
using System.Text.RegularExpressions;
using Lukoil.Client.Models;
using Lukoil.Client.Services;

namespace Lukoil.Client.Parsers;

public sealed class ResponseParser(ILogService logService) : IResponseParser
{
    private static readonly Regex PipeSplitRegex = new("\\s*\\|\\s*", RegexOptions.Compiled);

    public ParsedTable ParseTable(string rawResponse)
    {
        var lines = rawResponse
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Contains('|'))
            .ToList();

        if (lines.Count == 0)
        {
            logService.Warn("Parser received non-tabular response.");
            return new ParsedTable();
        }

        var header = ParseRow(lines[0]);
        if (header.Count == 0)
        {
            return new ParsedTable();
        }

        var dataTable = new DataTable();
        foreach (var h in header)
        {
            dataTable.Columns.Add(string.IsNullOrWhiteSpace(h) ? "Column" : h, typeof(string));
        }

        foreach (var line in lines.Skip(1))
        {
            var rowValues = ParseRow(line);
            if (rowValues.Count == 0)
            {
                continue;
            }

            while (rowValues.Count < dataTable.Columns.Count)
            {
                rowValues.Add(string.Empty);
            }

            if (rowValues.Count > dataTable.Columns.Count)
            {
                rowValues = rowValues.Take(dataTable.Columns.Count).ToList();
            }

            dataTable.Rows.Add(rowValues.ToArray());
        }

        return new ParsedTable
        {
            DataTable = dataTable,
            Headers = header.ToArray()
        };
    }

    private static List<string> ParseRow(string line)
    {
        var cleaned = line.Trim().Trim('|').Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return [];
        }

        return PipeSplitRegex.Split(cleaned)
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part) || cleaned.Contains("||"))
            .ToList();
    }
}
