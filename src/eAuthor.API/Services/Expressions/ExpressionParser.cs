using System.Text.RegularExpressions;

namespace eAuthor.Services.Expressions;

public class ParsedExpression {
    public string DataPath { get; set; } = "";
    public List<ExpressionFilter> Filters { get; set; } = new();
    public bool IsRelative => !string.IsNullOrEmpty(DataPath) && !DataPath.StartsWith("/");
}

public class ExpressionFilter {
    public string Name { get; set; } = "";
    public string[] Args { get; set; } = Array.Empty<string>();
}

public interface IExpressionParser {
    ParsedExpression Parse(string raw);
    IEnumerable<string> ExtractRawExpressions(string content);
}

public class ExpressionParser : IExpressionParser {
    private static readonly Regex TokenRx = new(@"\{\{\s*(.*?)\s*\}\}", RegexOptions.Compiled);

    public ParsedExpression Parse(string raw) {
        // Split by |
        var segments = raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0) throw new ArgumentException("Empty expression.");
        var dataPath = segments[0].Trim();

        // Allow relative identifiers (no leading /) for repeater context:
        // Must be simple path segments with optional indexing.
        if (!dataPath.StartsWith("/") && !Regex.IsMatch(dataPath, @"^[A-Za-z0-9_\.\/\[\]]+$"))
            throw new ArgumentException("Invalid data path.");
        var expr = new ParsedExpression { DataPath = dataPath };
        for (var i = 1; i < segments.Length; i++) {
            var seg = segments[i].Trim();
            if (string.IsNullOrWhiteSpace(seg)) continue;
            var parts = seg.Split(':');
            var name = parts[0].Trim().ToLowerInvariant();
            var args = parts.Length > 1 ? parts.Skip(1).ToArray() : Array.Empty<string>();
            expr.Filters.Add(new ExpressionFilter { Name = name, Args = args });
        }
        return expr;
    }

    public IEnumerable<string> ExtractRawExpressions(string content) {
        foreach (Match m in TokenRx.Matches(content))             yield return m.Groups[1].Value;
    }
}