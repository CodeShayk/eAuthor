using System.Text.Json;
using DocumentFormat.OpenXml.Wordprocessing;
using ParagraphProperties = DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties;
using RunProperties = DocumentFormat.OpenXml.Wordprocessing.RunProperties;
using Underline = DocumentFormat.OpenXml.Wordprocessing.Underline;

namespace eAuthor.Services.Impl;

public class StyleRenderer : IStyleRenderer
{
    public (RunProperties? runProps, ParagraphProperties? paraProps) Build(string? styleJson)
    {
        if (string.IsNullOrWhiteSpace(styleJson))
            return (null, null);
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(styleJson);
        }
        catch
        {
            return (null, null);
        }

        RunProperties? rp = null;
        ParagraphProperties? pp = null;

        string? GetString(string name) =>
            doc.RootElement.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
                ? el.GetString()
                : null;

        bool? GetBool(string name) =>
            doc.RootElement.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.True
                ? true
                : el.ValueKind == System.Text.Json.JsonValueKind.False ? false : null;

        // Run-level
        var fontSize = GetString("fontSize"); // e.g. "12pt" or "24"
        var bold = GetBool("bold");
        var italic = GetBool("italic");
        var underline = GetBool("underline");
        var color = GetString("color"); // #RRGGBB or hex
        var background = GetString("backgroundColor");

        if (fontSize != null || bold.HasValue || italic.HasValue || underline.HasValue || color != null || background != null)
        {
            rp = new RunProperties();
            if (fontSize != null)                 // Convert pt (e.g. "12pt") to half-points; Word uses half-point units as string
                if (fontSize.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
                    if (double.TryParse(fontSize[..^2], out var pt))
                        rp.Append(new FontSize { Val = ((int)(pt * 2)).ToString() });
                    else                     // Assume already half-points or integer of half-points
                        rp.Append(new FontSize { Val = fontSize });
            if (bold == true)
                rp.Append(new Bold());
            if (italic == true)
                rp.Append(new Italic());
            if (underline == true)
                rp.Append(new Underline { Val = UnderlineValues.Single });
            if (!string.IsNullOrWhiteSpace(color))
            {
                var hex = color.TrimStart('#');
                if (hex.Length == 3)
                    hex = string.Concat(hex.Select(c => $"{c}{c}"));
                if (hex.Length == 6)
                    rp.Append(new Color { Val = hex.ToUpperInvariant() });
            }
            if (!string.IsNullOrWhiteSpace(background))
            {
                var hex = background.TrimStart('#');
                if (hex.Length == 3)
                    hex = string.Concat(hex.Select(c => $"{c}{c}"));
                if (hex.Length == 6)
                    rp.Append(new Shading { Fill = hex.ToUpperInvariant(), Val = ShadingPatternValues.Clear, Color = "auto" });
            }
        }

        // Paragraph-level
        var alignment = GetString("alignment"); // left|center|right|justify
        var spacingBefore = GetString("spacingBefore"); // in pt maybe
        var spacingAfter = GetString("spacingAfter");

        if (alignment != null || spacingBefore != null || spacingAfter != null)
        {
            pp = new ParagraphProperties();
            if (alignment != null)
            {
                var jv = alignment.ToLower() switch
                {
                    "center" => JustificationValues.Center,
                    "right" => JustificationValues.Right,
                    "justify" => JustificationValues.Both,
                    _ => JustificationValues.Left
                };
                pp.Append(new Justification { Val = jv });
            }

            if (spacingBefore != null || spacingAfter != null)
            {
                var spacing = new SpacingBetweenLines();
                if (spacingBefore != null && double.TryParse(spacingBefore.Replace("pt", ""), out var bef))
                    spacing.Before = ((int)(bef * 20)).ToString(); // twentieths of a point
                if (spacingAfter != null && double.TryParse(spacingAfter.Replace("pt", ""), out var aft))
                    spacing.After = ((int)(aft * 20)).ToString();
                pp.Append(spacing);
            }
        }

        return (rp, pp);
    }
}