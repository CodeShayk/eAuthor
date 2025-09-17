using eAuthor.Models;
using eAuthor.Repositories;

namespace eAuthor.Services.Impl;

public class TemplateService : ITemplateService
{
    private readonly ITemplateRepository _repo;

    public TemplateService(ITemplateRepository repo)
    {
        _repo = repo;
    }

    public Task<IEnumerable<Template>> GetAllAsync() => _repo.GetAllAsync();

    public Task<Template?> GetAsync(Guid id) => _repo.GetAsync(id);

    public async Task<Guid> SaveAsync(Template template)
    {
        if (template.Id == Guid.Empty)
            template.Id = Guid.NewGuid();
        template.UpdatedUtc = DateTime.UtcNow;
        if (template.CreatedUtc == default)
            template.CreatedUtc = template.UpdatedUtc;
        await _repo.UpsertAsync(template);
        return template.Id;
    }
}