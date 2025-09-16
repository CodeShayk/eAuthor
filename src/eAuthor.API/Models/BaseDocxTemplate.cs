namespace eAuthor.Models;

public class BaseDocxTemplate {
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public byte[] FileContent { get; set; } = Array.Empty<byte>();
    public DateTime CreatedUtc { get; set; }
}