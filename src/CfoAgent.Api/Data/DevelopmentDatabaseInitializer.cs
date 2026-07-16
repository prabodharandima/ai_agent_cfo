using Microsoft.EntityFrameworkCore;

namespace CfoAgent.Api.Data;

public sealed class DevelopmentDatabaseInitializer(FinanceDbContext dbContext)
{
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return dbContext.Database.MigrateAsync(cancellationToken);
    }
}
