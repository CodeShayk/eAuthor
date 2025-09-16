namespace eAuthor.Models;

public class Template
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string HtmlBody { get; set; } = ""; // Editor content with tokens
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<TemplateControl> Controls { get; set; } = new();
}

public class TemplateControlBinding
{
    public Guid Id { get; set; }
    public Guid ControlId { get; set; }
    public string ColumnHeader { get; set; } = "";
    public string DataPath { get; set; } = ""; // relative path inside collection item
}