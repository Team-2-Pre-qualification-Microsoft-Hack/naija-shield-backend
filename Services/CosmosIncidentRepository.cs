using Microsoft.Azure.Cosmos;
using naija_shield_backend.Models;
using naija_shield_backend.Services.Interfaces;

namespace naija_shield_backend.Services;

/// <summary>
/// Persists <see cref="ThreatIncident"/> documents to Azure Cosmos DB.
/// Database and container are lazily initialised on first write using
/// <c>CreateDatabaseIfNotExistsAsync</c> / <c>CreateContainerIfNotExistsAsync</c>
/// so the app starts cleanly even if the Cosmos resources have not yet been
/// provisioned — they are created automatically on the first incident save.
/// Partition key path: <c>/channel</c> (SMS | Voice | WhatsApp).
/// </summary>
public sealed class CosmosIncidentRepository : IIncidentRepository
{
    private readonly CosmosClient _cosmos;
    private readonly IConfiguration _config;
    private readonly ILogger<CosmosIncidentRepository> _logger;

    // Lazily initialised to avoid async work in the constructor
    private Container? _container;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    /// <summary>Initialises the repository with a shared CosmosClient from DI.</summary>
    public CosmosIncidentRepository(
        CosmosClient cosmos,
        IConfiguration config,
        ILogger<CosmosIncidentRepository> logger)
    {
        _cosmos = cosmos;
        _config = config;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ThreatIncident> SaveAsync(
        ThreatIncident incident,
        CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);

        _logger.LogInformation(
            "[CosmosRepo] Saving incident Id={Id} Channel={Channel} Status={Status}",
            incident.Id, incident.Channel, incident.Status);

        var response = await container.CreateItemAsync(
            incident,
            new PartitionKey(incident.Channel),
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "[CosmosRepo] Saved. RU charge={Ru}", response.RequestCharge);

        return response.Resource;
    }

    // Ensures the database and container exist, caching the Container reference.
    private async Task<Container> GetContainerAsync(CancellationToken cancellationToken)
    {
        if (_container is not null)
            return _container;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_container is not null)
                return _container;

            var dbName = _config["CosmosDB:DatabaseName"] ?? "NaijaShieldDB";
            var containerName = _config["CosmosDB:ContainerName"] ?? "Incidents";

            _logger.LogInformation(
                "[CosmosRepo] Ensuring database={Db} container={Container} exist",
                dbName, containerName);

            var dbResponse = await _cosmos.CreateDatabaseIfNotExistsAsync(
                dbName, cancellationToken: cancellationToken);

            var containerProps = new ContainerProperties(containerName, partitionKeyPath: "/channel");
            var containerResponse = await dbResponse.Database.CreateContainerIfNotExistsAsync(
                containerProps, throughput: 400, cancellationToken: cancellationToken);

            _container = containerResponse.Container;
            return _container;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
