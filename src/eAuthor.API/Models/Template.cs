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