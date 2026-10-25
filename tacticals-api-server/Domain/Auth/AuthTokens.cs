using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace tacticals_api_server.Domain.Auth;

/// <summary>
/// Player login tokens: AES-encrypted "{issuedUtc}|{profileId}", sent as "Authorization: Bearer".
/// The key is random per API process, so a restart invalidates all tokens.
/// </summary>
public static class AuthTokens
{
    private static readonly string _encryptionKey = GetRandomKey();

    private static string GetRandomKey()
    {
        // Generate a random 32-character encryption key
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var keyChars = new char[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            var byteBuffer = new byte[4];
            for (int i = 0; i < keyChars.Length; i++)
            {
                rng.GetBytes(byteBuffer);
                uint randomNum = BitConverter.ToUInt32(byteBuffer, 0);
                keyChars[i] = chars[(int)(randomNum % (uint)chars.Length)];
            }
        }
        return new string(keyChars);
    }

    public static string BuildToken(Guid profileId)
    {
        // Combine current UTC timestamp and profileId
        var dateUtc = DateTime.UtcNow.ToString("o");
        var plaintext = $"{dateUtc}|{profileId}";

        // Convert the encryption key and use a zero IV
        var key = Encoding.UTF8.GetBytes(_encryptionKey);
        var iv = new byte[16];

        byte[] cipherBytes;
        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
            using (var ms = new MemoryStream())
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plaintext);
                sw.Flush();
                cs.FlushFinalBlock();
                cipherBytes = ms.ToArray();
            }
        }

        return Convert.ToBase64String(cipherBytes);
    }

    public static bool ValidateToken(string token, out Guid profileId)
    {
        profileId = Guid.Empty;
        try
        {
            var key = Encoding.UTF8.GetBytes(_encryptionKey);
            var iv = new byte[16]; // Initialization vector of zeros

            var cipherText = Convert.FromBase64String(token);
            string plaintext;
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream(cipherText))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    plaintext = sr.ReadToEnd();
                }
            }

            var parts = plaintext.Split('|');
            if (parts.Length != 2)
                return false;

            if (!DateTime.TryParse(parts[0], null, DateTimeStyles.AdjustToUniversal, out var dateUtc))
                return false;

            if (!Guid.TryParse(parts[1], out profileId))
                return false;

            // Optionally, you could check dateUtc for expiration here
            return true;
        }
        catch(Exception ex)
        {
            Console.WriteLine("ValidateToken: " + ex.Message);
            return false;
        }
    }

    /// <summary>Reads the "Authorization: Bearer" header; null if missing or invalid.</summary>
    public static Guid? GetProfileId(HttpRequest request)
    {
        var token = GetBearer(request);
        if (token == null || !ValidateToken(token, out Guid profileId))
            return null;

        return profileId;
    }

    /// <summary>Raw bearer value of the Authorization header, or null.</summary>
    public static string? GetBearer(HttpRequest request)
    {
        // The header is usually in the form "Bearer <token>"
        var headerValue = request.Headers.Authorization.ToString();
        if (!headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        return headerValue.Substring("Bearer ".Length).Trim();
    }
}
