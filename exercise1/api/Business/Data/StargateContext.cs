using Microsoft.EntityFrameworkCore;
using System.Data;

namespace StargateAPI.Business.Data
{
    public class StargateContext : DbContext
    {
        public IDbConnection Connection => Database.GetDbConnection();
        public DbSet<Person> People { get; set; }
        public DbSet<AstronautDetail> AstronautDetails { get; set; }
        public DbSet<AstronautDuty> AstronautDuties { get; set; }
        public DbSet<LogEntry> Logs { get; set; }
        private static readonly DateTime SeedT0 = new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public StargateContext(DbContextOptions<StargateContext> options)
        : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(StargateContext).Assembly);

            // SeedData(modelBuilder);

            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<LogEntry>().HasIndex(l => l.UtcTs);
            modelBuilder.Entity<LogEntry>().HasIndex(l => l.CorrelationId);
            modelBuilder.Entity<LogEntry>().HasIndex(l => l.Category);
        }

        private static void SeedData(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>().HasData(
                new Person { Id = 1, Name = "John Doe" },
                new Person { Id = 2, Name = "Jane Doe" }
            );

            modelBuilder.Entity<AstronautDetail>().HasData(
                new AstronautDetail
                {
                    Id = 1,
                    PersonId = 1,
                    CurrentRank = "1LT",
                    CurrentDutyTitle = "Commander",
                    CareerStartDate = SeedT0,   // <-- fixed fake date
                    CareerEndDate = null
                }
            );

            modelBuilder.Entity<AstronautDuty>().HasData(
                new AstronautDuty
                {
                    Id = 1,
                    PersonId = 1,
                    Rank = "1LT",
                    DutyTitle = "Commander",
                    DutyStartDate = SeedT0,     // <-- fixed fake date
                    DutyEndDate = null
                }
            );
        }

        // in StargateContext.cs
        public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            await EnforceAstronautInvariantsAsync(ct);
            return await base.SaveChangesAsync(ct);
        }

        private async Task EnforceAstronautInvariantsAsync(CancellationToken ct)
        {
            // Build the set of PersonIds that will have an AstronautDetail *after* this SaveChanges.
            // Include entries being Added or Unchanged/Modified; exclude ones being Deleted/Detached.
            var detailPids = ChangeTracker.Entries<AstronautDetail>()                 // track pending Detail rows
                .Where(e => e.State != EntityState.Deleted && e.State != EntityState.Detached)
                .Select(e => e.Entity.PersonId)                                       // project to PersonId
                .Distinct()                                                           // de-dup, we only need unique ids
                .ToList();

            // If no one will have an AstronautDetail after this save, there’s nothing to check.
            if (detailPids.Count == 0) return;

            // Compute how many *new* AstronautDuty rows are being added per PersonId in this SaveChanges.
            // We group Added rows by PersonId → count, then store in a dictionary for quick lookup.
            var addsByPid = ChangeTracker.Entries<AstronautDuty>()                    // track pending Duty rows
                .Where(e => e.State == EntityState.Added)                             // only additions
                .GroupBy(e => e.Entity.PersonId)
                .ToDictionary(g => g.Key, g => g.Count());                            // { pid => number of new duties }

            // Compute how many AstronautDuty rows are being deleted per PersonId in this SaveChanges.
            var deletesByPid = ChangeTracker.Entries<AstronautDuty>()
                .Where(e => e.State == EntityState.Deleted)                           // only deletions
                .GroupBy(e => e.Entity.PersonId)
                .ToDictionary(g => g.Key, g => g.Count());                            // { pid => number of deletions }

            // For each Person who will have an AstronautDetail…
            foreach (var pid in detailPids)
            {
                // Count how many duty rows currently exist in the database for this Person (ignoring the tracker).
                // AsNoTracking prevents EF from “seeing” tracked changes here—we want the committed DB state.
                var dbCount = await AstronautDuties.AsNoTracking()
                    .CountAsync(d => d.PersonId == pid, ct);

                // Project the number of duties that will exist *after* SaveChanges:
                // future = current in DB + pending adds − pending deletes
                var future = dbCount
                    + (addsByPid.TryGetValue(pid, out var a) ? a : 0)
                    - (deletesByPid.TryGetValue(pid, out var d) ? d : 0);

                // If the future count is 0 or negative, we’re about to persist an AstronautDetail
                // without any AstronautDuty rows — that violates the business rule.
                if (future <= 0)
                    throw new InvalidOperationException(
                        $"Invariant violated: PersonId={pid} has AstronautDetail but no AstronautDuty.");
            }
        }

    }
}
