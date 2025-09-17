using eAuthor.Models;

namespace eAuthor.Services
{
    public interface ITemplateService
    {
        Task<IEnumerable<Template>> GetAllAsync();
        Task<Template?> GetAsync(Guid id);
        Task<Guid> SaveAsync(Template template);
    }
}