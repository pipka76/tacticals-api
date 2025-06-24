using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tacticals_api_server.Domain;
using tacticals_api_server.Domain.Database;
using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Globalization;
using System.Runtime.Intrinsics.Arm;
using System.Text.Json;
using Aes = System.Security.Cryptography.Aes;

namespace tacticals_api_server.Controllers;

[ApiController]
public class ProfileController : ControllerBase
{
    private ILogger<ProfileController> _logger;
    private ApiDatabaseContext _db;
    private string _encryptionKey;

    public ProfileController(ILogger<ProfileController> logger,  ApiDatabaseContext db)
    {
        _logger = logger;
        _db = db;
        _encryptionKey = GetRandomKey();
    }

    private string GetRandomKey()
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

    private bool ValidateToken(string token, out Guid profileId)
    {
        profileId = Guid.Empty;
        try
        {
            // Replace with your actual 32-byte key (UTF8)
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
        catch
        {
            return false;
        }
    }

    private Guid? ValidateAuthorization()
    {
        // 1) Look for an Authorization header
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
            return null;

        // 2) The header is usually in the form "Bearer <token>"
        var headerValue = authHeader.ToString();
        if (!headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = headerValue.Substring("Bearer ".Length).Trim();
        if (!ValidateToken(token, out Guid profileId))
            return null;
        
        return profileId;
    }

    private string BuildToken(Guid profileId)
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

    [HttpPost]
    [Route("profile/login/{email}")]
    public async Task<IActionResult> Login(string email, [FromBody] string password)
    {
        string pwdHash = Convert.ToBase64String(new SHA256Managed().ComputeHash(Encoding.UTF8.GetBytes(String.Concat(email, password))));
        if (!await _db.Profiles.AnyAsync(u => u.Email == email && u.PasswordHash == pwdHash))
            return Unauthorized();

        var p = await _db.Profiles.FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == pwdHash);
        return new ContentResult()
        {
            StatusCode = 200,
            ContentType = "text/html",
            Content = BuildToken(p.Id)
        };
    }

    [HttpGet]
    [Route("profile/get")]
    public async Task<IActionResult> Get()
    {
        var profileId = ValidateAuthorization();
        if (!profileId.HasValue)
            return Unauthorized();
        
        var profile = await _db.Profiles.FirstOrDefaultAsync(u => u.Id == profileId);
        if(profile == null)
            return Unauthorized();

        profile.PasswordHash = String.Empty;
        
        return new ContentResult()
        {
            StatusCode = 200,
            ContentType = "application/json",
            Content = JsonSerializer.Serialize(profile)
        };
    }

    [HttpPost]
    [Route("profile/create")]
    public async Task<IActionResult> Create([FromBody] UProfile profile)
    {
        if (await _db.Profiles.AnyAsync(u => u.Email == profile.Email))
            return Unauthorized();

        if (String.IsNullOrEmpty(profile.PasswordHash))
            return Unauthorized();

        profile.Id = Guid.NewGuid();
        var pwd = profile.PasswordHash;
        profile.PasswordHash = string.Empty;
        await _db.Profiles.AddAsync(profile);
        await _db.SaveChangesAsync();
        
        profile.PasswordHash = Convert.ToBase64String(new SHA256Managed().ComputeHash(Encoding.UTF8.GetBytes(String.Concat(profile.Email, pwd))));

        _db.Profiles.Update(profile);
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPut]
    [Route("profile/update")]
    public async Task<IActionResult> Update([FromBody] UProfile profile)
    {
        var profileId = ValidateAuthorization();
        if (!profileId.HasValue)
            return Unauthorized();
        
        if (!profile.Id.Equals(profileId))
            return Unauthorized();
        
        _db.Profiles.Update(profile);
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete]
    [Route("profile/delete")]
    public async Task<IActionResult> Delete([FromBody] UProfile profile)
    {
        var profileId = ValidateAuthorization();
        if (!profileId.HasValue)
            return Unauthorized();
        
        if (!profile.Id.Equals(profileId))
            return Unauthorized();
        
        _db.Profiles.Remove(profile);
        await _db.SaveChangesAsync();

        return Ok();
    }
}
