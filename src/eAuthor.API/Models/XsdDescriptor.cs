namespace eAuthor.Models;

public class XsdDescriptor {
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string RawXsd { get; set; } = "";
    public XsdNode? Root { get; set; }
}

public class XsdNode {
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsArray { get; set; }
    public List<XsdNode> Children { get; set; } = new();
    public string Path { get; set; } = ""; // /Root/Customer/Name
}