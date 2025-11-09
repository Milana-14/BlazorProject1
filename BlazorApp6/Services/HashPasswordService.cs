using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Expressions.Internal;

namespace BlazorApp6.Services
{
    public static class HashPasswordService
    {
        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);

            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 100000,
                numBytesRequested: 32));
            return hashed;
        }

        public static bool ComparePasswords(string hashedPassword, string password)
        {
            string[] parts = hashedPassword.Split('.');
            if (parts.Length != 2)
            {
                return false;
            }
            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] hash = Convert.FromBase64String(parts[1]);
            
            byte[] inputHash = KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 100000,
                numBytesRequested: 32);

            return CryptographicOperations.FixedTimeEquals(hash, inputHash);
        }
    }
}
