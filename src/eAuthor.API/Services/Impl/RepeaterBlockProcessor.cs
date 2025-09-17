using System.Text.RegularExpressions;
using System.Text.Json;
using eAuthor.Services.Expressions;

namespace eAuthor.Services.Impl;

public class RepeaterBlockProcessor : IRepeaterBlockProcessor
{
    private static readonly Regex RepeaterRx = new(
        @"\{\{\s*repeat\s+(?<path>\/[A-Za-z0-9_\/\[\]]+)\s*\}\}(?<body>.*?)\{\{\s*endrepeat\s*\}\}",
        RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TokenRx = new(@"\{\{\s*(?<expr>[^}]+)\s*\}\}", RegexOptions.Compiled);

    private readonly IExpressionParser _parser;
    private readonly IExpressionEvaluator _evaluator;
    private readonly IConditionalBlockProcessor _conditional;

    public RepeaterBlockProcessor(IExpressionParser parser, IExpressionEvaluator evaluator, IConditionalBlockProcessor conditional)
    {
        _parser = parser;
        _evaluator = evaluator;
        _conditional = conditional;
    }

    public string ProcessRepeaters(string content, JsonElement root)
    {
        var guard = 0;
        while (true)
        {
            var match = RepeaterRx.Match(content);
            if (!match.Success)
                break;
            guard++;
            if (guard > 1000)
                throw new InvalidOperationException("Too many repeater expansions.");
            var path = match.Groups["path"].Value;
            var bodyTemplate = match.Groups["body"].Value;

            var collectionEl = _evaluator.ResolvePath(root, path);
            if (collectionEl == null || collectionEl.Value.ValueKind != JsonValueKind.Array)
            {
                content = content.Remove(match.Index, match.Length);
                continue;
            }

            var arr = collectionEl.Value.EnumerateArray().ToList();
            var count = arr.Count;
            var sb = new System.Text.StringBuilder();

            for (var i = 0; i < count; i++)
            {
                var item = arr[i];
                var metadata = new Dictionary<string, string>
                {
                    ["index"] = (i + 1).ToString(),
                    ["zeroIndex"] = i.ToString(),
                    ["first"] = (i == 0).ToString().ToLowerInvariant(),
                    ["last"] = (i == count - 1).ToString().ToLowerInvariant(),
                    ["odd"] = (i % 2 == 1).ToString().ToLowerInvariant(),
                    ["even"] = (i % 2 == 0).ToString().ToLowerInvariant(),
                    ["count"] = count.ToString()
                };

                var nestedBody = bodyTemplate;
                nestedBody = ProcessRepeaters(nestedBody, item); // recursive nested repeaters
                nestedBody = _conditional.ProcessConditionals(nestedBody, item);

                nestedBody = TokenRx.Replace(nestedBody, m =>
                {
                    var raw = m.Groups["expr"].Value.Trim();
                    // metadata?
                    if (metadata.ContainsKey(raw))
                        return metadata[raw];
                    if (raw.StartsWith("repeat ", StringComparison.OrdinalIgnoreCase) ||
                        raw.Equals("endrepeat", StringComparison.OrdinalIgnoreCase))
                        return "";
                    if (raw.StartsWith("if ", StringComparison.OrdinalIgnoreCase) ||
                        raw.StartsWith("elseif ", StringComparison.OrdinalIgnoreCase) ||
                        raw.Equals("end", StringComparison.OrdinalIgnoreCase) ||
                        raw.Equals("else", StringComparison.OrdinalIgnoreCase))
                        return m.Value;

                    try
                    {
                        var parsed = _parser.Parse(raw);
                        return _evaluator.Evaluate(parsed, root, item);
                    }
                    catch
                    {
                        return "";
                    }
                });
                sb.Append(nestedBody);
            }

            content = content.Substring(0, match.Index) + sb.ToString() + content.Substring(match.Index + match.Length);
        }
        return content;
    }
}