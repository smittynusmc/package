using MediatR;
using MediatR.Pipeline;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;

namespace StargateAPI.Business.Commands
{
    // REQUEST ---------------------------------------------------------------
    // Updates an existing AstronautDuty row by Id.
    public class UpdateAstronautDuty : IRequest<UpdateAstronautDutyResult>
    {
        public int Id { get; set; }                     // Duty Id to update (required)
        public int PersonId { get; set; }               // Keep required to prevent orphan moves
        public string Rank { get; set; } = "";
        public string DutyTitle { get; set; } = "";
        public DateTime DutyStartDate { get; set; }
        public DateTime? DutyEndDate { get; set; }
    }

    // PRE-PROCESSOR (validation) -------------------------------------------
    public class UpdateAstronautDutyPreProcessor : IRequestPreProcessor<UpdateAstronautDuty>
    {
        private readonly StargateContext _db;
        public UpdateAstronautDutyPreProcessor(StargateContext db) => _db = db;

        public async Task Process(UpdateAstronautDuty req, CancellationToken ct)
        {
            if (req.Id <= 0)
                throw new BadHttpRequestException("Duty Id is required.");

            var duty = await _db.AstronautDuties
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == req.Id, ct);
            if (duty is null)
                throw new BadHttpRequestException($"AstronautDuty with Id {req.Id} not found.");

            if (req.PersonId <= 0 ||
                !await _db.People.AsNoTracking().AnyAsync(p => p.Id == req.PersonId, ct))
                throw new BadHttpRequestException($"PersonId {req.PersonId} not found.");

            if (string.IsNullOrWhiteSpace(req.Rank))
                throw new BadHttpRequestException("Rank is required.");

            if (string.IsNullOrWhiteSpace(req.DutyTitle))
                throw new BadHttpRequestException("DutyTitle is required.");

            if (req.DutyStartDate == default)
                throw new BadHttpRequestException("DutyStartDate is required.");
        }
    }

    // HANDLER ---------------------------------------------------------------
    public class UpdateAstronautDutyHandler : IRequestHandler<UpdateAstronautDuty, UpdateAstronautDutyResult>
    {
        private readonly StargateContext _db;
        public UpdateAstronautDutyHandler(StargateContext db) => _db = db;

        public async Task<UpdateAstronautDutyResult> Handle(UpdateAstronautDuty req, CancellationToken ct)
        {
            var duty = await _db.AstronautDuties.FirstAsync(d => d.Id == req.Id, ct);

            duty.PersonId     = req.PersonId;
            duty.Rank         = req.Rank.Trim();
            duty.DutyTitle    = req.DutyTitle.Trim();
            duty.DutyStartDate= DateTime.SpecifyKind(req.DutyStartDate, DateTimeKind.Utc);
            duty.DutyEndDate  = req.DutyEndDate.HasValue
                                ? DateTime.SpecifyKind(req.DutyEndDate.Value, DateTimeKind.Utc)
                                : null;

            await _db.SaveChangesAsync(ct);

            return new UpdateAstronautDutyResult
            {
                Id           = duty.Id,
                PersonId     = duty.PersonId,
                Rank         = duty.Rank,
                DutyTitle    = duty.DutyTitle,
                DutyStartDate= duty.DutyStartDate,
                DutyEndDate  = duty.DutyEndDate,
                Success      = true,
                Message      = "Updated",
                ResponseCode = StatusCodes.Status200OK
            };
        }
    }

    // RESULT ---------------------------------------------------------------
    public class UpdateAstronautDutyResult : BaseResponse
    {
        public int Id { get; set; }
        public int PersonId { get; set; }
        public string? Rank { get; set; }
        public string? DutyTitle { get; set; }
        public DateTime DutyStartDate { get; set; }
        public DateTime? DutyEndDate { get; set; }
    }
}
