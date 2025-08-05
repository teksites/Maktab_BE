using System.Security.Cryptography;

namespace Users.Utils.Implementation
{
    public static class PasswordHelper
    {
        public static string HashPassword(string password)
        {
            using var deriveBytes = new Rfc2898DeriveBytes(password, 16, 10000, HashAlgorithmName.SHA256);
            var salt = deriveBytes.Salt;
            var key = deriveBytes.GetBytes(32); // 256-bit key

            return Convert.ToBase64String(salt.Concat(key).ToArray()); // Store salt+hash together
        }

        public static bool VerifyPassword(string password, string hashedPassword)
        {
            var fullBytes = Convert.FromBase64String(hashedPassword);
            var salt = fullBytes.Take(16).ToArray();
            var storedHash = fullBytes.Skip(16).ToArray();

            using var deriveBytes = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
            var computedHash = deriveBytes.GetBytes(32);

            return computedHash.SequenceEqual(storedHash);
        }
    }
}
