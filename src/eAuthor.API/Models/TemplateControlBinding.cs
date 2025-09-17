namespace eAuthor.Models;

public class TemplateControlBinding
{
    public Guid Id { get; set; }
    public Guid ControlId { get; set; }
    public string ColumnHeader { get; set; } = "";
    public string DataPath { get; set; } = ""; // relative path inside collection item
}