// UPDATED: (If this file previously existed, merge changes—shown here complete.)
namespace eAuthor.Models;

public class TemplateControl
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public string ControlType { get; set; } = ""; // TextBox, TextArea, CheckBox, RadioGroup, Grid, Repeater
    public string? Label { get; set; }
    public string DataPath { get; set; } = "";
    public string? Format { get; set; }
    public string? OptionsJson { get; set; }   // For RadioGroup, enumerations; also for style config fallback
    public List<TemplateControlBinding> Bindings { get; set; } = new();

    // NEW FIELDS
    public bool IsRequired { get; set; }

    public string? DefaultValue { get; set; }
    public string? Width { get; set; }          // e.g. "200px" or "flex"
    public string? StyleJson { get; set; }      // Arbitrary styling meta (font, color, etc.)
}