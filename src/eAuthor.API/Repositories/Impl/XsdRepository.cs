using Dapper;
using eAuthor.Models;

namespace eAuthor.Repositories.Impl;

public class XsdRepository : IXsdRepository
{
    private readonly IDapperContext _ctx;

    public XsdRepository(IDapperContext ctx)
    { _ctx = ctx; }

    public async Task<int> InsertAsync(string name, string rawXsd)
    {
        using var conn = _ctx.CreateConnection();
        var id = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO Xsds(Name, RawXsd, CreatedUtc) OUTPUT INSERTED.Id VALUES(@Name,@RawXsd, SYSUTCDATETIME())",
            new { Name = name, RawXsd = rawXsd });
        return id;
    }

    public async Task<XsdDescriptor?> GetAsync(int id)
    {
        using var conn = _ctx.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<XsdDescriptor>(
            "SELECT Id, Name, RawXsd FROM Xsds WHERE Id=@Id", new { Id = id });
    }
}