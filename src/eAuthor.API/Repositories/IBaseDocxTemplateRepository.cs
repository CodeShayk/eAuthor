using eAuthor.Models;

namespace eAuthor.Repositories;

public interface IBaseDocxTemplateRepository {
    Task<Guid> InsertAsync(BaseDocxTemplate template);
    Task<BaseDocxTemplate?> GetAsync(Guid id);
}