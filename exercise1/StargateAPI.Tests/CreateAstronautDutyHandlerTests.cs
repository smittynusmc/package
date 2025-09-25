// Tests/CreateAstronautDutyHandlerTests.cs
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;

namespace StargateAPI.Tests;

public class CreateAstronautDutyHandlerTests
{
    [Fact]
    public async Task NewDuty_ClosesPreviousOpen_And_UpdatesDetail()
    {
        var (ctx, _) = SqliteInMemory.CreateContext();

        var person = new Person { Name = "Samantha Carter" };
        ctx.People.Add(person);
        await ctx.SaveChangesAsync();

        // Existing open duty
        var startA = new DateTime(2020, 1, 1);
        ctx.AstronautDuties.Add(new AstronautDuty
        {
            PersonId = person.Id,
            Rank = "Captain",
            DutyTitle = "Engineer",
            DutyStartDate = startA
        });
        ctx.AstronautDetails.Add(new AstronautDetail
        {
            PersonId = person.Id,
            CurrentRank = "Captain",
            CurrentDutyTitle = "Engineer",
            CareerStartDate = startA
        });
        await ctx.SaveChangesAsync();

        // Insert new duty later
        var handler = new CreateAstronautDutyHandler(ctx, NullLogger<CreateAstronautDutyHandler>.Instance);
        var newStart = new DateTime(2021, 6, 1);
        var res = await handler.Handle(new CreateAstronautDuty
        {
            Name = person.Name,
            Rank = "Major",
            DutyTitle = "Commander",
            DutyStartDate = newStart
        }, CancellationToken.None);

        res.Success.Should().BeTrue();
        var duties = await ctx.AstronautDuties.Where(d => d.PersonId == person.Id).OrderBy(d => d.DutyStartDate).ToListAsync();
        duties.Should().HaveCount(2);

        // First duty is now closed on day before the new start
        duties[0].DutyEndDate.Should().NotBeNull();
        duties[0].DutyEndDate!.Value.Date.Should().Be(new DateTime(2021, 5, 31));

        // Detail reflects the new current
        var detail = await ctx.AstronautDetails.SingleAsync(d => d.PersonId == person.Id);
        detail.CurrentDutyTitle.Should().Be("Commander");
        detail.CurrentRank.Should().Be("Major");
    }
}
