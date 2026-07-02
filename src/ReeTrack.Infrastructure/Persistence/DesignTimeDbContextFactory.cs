using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ReeTrack.Infrastructure.Configuration;

namespace ReeTrack.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        EnvFileLoader.Load();

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__Default is not set. Copy .env.example to .env and configure the database connection.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
