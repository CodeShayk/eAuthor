namespace eAuthor.Models;

public class XsdDescriptor
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string RawXsd { get; set; } = "";
    public XsdNode? Root { get; set; }
}