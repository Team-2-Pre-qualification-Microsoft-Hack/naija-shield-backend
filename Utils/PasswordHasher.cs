using BCrypt.Net;

namespace naija_shield_backend.Utils;

public static class PasswordHasher
{
    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, 12);
    }

    public static bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    // Utility method to generate a hash for manual testing
    public static void Main(string[] args)
    {
        Console.WriteLine("=== NaijaShield Password Hasher ===");
        Console.WriteLine("This tool generates BCrypt hashes for initial admin user setup.");
        Console.WriteLine();
        
        Console.Write("Enter password to hash: ");
        var password = Console.ReadLine();
        
        if (string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("Error: Password cannot be empty");
            return;
        }
        
        Console.WriteLine();
        Console.WriteLine("Generating hash (cost factor: 12)...");
        var hash = HashPassword(password);
        
        Console.WriteLine();
        Console.WriteLine("=== GENERATED HASH ===");
        Console.WriteLine(hash);
        Console.WriteLine();
        Console.WriteLine("Use this hash in the 'password' field when creating your initial admin user in Cosmos DB.");
        Console.WriteLine();
        
        // Verify the hash works
        Console.WriteLine("Verifying hash...");
        var isValid = VerifyPassword(password, hash);
        Console.WriteLine($"Verification: {(isValid ? "SUCCESS ?" : "FAILED ?")}");
    }
}
