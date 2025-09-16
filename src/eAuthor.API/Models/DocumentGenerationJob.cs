namespace eAuthor.Models;

public class DocumentGenerationJob {
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public string Status { get; set; } = "Pending";
    public string InputData { get; set; } = "";
    public byte[]? ResultFile { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public Guid? CorrelationId { get; set; }
    public string? BatchGroup { get; set; }
}