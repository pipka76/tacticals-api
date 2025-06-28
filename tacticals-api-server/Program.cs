using Microsoft.EntityFrameworkCore;
using tacticals_api_server.Domain.Database;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMvc();
builder.Services.AddDbContext<ApiDatabaseContext>(options =>
    options.UseSqlite("Data Source=tacticals.db"));

var app = builder.Build();

// Apply any pending migrations and create the database if it doesn't exist
using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<ApiDatabaseContext>();
dbContext.Database.Migrate();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}

//app.UseHttpsRedirection();
app.MapControllers();
app.Run();
