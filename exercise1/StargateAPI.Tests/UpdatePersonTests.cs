// Tests/UpdatePersonTests.cs
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;

namespace StargateAPI.Tests;

public class UpdatePersonTests
{
    [Fact]
    public async Task UpdatePerson_PreProcessor_BlocksRenamingToExistingName()
    {
        var (ctx, _) = SqliteInMemory.CreateContext();
        ctx.People.AddRange(
    new Person { Name = "Daniel Jackson" }, // Id 1
    new Person { Name = "Teal'c" }          // Id 2
);
        await ctx.SaveChangesAsync();

        var pre = new UpdatePersonPreProcessor(ctx);
        // Must be EXACT (post-trim) to hit p.Name == newName && p.Id != request.Id
        var req = new UpdatePerson { Id = 2, Name = " Daniel Jackson " };

        var ex = await Assert.ThrowsAsync<BadHttpRequestException>(() => pre.Process(req, CancellationToken.None));
        ex.Message.Should().Contain("already in use");
    }

    [Fact]
    public async Task UpdatePerson_Handler_UpdatesName()
    {
        var (ctx, _) = SqliteInMemory.CreateContext();
        ctx.People.Add(new Person { Name = "Jonas Quinn" });
        await ctx.SaveChangesAsync();
        var id = await ctx.People.Select(p => p.Id).SingleAsync();

        var handler = new UpdatePersonHandler(ctx);
        var res = await handler.Handle(new UpdatePerson { Id = id, Name = "Jonas Q." }, CancellationToken.None);

        res.Success.Should().BeTrue();
        (await ctx.People.Select(p => p.Name).SingleAsync()).Should().Be("Jonas Q.");
    }
}
