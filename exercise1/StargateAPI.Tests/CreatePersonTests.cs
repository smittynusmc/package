// Tests/CreatePersonTests.cs
using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;

namespace StargateAPI.Tests;

public class CreatePersonTests
{
    [Fact]
    public async Task CreatePerson_PreProcessor_BlocksDuplicates_IgnoringCaseAndTrim()
    {
        var (ctx, _) = SqliteInMemory.CreateContext();

        // Seed an existing record. NormalizedName => "JOHN DOE"
        ctx.People.Add(new Person { Name = "John Doe" });
        await ctx.SaveChangesAsync();

        var pre = new CreatePersonPreProcessor(ctx);

        // Same after TRIM + UPPER, so it should throw 409
        var req = new CreatePerson { Name = "  john DOE  " };
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => pre.Process(req, CancellationToken.None));

        ex.StatusCode.Should().Be(HttpStatusCode.Conflict);
        ex.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreatePerson_PreProcessor_RejectsBlankName_With400()
    {
        var (ctx, _) = SqliteInMemory.CreateContext();
        var pre = new CreatePersonPreProcessor(ctx);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => pre.Process(new CreatePerson { Name = "   " }, CancellationToken.None));

        ex.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Message.Should().Contain("Name is required");
    }
}
