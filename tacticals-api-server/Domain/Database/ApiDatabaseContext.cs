using System.Collections.Generic;
using System.Text.Json;
using tacticals_api_server.Domain;
using Microsoft.EntityFrameworkCore;

namespace tacticals_api_server.Domain.Database;

public class ApiDatabaseContext : DbContext
{
    public ApiDatabaseContext(DbContextOptions<ApiDatabaseContext> options) : base(options)
    {
    }

    public DbSet<UProfile> Profiles { set; get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UProfile>(entity =>
        {
            entity.ToTable("Profiles");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(e => e.Email)
                  .UseCollation("NOCASE")
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(e => e.PasswordHash)
                  .IsRequired();

            // Configure ArmySaves dictionary as JSON
            entity.Property(e => e.ArmySaves)
                  .HasConversion(
                      v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                      v => JsonSerializer.Deserialize<Dictionary<string, ArmySetupSave>>(v, (JsonSerializerOptions)null))
                  .HasColumnType("nvarchar(4000)");
        });
    }
}