using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FlashFix.Api.Data;

public sealed class DesignTimeDbFactory : IDesignTimeDbContextFactory<FlashFixDb>
{
    public FlashFixDb CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("FLASHFIX_POSTGRES_CONNECTION")
            ?? "Host=localhost;Database=flashfix;Username=flashfix;Password=design-time";
        var options = new DbContextOptionsBuilder<FlashFixDb>().UseNpgsql(connection).Options;
        return new FlashFixDb(options);
    }
}
