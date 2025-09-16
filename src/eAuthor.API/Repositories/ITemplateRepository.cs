using eAuthor.Models;

namespace eAuthor.Repositories;

public interface ITemplateRepository {
    Task<IEnumerable<Template>> GetAllAsync();
    Task<Template?> GetAsync(Guid id);
    Task UpsertAsync(Template template);
}