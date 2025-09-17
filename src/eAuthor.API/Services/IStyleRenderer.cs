using DocumentFormat.OpenXml.Wordprocessing;

namespace eAuthor.Services
{
    public interface IStyleRenderer
    {
        (RunProperties? runProps, ParagraphProperties? paraProps) Build(string? styleJson);
    }
}