using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Jiten.Core;

public class UserDbContextFactory : IDesignTimeDbContextFactory<UserDbContext>
{
    public UserDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
                            .SetBasePath(Directory.GetCurrentDirectory())
                            .AddJsonFile("sharedsettings.json", optional: true, reloadOnChange: true)
                            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                            .AddEnvironmentVariables()
                            .Build();

        var connectionString = configuration.GetConnectionString("JitenDatabase");
        var optionsBuilder = new DbContextOptionsBuilder<UserDbContext>();
        optionsBuilder.UseNpgsql(connectionString, o => { o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery); });

        return new UserDbContext(optionsBuilder.Options);
    }
}