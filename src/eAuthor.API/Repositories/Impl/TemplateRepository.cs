using Dapper;
using eAuthor.Models;

namespace eAuthor.Repositories.Impl;

public class TemplateRepository : ITemplateRepository
{
    private readonly IDapperContext _ctx;

    public TemplateRepository(IDapperContext ctx)
    { _ctx = ctx; }

    public async Task<IEnumerable<Template>> GetAllAsync()
    {
        using var conn = _ctx.CreateConnection();
        var sql = "SELECT * FROM Templates";
        var templates = (await conn.QueryAsync<Template>(sql)).ToList();

        var controls = await conn.QueryAsync<TemplateControl>("SELECT * FROM TemplateControls");
        var bindings = await conn.QueryAsync<TemplateControlBinding>("SELECT * FROM TemplateControlBindings");

        foreach (var t in templates)
        {
            t.Controls = controls.Where(c => c.TemplateId == t.Id).ToList();
            foreach (var c in t.Controls)
                c.Bindings = bindings.Where(b => b.ControlId == c.Id).ToList();
        }
        return templates;
    }

    public async Task<Template?> GetAsync(Guid id)
    {
        using var conn = _ctx.CreateConnection();
        var t = await conn.QueryFirstOrDefaultAsync<Template>(
            "SELECT * FROM Templates WHERE Id=@Id", new { Id = id });
        if (t == null)
            return null;
        var ctrls = await conn.QueryAsync<TemplateControl>("SELECT * FROM TemplateControls WHERE TemplateId=@Id", new { Id = id });
        var bindings = await conn.QueryAsync<TemplateControlBinding>(
            "SELECT * FROM TemplateControlBindings WHERE ControlId IN @Ids",
            new { Ids = ctrls.Select(c => c.Id).ToArray() }
        );
        foreach (var c in ctrls)
            c.Bindings = bindings.Where(b => b.ControlId == c.Id).ToList();
        t.Controls = ctrls.ToList();
        return t;
    }

    public async Task UpsertAsync(Template template)
    {
        using var conn = _ctx.CreateConnection();
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync(@"
MERGE Templates AS target
USING (SELECT @Id AS Id) AS src
ON target.Id = src.Id
WHEN MATCHED THEN UPDATE SET Name=@Name, Description=@Description, HtmlBody=@HtmlBody, UpdatedUtc=@UpdatedUtc
WHEN NOT MATCHED THEN INSERT(Id,Name,Description,HtmlBody,CreatedUtc,UpdatedUtc)
VALUES (@Id,@Name,@Description,@HtmlBody,@CreatedUtc,@UpdatedUtc);",
            new
            {
                template.Id,
                template.Name,
                template.Description,
                template.HtmlBody,
                template.CreatedUtc,
                template.UpdatedUtc
            }, tx);

        // Simplistic approach: delete & reinsert controls each save
        await conn.ExecuteAsync("DELETE FROM TemplateControlBindings WHERE ControlId IN (SELECT Id FROM TemplateControls WHERE TemplateId=@TemplateId);",
            new { TemplateId = template.Id }, tx);
        await conn.ExecuteAsync("DELETE FROM TemplateControls WHERE TemplateId=@TemplateId;",
            new { TemplateId = template.Id }, tx);

        foreach (var c in template.Controls)
        {
            if (c.Id == Guid.Empty)
                c.Id = Guid.NewGuid();
            await conn.ExecuteAsync(@"
INSERT INTO TemplateControls(Id,TemplateId,ControlType,Label,DataPath,Format,OptionsJson)
VALUES(@Id,@TemplateId,@ControlType,@Label,@DataPath,@Format,@OptionsJson);", new
            {
                c.Id,
                TemplateId = template.Id,
                c.ControlType,
                c.Label,
                c.DataPath,
                c.Format,
                c.OptionsJson
            }, tx);
            foreach (var b in c.Bindings)
            {
                if (b.Id == Guid.Empty)
                    b.Id = Guid.NewGuid();
                await conn.ExecuteAsync(@"
INSERT INTO TemplateControlBindings(Id,ControlId,ColumnHeader,DataPath)
VALUES(@Id,@ControlId,@ColumnHeader,@DataPath);", new
                {
                    b.Id,
                    ControlId = c.Id,
                    b.ColumnHeader,
                    b.DataPath
                }, tx);
            }
        }

        tx.Commit();
    }
}