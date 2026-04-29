using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using naija_shield_backend.Models;
using naija_shield_backend.Utils;
using UserModel = naija_shield_backend.Models.User;

namespace naija_shield_backend.Scripts;

/// <summary>
/// Utility program to create the initial SYSTEM_ADMIN user in Cosmos DB.
/// This should be run once during initial setup.
/// </summary>
public class CreateAdminUser
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== NaijaShield Initial Admin User Setup ===");
        Console.WriteLine();

        // Get Cosmos DB connection string
        Console.Write("Enter Cosmos DB connection string: ");
        var connectionString = Console.ReadLine();
        
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine("Error: Connection string is required");
            return;
        }

        // Get admin details
        Console.WriteLine();
        Console.Write("Enter admin name: ");
        var name = Console.ReadLine();
        
        Console.Write("Enter admin email: ");
        var email = Console.ReadLine();
        
        Console.Write("Enter admin password: ");
        var password = ReadPassword();
        Console.WriteLine();
        
        Console.Write("Confirm password: ");
        var confirmPassword = ReadPassword();
        Console.WriteLine();

        if (password != confirmPassword)
        {
            Console.WriteLine("Error: Passwords do not match");
            return;
        }

        Console.Write("Enter organisation name (default: NaijaShield): ");
        var organisation = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(organisation))
        {
            organisation = "NaijaShield";
        }

        try
        {
            Console.WriteLine();
            Console.WriteLine("Creating admin user...");

            // Initialize Cosmos DB
            var cosmosClient = new CosmosClient(connectionString);
            var database = await cosmosClient.CreateDatabaseIfNotExistsAsync("NaijaShieldDB");
            var containerResponse = await database.Database.CreateContainerIfNotExistsAsync("Users", "/type");
            var container = containerResponse.Container;

            // Hash password
            var hashedPassword = PasswordHasher.HashPassword(password!);

            // Create admin user
            var adminUser = new UserModel
            {
                Id = "USR-001",
                Name = name!,
                Email = email!,
                Password = hashedPassword,
                Role = UserRole.SYSTEM_ADMIN,
                Organisation = organisation,
                Status = UserStatus.Active,
                InviteToken = null,
                InviteExpiry = null,
                LastActive = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                RefreshToken = null,
                RefreshTokenExpiry = null,
                FailedLoginAttempts = 0,
                LockoutUntil = null,
                Type = "user"
            };

            await container.CreateItemAsync(adminUser, new PartitionKey("user"));

            Console.WriteLine();
            Console.WriteLine("? Admin user created successfully!");
            Console.WriteLine();
            Console.WriteLine("User Details:");
            Console.WriteLine($"  ID: {adminUser.Id}");
            Console.WriteLine($"  Name: {adminUser.Name}");
            Console.WriteLine($"  Email: {adminUser.Email}");
            Console.WriteLine($"  Role: {adminUser.Role}");
            Console.WriteLine($"  Organisation: {adminUser.Organisation}");
            Console.WriteLine();
            Console.WriteLine("You can now login with these credentials.");
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            Console.WriteLine();
            Console.WriteLine("Error: A user with ID 'USR-001' already exists.");
            Console.WriteLine("Delete the existing user or use a different ID.");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Please check:");
            Console.WriteLine("  - Cosmos DB connection string is correct");
            Console.WriteLine("  - You have permission to create databases/containers");
            Console.WriteLine("  - Network connectivity to Cosmos DB");
        }
    }

    /// <summary>
    /// Reads password input without displaying characters on screen
    /// </summary>
    private static string? ReadPassword()
    {
        var password = "";
        ConsoleKeyInfo key;

        do
        {
            key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password = password[0..^1];
                Console.Write("\b \b");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password += key.KeyChar;
                Console.Write("*");
            }
        } while (key.Key != ConsoleKey.Enter);

        return password;
    }
}
