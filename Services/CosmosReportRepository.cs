using Microsoft.Azure.Cosmos;
using naija_shield_backend.Models;
using naija_shield_backend.Services.Interfaces;

namespace naija_shield_backend.Services;

public sealed class CosmosReportRepository : IReportRepository
{
    private readonly CosmosClient _cosmos;
    private readonly IConfiguration _config;
    private readonly ILogger<CosmosReportRepository> _logger;

    private Container? _container;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public CosmosReportRepository(
        CosmosClient cosmos,
        IConfiguration config,
        ILogger<CosmosReportRepository> logger)
    {
        _cosmos = cosmos;
        _config = config;
        _logger = logger;
    }

    public async Task<Report> SaveAsync(Report report, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);

        _logger.LogInformation(
            "[ReportRepo] Saving report Id={Id} AgencyType={Agency}", report.Id, report.AgencyType);

        var response = await container.CreateItemAsync(
            report,
            new PartitionKey(report.AgencyType),
            cancellationToken: cancellationToken);

        _logger.LogInformation("[ReportRepo] Saved. RU charge={Ru}", response.RequestCharge);
        return response.Resource;
    }

    public async Task<Report?> GetByIdAsync(
        string id,
        string agencyType,
        CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);
        try
        {
            var response = await container.ReadItemAsync<Report>(
                id, new PartitionKey(agencyType), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Report>> GetRecentAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);

        var queryDef = new QueryDefinition(
            $"SELECT TOP {limit} c.id, c.agencyType, c.generatedAt, c.generatedBy, " +
            "c.periodFrom, c.periodTo, c.summary FROM c ORDER BY c._ts DESC");

        var results  = new List<Report>();
        var iterator = container.GetItemQueryIterator<Report>(queryDef);

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results.AsReadOnly();
    }

    private async Task<Container> GetContainerAsync(CancellationToken cancellationToken)
    {
        if (_container is not null) return _container;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_container is not null) return _container;

            var dbName        = _config["CosmosDB:DatabaseName"] ?? "NaijaShieldDB";
            const string name = "Reports";

            var db = await _cosmos.CreateDatabaseIfNotExistsAsync(
                dbName, cancellationToken: cancellationToken);

            var props    = new ContainerProperties(name, partitionKeyPath: "/agencyType");
            var response = await db.Database.CreateContainerIfNotExistsAsync(
                props, cancellationToken: cancellationToken);

            _container = response.Container;
            return _container;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
