using eAuthor.Models;

namespace eAuthor.Repositories;

public interface IXsdRepository {
    Task<int> InsertAsync(string name, string rawXsd);
    Task<XsdDescriptor?> GetAsync(int id);
}