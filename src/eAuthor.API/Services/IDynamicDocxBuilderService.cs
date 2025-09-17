using eAuthor.Models;

namespace eAuthor.Services
{
    public interface IDynamicDocxBuilderService
    {
        byte[] Build(Template template);
    }
}