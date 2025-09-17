using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;
using System.Text.Json;
using eAuthor.Services.Expressions;
using eAuthor.Models;

namespace eAuthor.Services.Impl;

public class DocumentGenerationService : IDocumentGenerationService
{
    private readonly IExpressionParser _parser;
    private readonly IExpressionEvaluator _evaluator;
    private readonly IConditionalBlockProcessor _conditional;
    private readonly IRepeaterBlockProcessor _repeater;
    private static readonly Regex TokenRx = new(@"\{\{\s*(?<expr>.*?)\s*\}\}", RegexOptions.Compiled);

    public DocumentGenerationService(
        IExpressionParser parser,
        IExpressionEvaluator evaluator,
        IConditionalBlockProcessor conditional,
        IRepeaterBlockProcessor repeater)
    {
        _parser = parser;
        _evaluator = evaluator;
        _conditional = conditional;
        _repeater = repeater;
    }

    public byte[] Generate(Template template, JsonElement dataRoot, BaseDocxTemplate? baseDoc)
    {
        return baseDoc == null
            ? GenerateFromHtml(template, dataRoot)
            : GenerateFromBaseDocx(template, dataRoot, baseDoc);
    }

    private byte[] GenerateFromHtml(Template template, JsonElement dataRoot)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());

            var content = template.HtmlBody;
            content = _repeater.ProcessRepeaters(content, dataRoot);
            content = _conditional.ProcessConditionals(content, dataRoot);
            content = ProcessExpressions(content, dataRoot);

            foreach (var line in content.Split('\n'))
                mainPart.Document.Body!.AppendChild(new Paragraph(new Run(new Text(line))));

            foreach (var grid in template.Controls.Where(c => c.ControlType == "Grid"))
            {
                var table = BuildGridTable(grid, dataRoot);
                if (table != null)
                    mainPart.Document.Body!.AppendChild(table);
            }
            foreach (var rep in template.Controls.Where(c => c.ControlType == "Repeater"))
            {
                var table = BuildRepeaterTable(rep, dataRoot);
                if (table != null)
                    mainPart.Document.Body!.AppendChild(table);
            }

            mainPart.Document.Save();
        }
        return ms.ToArray();
    }

    private byte[] GenerateFromBaseDocx(Template template, JsonElement dataRoot, BaseDocxTemplate baseDoc)
    {
        var copy = new MemoryStream();
        copy.Write(baseDoc.FileContent, 0, baseDoc.FileContent.Length);
        copy.Position = 0;

        using (var doc = WordprocessingDocument.Open(copy, true))
        {
            var body = doc.MainDocumentPart!.Document.Body!;

            // Process textual repeaters first (plain text paragraphs)
            ReplaceTextRepeaters(body, dataRoot);
            // Then conditionals
            ReplaceTextConditionals(body, dataRoot);
            // Replace content controls
            var sdtBlocks = body.Descendants<SdtElement>().ToList();
            foreach (var sdt in sdtBlocks)
            {
                var tag = sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
                if (string.IsNullOrEmpty(tag))
                    continue;
                var replacement = ResolveTag(tag!, dataRoot);
                ReplaceSdtContent(sdt, replacement);
            }
            // Finally expressions
            ReplaceTextTokens(body, dataRoot);

            // Append table-based controls
            foreach (var grid in template.Controls.Where(c => c.ControlType == "Grid"))
            {
                var table = BuildGridTable(grid, dataRoot);
                if (table != null)
                    body.AppendChild(table);
            }
            foreach (var rep in template.Controls.Where(c => c.ControlType == "Repeater"))
            {
                var table = BuildRepeaterTable(rep, dataRoot);
                if (table != null)
                    body.AppendChild(table);
            }

            doc.MainDocumentPart.Document.Save();
        }
        return copy.ToArray();
    }

    private void ReplaceTextRepeaters(Body body, JsonElement root)
    {
        foreach (var p in body.Descendants<Paragraph>())
        {
            var textAgg = string.Concat(p.Descendants<Text>().Select(t => t.Text));
            if (textAgg.Contains("{{ repeat "))
            {
                var processed = _repeater.ProcessRepeaters(textAgg, root);
                p.RemoveAllChildren<Run>();
                p.AppendChild(new Run(new Text(processed)));
            }
        }
    }

    private void ReplaceTextConditionals(Body body, JsonElement root)
    {
        foreach (var p in body.Descendants<Paragraph>())
        {
            var allText = string.Concat(p.Descendants<Text>().Select(t => t.Text));
            if (allText.Contains("{{ if "))
            {
                var processed = _conditional.ProcessConditionals(allText, root);
                p.RemoveAllChildren<Run>();
                p.AppendChild(new Run(new Text(processed)));
            }
        }
    }

    private string ResolveTag(string tag, JsonElement root)
    {
        var exprText = tag.StartsWith("{{") ? tag : "{{ " + tag + " }}";
        var m = TokenRx.Match(exprText);
        if (!m.Success)
            return "";
        try
        {
            var parsed = _parser.Parse(m.Groups["expr"].Value);
            return _evaluator.Evaluate(parsed, root);
        }
        catch
        {
            return "";
        }
    }

    private void ReplaceSdtContent(SdtElement sdt, string text)
    {
        var run = new Run(new Text(text ?? ""));
        var contentBlock = sdt.Descendants<SdtContentBlock>().FirstOrDefault();
        if (contentBlock != null)
        {
            contentBlock.RemoveAllChildren<Paragraph>();
            contentBlock.AppendChild(new Paragraph(run));
            return;
        }
        var contentRun = sdt.Descendants<SdtContentRun>().FirstOrDefault();
        if (contentRun != null)
        {
            contentRun.RemoveAllChildren<Run>();
            contentRun.AppendChild(run);
        }
    }

    private void ReplaceTextTokens(Body body, JsonElement root)
    {
        foreach (var text in body.Descendants<Text>())
        {
            var val = text.Text;
            if (val.Contains("{{"))
            {
                var newVal = ProcessExpressions(val, root);
                text.Text = newVal;
            }
        }
    }

    private string ProcessExpressions(string content, JsonElement root)
    {
        return TokenRx.Replace(content, match =>
        {
            var raw = match.Groups["expr"].Value.Trim();
            if (raw.StartsWith("if ", StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith("repeat ", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("endrepeat", StringComparison.OrdinalIgnoreCase))
                return ""; // those were processed earlier
            try
            {
                var parsed = _parser.Parse(raw);
                return _evaluator.Evaluate(parsed, root);
            }
            catch
            {
                return "";
            }
        });
    }

    private Table? BuildGridTable(TemplateControl grid, JsonElement root)
    {
        var collection = ResolveCollection(root, grid.DataPath);
        if (collection == null)
            return null;

        var table = CreateStdBorderedTable();
        var header = new TableRow();
        foreach (var col in grid.Bindings)
            header.Append(new TableCell(new Paragraph(new Run(new Text(col.ColumnHeader)))));
        table.Append(header);

        foreach (var item in collection.Value.EnumerateArray())
        {
            var row = new TableRow();
            foreach (var col in grid.Bindings)
            {
                var path = col.DataPath.StartsWith("/") ? col.DataPath : "/" + col.DataPath;
                var val = _evaluator.ResolvePath(item, path.TrimStart('/')) ??
                          _evaluator.ResolvePath(item, col.DataPath, item);
                var text = val?.ValueKind switch
                {
                    JsonValueKind.String => val?.GetString(),
                    JsonValueKind.Number => val?.ToString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => ""
                } ?? "";
                row.Append(new TableCell(new Paragraph(new Run(new Text(text)))));
            }
            table.Append(row);
        }
        return table;
    }

    private Table? BuildRepeaterTable(TemplateControl rep, JsonElement root)
    {
        var collection = ResolveCollection(root, rep.DataPath);
        if (collection == null)
            return null;
        var style = "table"; // later: rep.OptionsJson parse style

        if (style == "table")
        {
            var table = CreateStdBorderedTable();
            var header = new TableRow();
            foreach (var col in rep.Bindings)
                header.Append(new TableCell(new Paragraph(new Run(new Text(col.ColumnHeader)))));
            table.Append(header);
            foreach (var item in collection.Value.EnumerateArray())
            {
                var row = new TableRow();
                foreach (var col in rep.Bindings)
                {
                    var val = _evaluator.ResolvePath(item, col.DataPath, item);
                    var text = val?.ValueKind switch
                    {
                        JsonValueKind.String => val?.GetString(),
                        JsonValueKind.Number => val?.ToString(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => ""
                    } ?? "";
                    row.Append(new TableCell(new Paragraph(new Run(new Text(text)))));
                }
                table.Append(row);
            }
            return table;
        }
        return null;
    }

    private Table CreateStdBorderedTable() =>
        new(new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }
            )));

    private JsonElement? ResolveCollection(JsonElement root, string path)
    {
        var col = _evaluator.ResolvePath(root, path);
        if (col == null)
            return null;
        if (col.Value.ValueKind == JsonValueKind.Array)
            return col;
        if (col.Value.ValueKind == JsonValueKind.Object)
        {
            var arr = col.Value.EnumerateObject().FirstOrDefault(p => p.Value.ValueKind == JsonValueKind.Array);
            if (arr.Value.ValueKind == JsonValueKind.Array)
                return arr.Value;
        }
        return null;
    }
}