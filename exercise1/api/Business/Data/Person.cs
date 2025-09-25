using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace StargateAPI.Business.Data
{
    [Table("Person")]
    public class Person
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string NormalizedName { get; private set; } = string.Empty;

        public virtual AstronautDetail? AstronautDetail { get; set; }

        public virtual ICollection<AstronautDuty> AstronautDuties { get; set; } = new HashSet<AstronautDuty>();

    }

    public class PersonConfiguration : IEntityTypeConfiguration<Person>
    {
        public void Configure(EntityTypeBuilder<Person> builder)
        {
            builder.ToTable("Person");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            // 1) Name is required (TEXT in SQLite)
            builder.Property(p => p.Name)
                   .IsRequired()
                   .HasColumnType("TEXT");

            // 2) Computed column for normalized name (SQLite supports generated columns)
            builder.Property(p => p.NormalizedName)
                   .HasColumnType("TEXT")
                   .HasComputedColumnSql("UPPER(TRIM([Name]))", stored: true);

            // 3) Enforce uniqueness on the normalized value
            builder.HasIndex(p => p.NormalizedName)
                   .IsUnique();

            // Relationships (unchanged)
            builder.HasOne(z => z.AstronautDetail)
                   .WithOne(z => z.Person)
                   .HasForeignKey<AstronautDetail>(z => z.PersonId);

            builder.HasMany(z => z.AstronautDuties)
                   .WithOne(z => z.Person)
                   .HasForeignKey(z => z.PersonId);
        }
    }
}
