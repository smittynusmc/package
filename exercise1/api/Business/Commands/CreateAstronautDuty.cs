using Dapper;
using MediatR;
using MediatR.Pipeline;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Common;
using StargateAPI.Controllers;
using System.Net;

namespace StargateAPI.Business.Commands
{
    public class CreateAstronautDuty : IRequest<CreateAstronautDutyResult>
    {
        public required string Name { get; set; }

        public required string Rank { get; set; }

        public required string DutyTitle { get; set; }

        public DateTime DutyStartDate { get; set; }
    }

    public class CreateAstronautDutyPreProcessor : IRequestPreProcessor<CreateAstronautDuty>
    {
        private readonly StargateContext _context;
        public CreateAstronautDutyPreProcessor(StargateContext context)
        {
            _context = context;

        }

        public Task Process(CreateAstronautDuty request, CancellationToken cancellationToken)
        {
            var person = _context.People.AsNoTracking().FirstOrDefault(z => z.Name == request.Name);

            if (person is null) throw new BadHttpRequestException("Bad Request");

            var verifyNoPreviousDuty = _context.AstronautDuties.FirstOrDefault(z => z.DutyTitle == request.DutyTitle && z.DutyStartDate == request.DutyStartDate);

            if (verifyNoPreviousDuty is not null) throw new BadHttpRequestException("Bad Request");

            return Task.CompletedTask;
        }
    }

    public class CreateAstronautDutyHandler : IRequestHandler<CreateAstronautDuty, CreateAstronautDutyResult>
    {
        private readonly StargateContext _context;
        private readonly ILogger<CreateAstronautDutyHandler> _logger;

        public CreateAstronautDutyHandler(StargateContext context, ILogger<CreateAstronautDutyHandler> logger)
        {
            _context = context;
            _logger = logger;
        }
        public async Task<CreateAstronautDutyResult> Handle(CreateAstronautDuty request, CancellationToken cancellationToken)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("[CreateAstronautDuty] start | name={Name} rank={Rank} title={Title} start={Start}",
                request.Name, request.Rank, request.DutyTitle, request.DutyStartDate.ToString("yyyy-MM-dd"));

            // 1) Person lookup
            var person = await _context.Connection.QueryFirstOrDefaultAsync<Person>(
                "SELECT * FROM [Person] WHERE Name = @name",
                new { name = request.Name });

            if (person is null)
            {
                _logger.LogWarning("[CreateAstronautDuty] person not found | name={Name}", request.Name);
                throw new BadHttpRequestException("Bad Request: person not found.");
            }
            _logger.LogInformation("[CreateAstronautDuty] person found | personId={PersonId} name={Name}", person.Id, person.Name);

            // Helpers
            static string fmtDate(DateOnly? d) => d.HasValue ? d.Value.ToString("yyyy-MM-dd") : "(none)";

            // 2) Load duties (tracked), oldest → newest
            var duties = await _context.AstronautDuties
                .Where(d => d.PersonId == person.Id)
                .OrderBy(d => d.DutyStartDate)
                .ToListAsync(cancellationToken);

            // Normalize to DateOnly for all comparisons
            var segments = duties
                .Select(x => new
                {
                    Entity = x,
                    Start = DateOnlyUtils.ToDateOnly(x.DutyStartDate),
                    End = x.DutyEndDate.HasValue ? DateOnlyUtils.ToDateOnly(x.DutyEndDate.Value) : (DateOnly?)null
                })
                .OrderBy(s => s.Start)
                .ToList();

            _logger.LogInformation("[CreateAstronautDuty] existing duties loaded | count={Count}", segments.Count);

            var newStart = DateOnlyUtils.ToDateOnly(request.DutyStartDate); // new duty start (DateOnly)
            var nextAfterS = segments.FirstOrDefault(s => s.Start.CompareTo(newStart) > 0);   // strictly after newStart
            var prevBeforeS = segments.LastOrDefault(s => s.Start.CompareTo(newStart) < 0);    // strictly before newStart
            var openCurrent = segments.LastOrDefault(s => s.End == null);               // current (open) duty

            _logger.LogInformation(
                "[CreateAstronautDuty] neighbors | prevStart={PrevStart} prevEnd={PrevEnd} nextStart={NextStart} openStart={OpenStart}",
                fmtDate(prevBeforeS?.Start), fmtDate(prevBeforeS?.End), fmtDate(nextAfterS?.Start), fmtDate(openCurrent?.Start));

            // 3) If the new duty begins AFTER the current open duty, it becomes the new current.
            // Close the open duty to newStart-1. (Backfilled historical inserts won't satisfy this.)
            if (openCurrent != null && newStart.CompareTo(openCurrent.Start) > 0)
            {
                var newEnd = newStart.AddDays(-1);
                _logger.LogInformation("[CreateAstronautDuty] closing open duty | openStart={OpenStart} newEnd={NewEnd}",
                    fmtDate(openCurrent.Start), fmtDate(newEnd));

                openCurrent.Entity.DutyEndDate = DateOnlyUtils.ToDateTimeAtMidnight(newEnd);
                _context.AstronautDuties.Update(openCurrent.Entity);
                openCurrent = null; // it's no longer open after we set an end
            }

            // 4) Trim the previous neighbor to end on newStart-1 (remove overlaps)
            if (prevBeforeS != null && (!prevBeforeS.End.HasValue || prevBeforeS.End.Value.CompareTo(newStart) >= 0))
            {
                var trimmedEnd = newStart.AddDays(-1);
                _logger.LogInformation("[CreateAstronautDuty] trimming previous duty | prevStart={PrevStart} prevOldEnd={PrevOldEnd} prevNewEnd={PrevNewEnd}",
                    fmtDate(prevBeforeS.Start), fmtDate(prevBeforeS.End), fmtDate(trimmedEnd));

                prevBeforeS.Entity.DutyEndDate = DateOnlyUtils.ToDateTimeAtMidnight(trimmedEnd);
                _context.AstronautDuties.Update(prevBeforeS.Entity);
                prevBeforeS = new { prevBeforeS.Entity, prevBeforeS.Start, End = (DateOnly?)trimmedEnd };
            }

            // 5) Create the new duty; if there is a "next", its end is next.Start - 1, else it's open
            var newDutyEnd = nextAfterS != null ? nextAfterS.Start.AddDays(-1) : (DateOnly?)null;

            var newDuty = new AstronautDuty
            {
                PersonId = person.Id,
                Rank = request.Rank,
                DutyTitle = request.DutyTitle,
                DutyStartDate = DateOnlyUtils.ToDateTimeAtMidnight(newStart),
                DutyEndDate = newDutyEnd.HasValue ? DateOnlyUtils.ToDateTimeAtMidnight(newDutyEnd.Value) : (DateTime?)null
            };
            await _context.AstronautDuties.AddAsync(newDuty, cancellationToken);

            _logger.LogInformation("[CreateAstronautDuty] new duty staged | title={Title} rank={Rank} start={Start} end={End}",
                newDuty.DutyTitle, newDuty.Rank, newStart.ToString("yyyy-MM-dd"), fmtDate(newDutyEnd));

            // 6) AstronautDetail (snapshot of *current* state)
            var astronautDetail = await _context.AstronautDetails
                .FirstOrDefaultAsync(d => d.PersonId == person.Id, cancellationToken);

            var newIsCurrent = !newDutyEnd.HasValue; // current iff no end
            var retired = string.Equals(request.DutyTitle, "RETIRED", StringComparison.OrdinalIgnoreCase);

            // Earliest career start across all known segments (existing + this new one)
            var earliestAcross = segments.Count > 0
                ? (segments[0].Start.CompareTo(newStart) < 0 ? segments[0].Start : newStart)
                : newStart;

            if (astronautDetail == null)
            {
                // Pick the correct "current" for snapshot: prefer any existing open (still none after closure), else the new one if open.
                var currentForSnapshot = openCurrent?.Entity ?? (newIsCurrent ? newDuty : null);

                astronautDetail = new AstronautDetail
                {
                    PersonId = person.Id,
                    CurrentDutyTitle = currentForSnapshot?.DutyTitle,
                    CurrentRank = currentForSnapshot?.Rank,
                    CareerStartDate = DateOnlyUtils.ToDateTimeAtMidnight(earliestAcross),
                    CareerEndDate = string.Equals(currentForSnapshot?.DutyTitle, "RETIRED", StringComparison.OrdinalIgnoreCase)
                        ? (currentForSnapshot == newDuty ? DateOnlyUtils.ToDateTimeAtMidnight(newStart.AddDays(-1))
                           : DateOnlyUtils.ToDateTimeAtMidnight(DateOnlyUtils.ToDateOnly(currentForSnapshot!.DutyStartDate).AddDays(-1)))
                        : null
                };
                await _context.AstronautDetails.AddAsync(astronautDetail, cancellationToken);

                _logger.LogInformation("[CreateAstronautDuty] detail created | currentTitle={CurrTitle} currentRank={CurrRank} careerStart={CareerStart} careerEnd={CareerEnd}",
                    astronautDetail.CurrentDutyTitle, astronautDetail.CurrentRank,
                    astronautDetail.CareerStartDate.ToString("yyyy-MM-dd"),
                    astronautDetail.CareerEndDate?.ToString("yyyy-MM-dd") ?? "(none)");
            }
            else
            {
                // Move career start back if backfilled earlier than recorded
                if (astronautDetail.CareerStartDate == default || DateOnlyUtils.ToDateTimeAtMidnight(earliestAcross).Date < astronautDetail.CareerStartDate.Date)
                {
                    _logger.LogInformation("[CreateAstronautDuty] career start moved earlier | oldStart={Old} newStart={New}",
                        astronautDetail.CareerStartDate.ToString("yyyy-MM-dd"), DateOnlyUtils.ToDateTimeAtMidnight(earliestAcross).ToString("yyyy-MM-dd"));

                    astronautDetail.CareerStartDate = DateOnlyUtils.ToDateTimeAtMidnight(earliestAcross);
                }

                if (newIsCurrent)
                {
                    astronautDetail.CurrentDutyTitle = request.DutyTitle;
                    astronautDetail.CurrentRank = request.Rank;

                    if (retired)
                    {
                        astronautDetail.CareerEndDate = DateOnlyUtils.ToDateTimeAtMidnight(newStart.AddDays(-1)); // end day BEFORE retirement starts
                    }
                }
                _context.AstronautDetails.Update(astronautDetail);
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                sw.Stop();
                _logger.LogInformation("[CreateAstronautDuty] success | newDutyId={Id} elapsedMs={Elapsed}",
                    newDuty.Id, sw.ElapsedMilliseconds);
                return new CreateAstronautDutyResult { Id = newDuty.Id };
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[CreateAstronautDuty] failure | personId={PersonId} name={Name} elapsedMs={Elapsed}",
                    person.Id, person.Name, sw.ElapsedMilliseconds);
                throw;
            }
        }
        

    }

    public class CreateAstronautDutyResult : BaseResponse
    {
        public int? Id { get; set; }
    }
}
