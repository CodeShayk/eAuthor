using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace eAuthor.Services.Expressions;

public interface IExpressionEvaluator {
    string Evaluate(ParsedExpression expression, JsonElement root, JsonElement? relativeContext = null);
    JsonElement? ResolvePath(JsonElement root, string path, JsonElement? relativeContext = null);
}

public class ExpressionEvaluator : IExpressionEvaluator {
    private static readonly Regex IndexedPartRx = new(@"^(?<name>[A-Za-z0-9_]+)(\[(?<idx>\d+)\])?$", RegexOptions.Compiled);

    public string Evaluate(ParsedExpression expression, JsonElement root, JsonElement? relativeContext = null) {
        var value = ResolvePath(root, expression.DataPath, relativeContext);
        var str = ValueToString(value);
        foreach (var filter in expression.Filters)             str = ApplyFilter(str, filter);
        return str;
    }

    public JsonElement? ResolvePath(JsonElement root, string path, JsonElement? relativeContext = null) {
        // If relative (no leading slash), try relativeContext first
        if (!path.StartsWith("/")) {
            if (relativeContext == null)
                return null;
            return Traverse(relativeContext.Value, path);
        }
        return Traverse(root, path.TrimStart('/'));
    }

    private JsonElement? Traverse(JsonElement start, string path) {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = start;
        foreach (var part in parts) {
            var m = IndexedPartRx.Match(part);
            if (!m.Success) return null;
            var name = m.Groups["name"].Value;

            if (current.ValueKind != JsonValueKind.Object)
                return null;

            if (!current.TryGetProperty(name, out var child))
                return null;

            if (m.Groups["idx"].Success) {
                var idx = int.Parse(m.Groups["idx"].Value, CultureInfo.InvariantCulture);
                if (child.ValueKind == JsonValueKind.Array) {
                    if (idx < 0 || idx >= child.GetArrayLength()) return null;
                    child = child.EnumerateArray().ElementAt(idx);
                } else if (child.ValueKind == JsonValueKind.Object) {
                    // Try find first array property
                    var arrProp = child.EnumerateObject().FirstOrDefault(p => p.Value.ValueKind == JsonValueKind.Array);
                    if (arrProp.Value.ValueKind == JsonValueKind.Array) {
                        if (idx < 0 || idx >= arrProp.Value.GetArrayLength()) return null;
                        child = arrProp.Value.EnumerateArray().ElementAt(idx);
                    } else return null;
                } else return null;
            }
            current = child;
        }
        return current;
    }

    private string ValueToString(JsonElement? el) {
        if (el == null) return "";
        return el.Value.ValueKind switch {
            JsonValueKind.String => el.Value.GetString() ?? "",
            JsonValueKind.Number => el.Value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => ""
        };
    }

    private string ApplyFilter(string input, ExpressionFilter filter) {
        switch (filter.Name) {
            case "upper": return input.ToUpperInvariant();
            case "lower": return input.ToLowerInvariant();
            case "trim": return input.Trim();
            case "date":
                if (DateTime.TryParse(input, out var dt)) {
                    var fmt = filter.Args.FirstOrDefault() ?? "yyyy-MM-dd";
                    return dt.ToString(fmt, CultureInfo.InvariantCulture);
                }
                return input;
            case "number":
                if (decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec)) {
                    var fmt = filter.Args.FirstOrDefault() ?? "0.##";
                    return dec.ToString(fmt, CultureInfo.InvariantCulture);
                }
                return input;
            case "bool":
                {
                    var yes = filter.Args.ElementAtOrDefault(0) ?? "Yes";
                    var no = filter.Args.ElementAtOrDefault(1) ?? "No";
                    return input.Equals("true", StringComparison.OrdinalIgnoreCase) ? yes : no;
                }
            default: return input;
        }
    }
}