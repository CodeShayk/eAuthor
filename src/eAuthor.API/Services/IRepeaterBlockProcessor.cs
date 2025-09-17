using System.Text.Json;

namespace eAuthor.Services;

public interface IRepeaterBlockProcessor {
    string ProcessRepeaters(string content, JsonElement root);
}
