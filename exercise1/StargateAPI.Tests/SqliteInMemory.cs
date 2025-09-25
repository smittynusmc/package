// Tests/SqliteInMemory.cs
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;

namespace StargateAPI.Tests;

public static class SqliteInMemory
{
    public static (StargateContext ctx, SqliteConnection conn) CreateContext()
    {
        var conn = new SqliteConnection("DataSource=:memory:;Cache=Shared");
        conn.Open();

        var options = new DbContextOptionsBuilder<StargateContext>()
            .UseSqlite(conn)
            .EnableSensitiveDataLogging()
            .Options;

        var ctx = new StargateContext(options);
        ctx.Database.EnsureCreated(); // builds tables, indices, computed columns

        return (ctx, conn);
    }
}
