using System.Text.Json;
using System.Text.RegularExpressions;
using eAuthor.Services.Expressions;

namespace eAuthor.Services.Impl
{
    public class ConditionalBlockProcessor : IConditionalBlockProcessor
    {
        // Matches: {{ if <cond> }} (body including ALL inner text) {{ end }}
        private static readonly Regex IfBlockRx = new(
            @"\{\{\s*if\s+(?<cond>[^}]+?)\s*\}\}(?<body>.*?)\{\{\s*end\s*\}\}",
            RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches: {{ elseif <cond> }}  (no body captured here; body is determined by slicing)
        private static readonly Regex ElseIfRx = new(
            @"\{\{\s*elseif\s+(?<cond>[^}]+?)\s*\}\}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches: {{ else }}
        private static readonly Regex ElseRx = new(
            @"\{\{\s*else\s*\}\}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IExpressionParser _parser;
        private readonly IExpressionEvaluator _evaluator;

        public ConditionalBlockProcessor(IExpressionParser parser, IExpressionEvaluator evaluator)
        {
            _parser = parser;
            _evaluator = evaluator;
        }

        public string ProcessConditionals(string content, JsonElement root)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            var guard = 0;
            while (true)
            {
                var match = IfBlockRx.Match(content);
                if (!match.Success)
                    break;

                guard++;
                if (guard > 3000)
                    throw new InvalidOperationException("Too many nested conditional resolutions (possible infinite loop).");

                var fullMatchStart = match.Index;
                var fullMatchLength = match.Length;
                var condition = match.Groups["cond"].Value.Trim();
                var innerBody = match.Groups["body"].Value;

                // Identify elseif & else markers inside the body (first level only).
                var elseifMatches = ElseIfRx.Matches(innerBody).Cast<Match>().OrderBy(m => m.Index).ToList();
                var elseMatch = ElseRx.Match(innerBody);

                // Segment extraction
                var segments = new List<(string kind, string? expr, string body)>();

                var firstSegmentEnd = innerBody.Length;
                if (elseifMatches.Any())
                    firstSegmentEnd = Math.Min(firstSegmentEnd, elseifMatches.First().Index);
                if (elseMatch.Success)
                    firstSegmentEnd = Math.Min(firstSegmentEnd, elseMatch.Index);

                // IF segment
                segments.Add(("if", condition, innerBody.Substring(0, firstSegmentEnd)));

                // ELSEIF segments
                for (var i = 0; i < elseifMatches.Count; i++)
                {
                    var current = elseifMatches[i];
                    var start = current.Index + current.Length;
                    var end = i + 1 < elseifMatches.Count
                        ? elseifMatches[i + 1].Index
                        : elseMatch.Success ? elseMatch.Index : innerBody.Length;
                    var expr = current.Groups["cond"].Value.Trim();
                    segments.Add(("elseif", expr, innerBody.Substring(start, end - start)));
                }

                // ELSE segment
                if (elseMatch.Success)
                {
                    var start = elseMatch.Index + elseMatch.Length;
                    if (start <= innerBody.Length)
                        segments.Add(("else", null, innerBody.Substring(start)));
                }

                // Decide which segment to keep
                var replacement = string.Empty;
                foreach (var seg in segments)
                    if (seg.kind == "if" || seg.kind == "elseif")
                        if (EvaluateConditionSafe(seg.expr!, root))
                        {
                            replacement = seg.body;
                            break;
                        }
                        else if (seg.kind == "else")
                        {
                            replacement = seg.body;
                            break;
                        }

                // Replace the entire matched conditional block with the decided segment
                content = content.Substring(0, fullMatchStart)
                          + replacement
                          + content.Substring(fullMatchStart + fullMatchLength);
            }

            return content;
        }

        private bool EvaluateConditionSafe(string rawExpr, JsonElement root)
        {
            try
            {
                var parsed = _parser.Parse(rawExpr);
                var valueEl = _evaluator.ResolvePath(root, parsed.DataPath);
                return IsTruthy(valueEl);
            }
            catch
            {
                return false;
            }
        }

        private bool IsTruthy(JsonElement? element)
        {
            if (element == null)
                return false;
            var v = element.Value;

            switch (v.ValueKind)
            {
                case JsonValueKind.Undefined:
                case JsonValueKind.Null:
                case JsonValueKind.False:
                    return false;

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.String:
                    return !string.IsNullOrWhiteSpace(v.GetString());

                case JsonValueKind.Number:
                    if (v.TryGetInt64(out var l))
                        return l != 0;
                    if (v.TryGetDouble(out var d))
                        return Math.Abs(d) > double.Epsilon;
                    return true;

                case JsonValueKind.Array:
                    return v.GetArrayLength() > 0;

                case JsonValueKind.Object:
                    return v.EnumerateObject().Any();

                default:
                    return false;
            }
        }
    }
}