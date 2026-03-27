namespace Lukoil.Client.Models;

public sealed class QueryResult
{
    public bool IsSuccess { get; init; }
    public string RawResponse { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public ParsedTable? Table { get; init; }

    public static QueryResult Success(string rawResponse, ParsedTable? table = null) => new()
    {
        IsSuccess = true,
        RawResponse = rawResponse,
        Table = table
    };

    public static QueryResult Fail(string message, string rawResponse = "") => new()
    {
        IsSuccess = false,
        ErrorMessage = message,
        RawResponse = rawResponse
    };
}
