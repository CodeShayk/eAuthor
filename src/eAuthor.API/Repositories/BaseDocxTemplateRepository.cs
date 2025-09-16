using Dapper;
using eAuthor.Models;

namespace eAuthor.Repositories;

public class BaseDocxTemplateRepository : IBaseDocxTemplateRepository {
    private readonly DapperContext _ctx;
    public BaseDocxTemplateRepository(DapperContext ctx) { _ctx = ctx; }

    public async Task<Guid> InsertAsync(BaseDocxTemplate template) {
        if (template.Id == Guid.Empty) template.Id = Guid.NewGuid();
        using var conn = _ctx.CreateConnection();
        await conn.ExecuteAsync(@"
INSERT INTO BaseDocxTemplates(Id, Name, FileContent, CreatedUtc)
VALUES(@Id, @Name, @FileContent, @CreatedUtc);", template);
        return template.Id;
    }

    public async Task<BaseDocxTemplate?> GetAsync(Guid id) {
        using var conn = _ctx.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<BaseDocxTemplate>(
            "SELECT Id, Name, FileContent, CreatedUtc FROM BaseDocxTemplates WHERE Id=@Id", new { Id = id });
    }
}