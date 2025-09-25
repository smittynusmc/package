using MediatR;
using MediatR.Pipeline;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;

namespace StargateAPI.Business.Commands
{
    // REQUEST ---------------------------------------------------------------
    // Updates a person's Name by current name (case-insensitive).
    public class UpdatePersonByName : IRequest<UpdatePersonByNameResult>
    {
        public string CurrentName { get; set; } = "";
        public string NewName { get; set; } = "";
    }

    // PRE-PROCESSOR (validation) -------------------------------------------
    public class UpdatePersonByNamePreProcessor : IRequestPreProcessor<UpdatePersonByName>
    {
        private readonly StargateContext _db;
        public UpdatePersonByNamePreProcessor(StargateContext db) => _db = db;

        public async Task Process(UpdatePersonByName req, CancellationToken ct)
        {
            var current = req.CurrentName?.Trim();
            var next    = req.NewName?.Trim();

            if (string.IsNullOrWhiteSpace(current))
                throw new BadHttpRequestException("CurrentName is required.");
            if (string.IsNullOrWhiteSpace(next))
                throw new BadHttpRequestException("NewName is required.");

            // Must exist
            var person = await _db.People
                .FirstOrDefaultAsync(p => p.Name.ToLower() == current.ToLower(), ct);
            if (person is null)
                throw new BadHttpRequestException($"Person '{current}' not found.");

            // New name must be unique (excluding this person)
            var taken = await _db.People
                .AnyAsync(p => p.Name.ToLower() == next.ToLower() && p.Id != person.Id, ct);
            if (taken)
                throw new BadHttpRequestException($"The name '{next}' is already in use.");
        }
    }

    // HANDLER ---------------------------------------------------------------
    public class UpdatePersonByNameHandler : IRequestHandler<UpdatePersonByName, UpdatePersonByNameResult>
    {
        private readonly StargateContext _db;
        public UpdatePersonByNameHandler(StargateContext db) => _db = db;

        public async Task<UpdatePersonByNameResult> Handle(UpdatePersonByName req, CancellationToken ct)
        {
            var current = req.CurrentName.Trim();
            var next    = req.NewName.Trim();

            var person = await _db.People
                .FirstAsync(p => p.Name.ToLower() == current.ToLower(), ct);

            person.Name = next;
            await _db.SaveChangesAsync(ct);

            return new UpdatePersonByNameResult
            {
                PersonId    = person.Id,
                Name        = person.Name,
                Success     = true,
                Message     = "Updated",
                ResponseCode= StatusCodes.Status200OK
            };
        }
    }

    // RESULT ---------------------------------------------------------------
    public class UpdatePersonByNameResult : BaseResponse
    {
        public int    PersonId { get; set; }
        public string? Name    { get; set; }
    }
}
