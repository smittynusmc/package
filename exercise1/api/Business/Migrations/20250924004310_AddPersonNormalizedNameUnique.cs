using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StargateAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonNormalizedNameUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rebuild Person to add STORED generated column on SQLite
            migrationBuilder.Sql(@"
        PRAGMA foreign_keys=off;

        CREATE TABLE __tmp_Person AS
        SELECT Id, Name
        FROM Person;

        DROP TABLE Person;

        CREATE TABLE Person (
            Id INTEGER NOT NULL
                CONSTRAINT PK_Person PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            NormalizedName TEXT GENERATED ALWAYS AS (UPPER(TRIM(Name))) STORED
        );

        INSERT INTO Person (Id, Name)
        SELECT Id, Name
        FROM __tmp_Person;

        DROP TABLE __tmp_Person;

        PRAGMA foreign_keys=on;
    ");

            // Unique index on normalized value
            migrationBuilder.Sql(@"
        CREATE UNIQUE INDEX IF NOT EXISTS IX_Person_NormalizedName
        ON Person (NormalizedName);
    ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop index and revert table shape (no computed column)
            migrationBuilder.Sql(@"
        PRAGMA foreign_keys=off;

        CREATE TABLE __tmp_Person AS
        SELECT Id, Name
        FROM Person;

        DROP TABLE Person;

        CREATE TABLE Person (
            Id INTEGER NOT NULL
                CONSTRAINT PK_Person PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL
        );

        INSERT INTO Person (Id, Name)
        SELECT Id, Name
        FROM __tmp_Person;

        DROP TABLE __tmp_Person;

        PRAGMA foreign_keys=on;
    ");

            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_Person_NormalizedName;");
        }

    }
}
