using System.Text.Json;
using eAuthor.Models;

namespace eAuthor.Services
{
    public interface IDocumentGenerationService
    {
        byte[] Generate(Template template, JsonElement dataRoot, BaseDocxTemplate? baseDoc);
    }
}