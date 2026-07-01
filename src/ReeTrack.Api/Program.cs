using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

LoadDotEnvFile();

var builder = WebApplication.CreateBuilder(args);

// builder.Services.AddDbContext<AppDbContext>(o =>
//o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddControllers();
builder.Services.AddOpenApi();                          // native doc at /openapi/v1.json

var frontendOrigin = builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173";
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p
    .WithOrigins(frontendOrigin)                        // Vite dev server
    .AllowAnyHeader().AllowAnyMethod()));

// builder.Services.AddAuthentication(...).AddJwtBearer(...);  // Google, later
// builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();                         // UI at /scalar
}

app.UseHttpsRedirection();
app.UseCors("frontend");
// app.UseAuthentication();
// app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Loads KEY=VALUE pairs from a ".env" file (searched for from the app's base
// directory upward) into process environment variables, so they flow into
// IConfiguration via the built-in environment variables provider. Existing
// environment variables always take precedence over the file.
static void LoadDotEnvFile()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    for (var depth = 0; depth < 6 && directory is not null; depth++, directory = directory.Parent)
    {
        var envPath = Path.Combine(directory.FullName, ".env");
        if (!File.Exists(envPath)) continue;

        foreach (var rawLine in File.ReadAllLines(envPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0) continue;

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"');

            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }

        break;
    }
}

public partial class Program { }
