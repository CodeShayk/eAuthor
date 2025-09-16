using Dapper;
using eAuthor.Models;

namespace eAuthor.Repositories;

public class DocumentGenerationJobRepository : IDocumentGenerationJobRepository
{
    private readonly DapperContext _ctx;

    public DocumentGenerationJobRepository(DapperContext ctx)
    { _ctx = ctx; }

    public async Task<IEnumerable<DocumentGenerationJob>> GetByCorrelationAsync(Guid correlationId)
    {
        using var conn = _ctx.CreateConnection();
        return await conn.QueryAsync<DocumentGenerationJob>(
            "SELECT * FROM DocumentGenerationJobs WHERE CorrelationId=@cid ORDER BY CreatedUtc",
            new { cid = correlationId });
    }

    public async Task<DocumentGenerationJob?> GetAsync(Guid id)
    {
        using var conn = _ctx.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<DocumentGenerationJob>(
            "SELECT * FROM DocumentGenerationJobs WHERE Id=@Id", new { Id = id });
    }

    public async Task InsertBatchAsync(IEnumerable<DocumentGenerationJob> jobs)
    {
        using var conn = _ctx.CreateConnection();
        using var tx = conn.BeginTransaction();
        foreach (var j in jobs)
            await conn.ExecuteAsync(@"
INSERT INTO DocumentGenerationJobs
(Id, TemplateId, Status, InputData, CreatedUtc, CorrelationId, BatchGroup)
VALUES (@Id,@TemplateId,@Status,@InputData,@CreatedUtc,@CorrelationId,@BatchGroup);", j, tx);
        tx.Commit();
    }

    public bool LockingSupported => true;

    public bool UseAppLock => false;

    public async Task<(bool, DocumentGenerationJob?)> TryStartNextPendingAsync()
    {
        using var conn = _ctx.CreateConnection();
        using var tx = conn.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        // pick one pending
        var candidate = await conn.QueryFirstOrDefaultAsync<DocumentGenerationJob>(
            "SELECT TOP 1 * FROM DocumentGenerationJobs WITH (UPDLOCK, READPAST, ROWLOCK) WHERE Status='Pending' ORDER BY CreatedUtc");
        if (candidate == null)
        {
            tx.Commit();
            return (false, candidate);
        }
        await conn.ExecuteAsync("UPDATE DocumentGenerationJobs SET Status='Processing', StartedUtc=SYSUTCDATETIME() WHERE Id=@Id",
            new { candidate.Id }, tx);
        tx.Commit();
        candidate.Status = "Processing";
        candidate.StartedUtc = DateTime.UtcNow;
        return (true, candidate);
    }

    public async Task CompleteSuccessAsync(Guid id, byte[] file)
    {
        using var conn = _ctx.CreateConnection();
        await conn.ExecuteAsync(@"UPDATE DocumentGenerationJobs
SET Status='Completed', ResultFile=@ResultFile, CompletedUtc=SYSUTCDATETIME()
WHERE Id=@Id", new { Id = id, ResultFile = file });
    }

    public async Task CompleteFailureAsync(Guid id, string error)
    {
        using var conn = _ctx.CreateConnection();
        await conn.ExecuteAsync(@"UPDATE DocumentGenerationJobs
SET Status='Failed', ErrorMessage=@Error, CompletedUtc=SYSUTCDATETIME()
WHERE Id=@Id", new { Id = id, Error = error });
    }
}