using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using eAuthor.Services.Expressions;

namespace eAuthor.Services.Impl
{
    public class ConditionalBlockProcessor : IConditionalBlockProcessor
    {
        private static readonly Regex ControlTokenRx = new(
            @"\{\{\s*(?<type>if|elseif|else|end)(?:\s+(?<cond>[^}]+?))?\s*\}\}",
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
            var sb = new StringBuilder(content);

            while (true)
            {
                guard++;
                if (guard > 5000)
                    throw new InvalidOperationException("Too many conditional processing iterations (possible malformed or cyclic template).");

                var nextIfIndex = IndexOfIfToken(sb, 0);
                if (nextIfIndex < 0)
                    break;

                if (!TryExtractFullIfBlock(sb.ToString(), nextIfIndex, out var block))
                    break;

                var replacement = EvaluateBlock(block!, root);
                sb.Remove(block!.FullStart, block.FullLength);
                sb.Insert(block.FullStart, replacement);
            }

            return sb.ToString();
        }

        private int IndexOfIfToken(StringBuilder sb, int start)
        {
            var text = sb.ToString();
            var match = ControlTokenRx.Match(text, start);
            while (match.Success)
            {
                if (string.Equals(match.Groups["type"].Value, "if", StringComparison.OrdinalIgnoreCase))
                    return match.Index;
                match = match.NextMatch();
            }
            return -1;
        }

        private bool TryExtractFullIfBlock(string text, int ifStart, out IfBlock? block)
        {
            block = null;

            var firstMatch = ControlTokenRx.Match(text, ifStart);
            if (!firstMatch.Success || !IsType(firstMatch, "if"))
                return false;

            var stack = new Stack<Match>();
            stack.Push(firstMatch);

            var cursor = firstMatch.Index + firstMatch.Length;
            Match? closingEnd = null;

            while (true)
            {
                var m = ControlTokenRx.Match(text, cursor);
                if (!m.Success)
                    break;

                var type = m.Groups["type"].Value.ToLowerInvariant();

                if (type == "if")
                {
                    stack.Push(m);
                }
                else if (type == "end")
                {
                    if (stack.Count == 1)
                    {
                        closingEnd = m;
                        break;
                    }
                    stack.Pop();
                }

                cursor = m.Index + m.Length;
            }

            if (closingEnd == null)
                return false;

            var fullStart = firstMatch.Index;
            var fullLen = closingEnd.Index + closingEnd.Length - fullStart;

            var innerBodyStart = firstMatch.Index + firstMatch.Length;
            var innerBodyLen = closingEnd.Index - innerBodyStart;
            var inner = innerBodyLen > 0 ? text.Substring(innerBodyStart, innerBodyLen) : string.Empty;

            var branchTokens = new List<MatchInfo>();
            var depth = 0;

            var scanMatch = ControlTokenRx.Match(text, innerBodyStart);
            while (scanMatch.Success && scanMatch.Index < closingEnd.Index)
            {
                var t = scanMatch.Groups["type"].Value.ToLowerInvariant();

                if (t == "if")
                    depth++;
                else if (t == "end")
                    depth = Math.Max(0, depth - 1);
                else if ((t == "elseif" || t == "else") && depth == 0)
                {
                    branchTokens.Add(new MatchInfo
                    {
                        Match = scanMatch,
                        Type = t,
                        Condition = scanMatch.Groups["cond"]?.Value?.Trim()
                    });
                }

                scanMatch = scanMatch.NextMatch();
            }

            var segments = new List<Segment>();

            var firstBranchPos = branchTokens.Any()
                ? branchTokens.Min(b => b.Match.Index)
                : closingEnd.Index;

            var ifBody = text.Substring(innerBodyStart, firstBranchPos - innerBodyStart);

            segments.Add(new Segment
            {
                Kind = "if",
                Condition = firstMatch.Groups["cond"]?.Value?.Trim(),
                Body = ifBody
            });

            for (var i = 0; i < branchTokens.Count; i++)
            {
                var bt = branchTokens[i];
                if (bt.Type == "elseif")
                {
                    var segStart = bt.Match.Index + bt.Match.Length;
                    var segEndExclusive = (i + 1 < branchTokens.Count)
                        ? branchTokens[i + 1].Match.Index
                        : closingEnd.Index;
                    var body = text.Substring(segStart, segEndExclusive - segStart);
                    segments.Add(new Segment
                    {
                        Kind = "elseif",
                        Condition = bt.Condition,
                        Body = body
                    });
                }
            }

            var elseToken = branchTokens.LastOrDefault(b => b.Type == "else");
            if (elseToken != null)
            {
                var segStart = elseToken.Match.Index + elseToken.Match.Length;
                var segEndExclusive = closingEnd.Index;
                var body = text.Substring(segStart, segEndExclusive - segStart);
                segments.Add(new Segment
                {
                    Kind = "else",
                    Condition = null,
                    Body = body
                });
            }

            block = new IfBlock
            {
                FullStart = fullStart,
                FullLength = fullLen,
                Segments = segments
            };
            return true;
        }

        private string EvaluateBlock(IfBlock block, JsonElement root)
        {
            foreach (var seg in block.Segments)
            {
                if (seg.Kind == "if" || seg.Kind == "elseif")
                {
                    if (EvaluateConditionSafe(seg.Condition!, root))
                        return seg.Body;
                }
                else if (seg.Kind == "else")
                {
                    return seg.Body;
                }
            }
            return string.Empty;
        }

        private bool EvaluateConditionSafe(string expr, JsonElement root)
        {
            try
            {
                var parsed = _parser.Parse(expr);
                return _evaluator.EvaluateBoolean(root, parsed);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsType(Match m, string type) =>
            string.Equals(m.Groups["type"].Value, type, StringComparison.OrdinalIgnoreCase);

        private sealed class MatchInfo
        {
            public Match Match { get; set; } = default!;
            public string Type { get; set; } = "";
            public string? Condition { get; set; }
        }

        private sealed class Segment
        {
            public string Kind { get; set; } = "";
            public string? Condition { get; set; }
            public string Body { get; set; } = "";
        }

        private sealed class IfBlock
        {
            public int FullStart { get; set; }
            public int FullLength { get; set; }
            public List<Segment> Segments { get; set; } = new();
        }
    }
}