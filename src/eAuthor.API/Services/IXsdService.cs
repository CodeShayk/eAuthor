using eAuthor.Models;

namespace eAuthor.Services
{
    public interface IXsdService
    {
        XsdNode ParseXsd(string xsd);
    }
}