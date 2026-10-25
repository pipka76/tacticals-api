using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tacticals_api_server.Domain;
using tacticals_api_server.Domain.Database;
using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using tacticals_api_server.Domain.Auth;

namespace tacticals_api_server.Controllers;

[ApiController]
public class ProfileController : ControllerBase
{
    private ILogger<ProfileController> _logger;
    private ApiDatabaseContext _db;

    public ProfileController(ILogger<ProfileController> logger,  ApiDatabaseContext db)
    {
        _logger = logger;
        _db = db;
    }

    private Guid? ValidateAuthorization() => AuthTokens.GetProfileId(Request);

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
            Content = AuthTokens.BuildToken(p.Id)
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
