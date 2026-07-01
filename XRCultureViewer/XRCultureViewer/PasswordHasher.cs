using BCrypt.Net;

namespace XRCultureViewer;

public static class PasswordHasher
{
    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, 11); // Cost factor 11 for good security
    }
    
    public static bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
    
    // Helper method for generating credentials file entries
    public static void GenerateCredentialsEntry(string username, string password)
    {
        var hash = HashPassword(password);
        Console.WriteLine($"{username}:{hash}");
    }
}