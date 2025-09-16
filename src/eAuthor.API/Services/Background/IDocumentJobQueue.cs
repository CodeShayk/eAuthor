namespace eAuthor.Services.Background;

public interface IDocumentJobQueue
{
    void SignalNewWork();

    IAsyncEnumerable<bool> GetSignalsAsync(CancellationToken ct);
}