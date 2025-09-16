namespace eAuthor.Services.Background;

public class InMemoryDocumentJobQueue : IDocumentJobQueue {
    private readonly System.Threading.Channels.Channel<bool> _channel =
        System.Threading.Channels.Channel.CreateUnbounded<bool>();
    public void SignalNewWork() {
        _channel.Writer.TryWrite(true);
    }
    public IAsyncEnumerable<bool> GetSignalsAsync(CancellationToken ct) {
        return _channel.Reader.ReadAllAsync(ct);
    }
}