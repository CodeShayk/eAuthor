using System.Data;

namespace eAuthor
{
    public interface IDapperContext
    {
        IDbConnection CreateConnection();
    }
}