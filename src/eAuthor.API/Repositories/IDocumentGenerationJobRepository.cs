using eAuthor.Models;

namespace eAuthor.Repositories;

public interface IDocumentGenerationJobRepository
{
    Task<IEnumerable<DocumentGenerationJob>> GetByCorrelationAsync(Guid correlationId);

    Task<DocumentGenerationJob?> GetAsync(Guid id);

    Task InsertBatchAsync(IEnumerable<DocumentGenerationJob> jobs);

    Task<(bool, DocumentGenerationJob?)> TryStartNextPendingAsync();

    Task CompleteSuccessAsync(Guid id, byte[] file);

    Task CompleteFailureAsync(Guid id, string error);
}