using Lukoil.Client.Models;

namespace Lukoil.Client.Parsers;

public interface IResponseParser
{
    ParsedTable ParseTable(string rawResponse);
}
