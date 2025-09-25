// Tests/AstronautInvariantsTests.cs
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;

namespace StargateAPI.Tests;

public class AstronautInvariantsTests
{
    [Fact]
    public async Task SaveChanges_Throws_When_DetailWouldExistWithNoDuties()
    {
        var (ctx, _) = SqliteInMemory.CreateContext();

        var person = new Person { Name = "Kawalsky" };
        ctx.People.Add(person);
        await ctx.SaveChangesAsync();

        var duty = new AstronautDuty
        {
            PersonId = person.Id,
            Rank = "1LT",
            DutyTitle = "Pilot",
            DutyStartDate = DateTime.UtcNow.Date
        };
        ctx.AstronautDuties.Add(duty);
        ctx.AstronautDetails.Add(new AstronautDetail
        {
            PersonId = person.Id,
            CurrentRank = "1LT",
            CurrentDutyTitle = "Pilot",
            CareerStartDate = DateTime.UtcNow.Date
        });
        await ctx.SaveChangesAsync();

        ctx.AstronautDuties.Remove(duty);
        Func<Task> act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AstronautDetail but no AstronautDuty*");
    }
}
