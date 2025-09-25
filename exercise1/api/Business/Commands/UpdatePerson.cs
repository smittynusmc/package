using MediatR;
using MediatR.Pipeline;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;

namespace StargateAPI.Business.Commands
{
    // REQUEST ---------------------------------------------------------------
    public class UpdatePerson : IRequest<UpdatePersonResult>
    {
        public int Id { get; set; }                     // which person to update
        public required string Name { get; set; } = ""; // new name
    }

    // PRE-PROCESSOR (validation) -------------------------------------------
    public class UpdatePersonPreProcessor : IRequestPreProcessor<UpdatePerson>
    {
        private readonly StargateContext _context;
        public UpdatePersonPreProcessor(StargateContext context) => _context = context;

        public async Task Process(UpdatePerson request, CancellationToken cancellationToken)
        {
            var newName = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(newName))
                throw new BadHttpRequestException("Name is required.");

            // Must exist
            var exists = await _context.People
                .AsNoTracking()
                .AnyAsync(p => p.Id == request.Id, cancellationToken);
            if (!exists)
                throw new BadHttpRequestException($"Person with Id {request.Id} not found.");

            // New name must be unique (excluding the same Id)
            var nameTaken = await _context.People
                .AsNoTracking()
                .AnyAsync(p => p.Name == newName && p.Id != request.Id, cancellationToken);
            if (nameTaken)
                throw new BadHttpRequestException($"The name '{newName}' is already in use.");
        }
    }

    // HANDLER ---------------------------------------------------------------
    public class UpdatePersonHandler : IRequestHandler<UpdatePerson, UpdatePersonResult>
    {
        private readonly StargateContext _context;
        public UpdatePersonHandler(StargateContext context) => _context = context;

        public async Task<UpdatePersonResult> Handle(UpdatePerson request, CancellationToken cancellationToken)
        {
            var person = await _context.People.FirstAsync(p => p.Id == request.Id, cancellationToken);
            person.Name = request.Name.Trim();

            await _context.SaveChangesAsync(cancellationToken);

            return new UpdatePersonResult
            {
                Id = person.Id,
                Success = true,
                Message = "Updated",
                ResponseCode = StatusCodes.Status200OK
            };
        }
    }

    // RESULT ---------------------------------------------------------------
    public class UpdatePersonResult : BaseResponse
    {
        public int Id { get; set; }
    }
}
