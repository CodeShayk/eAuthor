using System.Text.Json;

namespace eAuthor.Services
{
    /// <summary>
    /// Processes conditional template blocks with syntax:
    /// {{ if <expr> }} ... {{ elseif <expr> }} ... {{ else }} ... {{ end }}
    /// Supports nesting by iteratively resolving from outermost matches.
    /// </summary>
    public interface IConditionalBlockProcessor
    {
        string ProcessConditionals(string content, JsonElement root);
    }
}