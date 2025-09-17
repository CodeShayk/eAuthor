using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace eAuthor.Services.Expressions
{
    public class ExpressionEvaluator : IExpressionEvaluator
    {
        private static readonly Regex IndexedPartRx = new(
            @"^(?<name>[A-Za-z0-9_]+)(\[(?<idx>\d+)\])?$",
            RegexOptions.Compiled);

        public string Evaluate(ParsedExpression expression, JsonElement root, JsonElement? relativeContext = null)
        {
            var value = ResolvePath(root, expression.DataPath, relativeContext);
            var str = ValueToString(value);

            foreach (var filter in expression.Filters)
                str = ApplyFilter(str, filter, value);

            return str;
        }

        public bool EvaluateBoolean(JsonElement root, ParsedExpression expression, JsonElement? relativeContext = null)
        {
            // If filters exist we evaluate them first (string-based truthiness).
            if (expression.Filters.Count > 0)
            {
                var evaluated = Evaluate(expression, root, relativeContext);
                return CoerceStringToBool(evaluated);
            }

            var value = ResolvePath(root, expression.DataPath, relativeContext);
            return CoerceElementToBool(value);
        }

        public JsonElement? ResolvePath(JsonElement root, string path, JsonElement? relativeContext = null)
        {
            if (!path.StartsWith("/"))
            {
                if (relativeContext == null)
                    return null;
                return Traverse(relativeContext.Value, path);
            }
            return Traverse(root, path.TrimStart('/'));
        }

        private JsonElement? Traverse(JsonElement start, string path)
        {
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var current = start;

            foreach (var part in parts)
            {
                var m = IndexedPartRx.Match(part);
                if (!m.Success)
                    return null;
                var name = m.Groups["name"].Value;

                if (current.ValueKind != JsonValueKind.Object)
                    return null;

                if (!current.TryGetProperty(name, out var child))
                    return null;

                if (m.Groups["idx"].Success)
                {
                    var idx = int.Parse(m.Groups["idx"].Value, CultureInfo.InvariantCulture);

                    if (child.ValueKind == JsonValueKind.Array)
                    {
                        if (idx < 0 || idx >= child.GetArrayLength())
                            return null;
                        child = child.EnumerateArray().ElementAt(idx);
                    }
                    else if (child.ValueKind == JsonValueKind.Object)
                    {
                        // Best-effort fallback: first array property if indexing used on object
                        var arrProp = child.EnumerateObject().FirstOrDefault(p => p.Value.ValueKind == JsonValueKind.Array);
                        if (arrProp.Value.ValueKind == JsonValueKind.Array)
                        {
                            if (idx < 0 || idx >= arrProp.Value.GetArrayLength())
                                return null;
                            child = arrProp.Value.EnumerateArray().ElementAt(idx);
                        }
                        else
                            return null;
                    }
                    else
                        return null;
                }

                current = child;
            }

            return current;
        }

        private string ValueToString(JsonElement? el)
        {
            if (el == null)
                return "";

            var v = el.Value;
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString() ?? "",
                JsonValueKind.Number => v.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Array => v.GetArrayLength().ToString(CultureInfo.InvariantCulture),
                JsonValueKind.Object => v.EnumerateObject().Any() ? "[object]" : "",
                _ => ""
            };
        }

        private bool CoerceElementToBool(JsonElement? el)
        {
            if (el == null)
                return false;
            var v = el.Value;

            return v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => NumberIsNonZero(v),
                JsonValueKind.String => CoerceStringToBool(v.GetString() ?? ""),
                JsonValueKind.Array => v.GetArrayLength() > 0,
                JsonValueKind.Object => v.EnumerateObject().Any(),
                _ => false
            };
        }

        private bool NumberIsNonZero(JsonElement numberElement)
        {
            if (numberElement.TryGetInt64(out var intVal))
                return intVal != 0;
            if (double.TryParse(numberElement.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var dbl))
                return Math.Abs(dbl) > double.Epsilon;
            return false;
        }

        private bool CoerceStringToBool(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return false;

            var lower = s.Trim().ToLowerInvariant();
            if (lower is "false" or "0" or "null" or "undefined" or "nan")
                return false;

            return true;
        }

        private string ApplyFilter(string input, ExpressionFilter filter, JsonElement? originalElement)
        {
            switch (filter.Name)
            {
                case "upper":
                    return input.ToUpperInvariant();

                case "lower":
                    return input.ToLowerInvariant();

                case "trim":
                    return input.Trim();

                case "date":
                    // If original looks like a date string, format it.
                    if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
                    {
                        var fmt = filter.Args.FirstOrDefault() ?? "yyyy-MM-dd";
                        return dt.ToString(fmt, CultureInfo.InvariantCulture);
                    }
                    return input;

                case "number":
                    if (decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
                    {
                        var fmt = filter.Args.FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(fmt))
                            return dec.ToString(fmt, CultureInfo.InvariantCulture);
                        return dec.ToString(CultureInfo.InvariantCulture);
                    }
                    return input;

                case "bool":
                    // bool:TrueVal:FalseVal
                    var trueText = filter.Args.Length > 0 ? filter.Args[0] : "true";
                    var falseText = filter.Args.Length > 1 ? filter.Args[1] : "false";
                    bool truthy;
                    if (originalElement is { } elRef)
                        truthy = CoerceElementToBool(elRef);
                    else
                        truthy = CoerceStringToBool(input);
                    return truthy ? trueText : falseText;

                default:
                    // Unknown filter => no transformation
                    return input;
            }
        }
    }
}