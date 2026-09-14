using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Crm;

namespace UniversalPOS.Infrastructure.BackgroundJobs;

/// <summary>
/// Runs once a day and expires any loyalty point batches past their ExpiresAtUtc, for
/// every active company. Each company also has an admin-triggered
/// POST /companies/{id}/loyalty/expire-points endpoint that calls the same
/// ILoyaltyService.ExpirePointsAsync — used by integration tests so this doesn't have
/// to be verified by waiting a full day.
/// </summary>
public class PointsExpiryBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PointsExpiryBackgroundService> _logger;

    public PointsExpiryBackgroundService(IServiceScopeFactory scopeFactory, ILogger<PointsExpiryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Points expiry job failed; will retry on the next interval.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var loyaltyService = scope.ServiceProvider.GetRequiredService<ILoyaltyService>();

        var companyIds = await db.Companies.Where(c => c.IsActive).Select(c => c.Id).ToListAsync(cancellationToken);
        foreach (var companyId in companyIds)
        {
            var affected = await loyaltyService.ExpirePointsAsync(companyId, cancellationToken);
            if (affected > 0)
            {
                _logger.LogInformation("Expired loyalty points for {CustomerCount} customer(s) in company {CompanyId}.", affected, companyId);
            }
        }
    }
}
