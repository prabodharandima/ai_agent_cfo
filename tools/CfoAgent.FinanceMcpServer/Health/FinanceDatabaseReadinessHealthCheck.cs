using CfoAgent.FinanceMcpServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CfoAgent.FinanceMcpServer.Health;

public sealed class FinanceDatabaseReadinessHealthCheck(
    IServiceScopeFactory scopeFactory,
    ILogger<FinanceDatabaseReadinessHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");
            }

            if (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken) is var pendingMigrations
                && pendingMigrations.Any())
            {
                return HealthCheckResult.Unhealthy("The finance database schema is not current.");
            }

            await dbContext.Products.AsNoTracking().Select(product => product.Id).Take(1).ToArrayAsync(cancellationToken);
            await dbContext.Sales.AsNoTracking().Select(sale => sale.Id).Take(1).ToArrayAsync(cancellationToken);
            await dbContext.BudgetTargets.AsNoTracking().Select(target => target.Id).Take(1).ToArrayAsync(cancellationToken);

            return HealthCheckResult.Healthy("PostgreSQL and the finance schema are ready.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Finance database readiness failed with {ExceptionType}.",
                exception.GetType().Name);
            return HealthCheckResult.Unhealthy("PostgreSQL or the finance schema is unavailable.");
        }
    }
}
