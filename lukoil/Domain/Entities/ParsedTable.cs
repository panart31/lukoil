using System.Data;

namespace Lukoil.Client.Models;

public sealed class ParsedTable
{
    public DataTable DataTable { get; init; } = new();
    public string[] Headers { get; init; } = [];
    public int RowCount => DataTable.Rows.Count;
}
