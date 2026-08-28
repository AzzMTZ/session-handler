using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SessionHandler.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveIdentityUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Close out any duplicate active sessions for the same identity triple
            // that may already exist on this database from before this fix - e.g.
            // from the exact race this PR closes, or from earlier manual/dev testing.
            // CREATE UNIQUE INDEX below fails outright if any such duplicates remain,
            // since SQLite can't build a unique index over data that already violates
            // it - and Program.cs runs Database.Migrate() on startup, so that failure
            // would surface as the app refusing to start, not just a bad migration.
            // Keeps the same "most recently opened session wins" tie-break that
            // SessionRepository.GetActiveByCompoundId already uses, and closes the
            // losing duplicates as of their own LoginAt, since they represent invalid
            // state that should never have persisted as active in the first place.
            migrationBuilder.Sql("""
                UPDATE Sessions
                SET LogoutAt = LoginAt
                WHERE LogoutAt IS NULL
                  AND Id NOT IN (
                      SELECT Id FROM (
                          SELECT Id,
                                 ROW_NUMBER() OVER (
                                     PARTITION BY TenantId, Username, Ip
                                     ORDER BY LoginAt DESC, Id DESC
                                 ) AS rn
                          FROM Sessions
                          WHERE LogoutAt IS NULL
                      )
                      WHERE rn = 1
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ActiveIdentity",
                table: "Sessions",
                columns: new[] { "TenantId", "Username", "Ip" },
                unique: true,
                filter: "\"LogoutAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_ActiveIdentity",
                table: "Sessions");
        }
    }
}
