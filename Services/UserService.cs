using Microsoft.Azure.Cosmos;
using naija_shield_backend.Models;
using UserModel = naija_shield_backend.Models.User;

namespace naija_shield_backend.Services;

public interface IUserService
{
    Task<UserModel?> GetUserByEmailAsync(string email);
    Task<UserModel?> GetUserByIdAsync(string userId);
    Task<UserModel?> GetUserByInviteTokenAsync(string inviteToken);
    Task<UserModel?> GetUserByRefreshTokenAsync(string refreshToken);
    Task<UserModel> CreateUserAsync(UserModel user);
    Task<UserModel> UpdateUserAsync(UserModel user);
    Task<string> GenerateNextUserIdAsync();
}

public class UserService : IUserService
{
    private readonly Container _container;

    public UserService(CosmosClient cosmosClient, IConfiguration configuration)
    {
        var databaseName = configuration["Cosmos:DatabaseName"] ?? "NaijaShieldDB";
        var containerName = configuration["Cosmos:UserContainerName"] ?? "Users";
        
        _container = cosmosClient.GetContainer(databaseName, containerName);
    }

    public async Task<UserModel?> GetUserByEmailAsync(string email)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.email = @email AND c.type = 'user'")
                .WithParameter("@email", email);

            var iterator = _container.GetItemQueryIterator<UserModel>(query);
            var response = await iterator.ReadNextAsync();
            
            return response.FirstOrDefault();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<UserModel?> GetUserByIdAsync(string userId)
    {
        try
        {
            var response = await _container.ReadItemAsync<UserModel>(userId, new PartitionKey(userId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<UserModel?> GetUserByInviteTokenAsync(string inviteToken)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.inviteToken = @token AND c.type = 'user'")
                .WithParameter("@token", inviteToken);

            var iterator = _container.GetItemQueryIterator<UserModel>(query);
            var response = await iterator.ReadNextAsync();
            
            return response.FirstOrDefault();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<UserModel?> GetUserByRefreshTokenAsync(string refreshToken)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.refreshToken = @token AND c.type = 'user'")
                .WithParameter("@token", refreshToken);

            var iterator = _container.GetItemQueryIterator<UserModel>(query);
            var response = await iterator.ReadNextAsync();
            
            return response.FirstOrDefault();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<UserModel> CreateUserAsync(UserModel user)
    {
        var response = await _container.CreateItemAsync(user, new PartitionKey(user.Id));
        return response.Resource;
    }

    public async Task<UserModel> UpdateUserAsync(UserModel user)
    {
        var response = await _container.ReplaceItemAsync(user, user.Id, new PartitionKey(user.Id));
        return response.Resource;
    }

    public async Task<string> GenerateNextUserIdAsync()
    {
        try
        {
            // Keep trying to find a unique ID
            for (int i = 1; i <= 999; i++)
            {
                var candidateId = $"USR-{i:D3}";
                
                // Check if this ID already exists
                try
                {
                    var existing = await _container.ReadItemAsync<UserModel>(candidateId, new PartitionKey(candidateId));
                    // If we get here, the ID exists, try next one
                    continue;
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // This ID doesn't exist - use it!
                    return candidateId;
                }
            }
            
            // Fallback to GUID-based ID if somehow we have 999 users
            return $"USR-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";
        }
        catch
        {
            return "USR-001";
        }
    }
}
