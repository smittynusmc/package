using System.Net;
using MediatR;
using MediatR.Pipeline;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;

namespace StargateAPI.Business.Commands
{
    public class CreatePerson : IRequest<CreatePersonResult>
    {
        public required string Name { get; set; } = string.Empty;
    }

    public class CreatePersonPreProcessor : IRequestPreProcessor<CreatePerson>
    {
        private readonly StargateContext _context;
        public CreatePersonPreProcessor(StargateContext context)
        {
            _context = context;
        }
        public async Task Process(CreatePerson request, CancellationToken cancellationToken)
        {
            // Basic input guard (optional but nice)
            var raw = request.Name?.Trim();
            if (string.IsNullOrEmpty(raw))
                throw new HttpRequestException("Name is required.", null, HttpStatusCode.BadRequest);

            // Normalize and check against the computed/indexed column
            var norm = raw.ToUpperInvariant();

            var exists = await _context.People
                .AsNoTracking()
                .AnyAsync(p => p.NormalizedName == norm, cancellationToken);

            if (exists)
                // 409 → frontend can show “name already exists”
                throw new HttpRequestException("That person name already exists.", null, HttpStatusCode.Conflict);
        }
    }

    public class CreatePersonHandler : IRequestHandler<CreatePerson, CreatePersonResult>
    {
        private readonly StargateContext _context;
        public CreatePersonHandler(StargateContext context) => _context = context;

        public async Task<CreatePersonResult> Handle(CreatePerson request, CancellationToken cancellationToken)
        {
            var newPerson = new Person { Name = request.Name };
            await _context.People.AddAsync(newPerson, cancellationToken);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);

                return new CreatePersonResult
                {
                    Success = true,
                    ResponseCode = StatusCodes.Status201Created,
                    Id = newPerson.Id,
                    Message = "Created."
                };
            }
            catch (DbUpdateException ex) when (IsSqliteUniqueConstraint(ex))
            {
                // DB is authoritative (unique index on NormalizedName)
                return new CreatePersonResult
                {
                    Success = false,
                    ResponseCode = StatusCodes.Status409Conflict,
                    Message = "That person name already exists."
                };
            }
        }

        private static bool IsSqliteUniqueConstraint(DbUpdateException ex)
        {
            var se = ex.InnerException as SqliteException ?? ex.GetBaseException() as SqliteException;
            // 19 = SQLITE_CONSTRAINT; (extended 2067 = UNIQUE)
            return se != null && se.SqliteErrorCode == 19;
        }
    }

    public class CreatePersonResult : BaseResponse
    {
        public int Id { get; set; }
    }
}
