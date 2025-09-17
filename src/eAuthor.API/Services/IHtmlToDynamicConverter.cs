using eAuthor.Models;

namespace eAuthor.Services
{
    public interface IHtmlToDynamicConverter
    {
        List<TemplateControl> Convert(string html);
    }
}