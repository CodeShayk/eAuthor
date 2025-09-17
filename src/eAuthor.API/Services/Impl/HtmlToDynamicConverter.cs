using System.Text.RegularExpressions;
using eAuthor.Models;

namespace eAuthor.Services.Impl;

public class HtmlToDynamicConverter : IHtmlToDynamicConverter
{
    private static readonly Regex TokenRx = new(@"\{\{\s*(?<path>\/[A-Za-z0-9_\/\[\]]+)(?<filters>(\s*\|\s*[^}]+)*)\s*\}\}",
        RegexOptions.Compiled);

    public List<TemplateControl> Convert(string html)
    {
        // Very simple heuristic: each unique absolute path -> TextBox control unless path seems boolean
        var matches = TokenRx.Matches(html);
        var controls = new Dictionary<string, TemplateControl>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in matches)
        {
            var path = m.Groups["path"].Value;
            if (!controls.ContainsKey(path))
            {
                var type = InferTypeFromPath(path);
                controls[path] = new TemplateControl
                {
                    Id = Guid.NewGuid(),
                    ControlType = type,
                    DataPath = path,
                    Label = path.Split('/').Last()
                };
            }
        }
        return controls.Values.ToList();
    }

    private string InferTypeFromPath(string path)
    {
        var lower = path.ToLower();
        if (lower.Contains("is") || lower.StartsWith("/is") || lower.EndsWith("flag"))
            return "CheckBox";
        return "TextBox";
    }
}