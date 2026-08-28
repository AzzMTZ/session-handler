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
            // The unique index below can't be built while duplicate active sessions
            // exist, and Database.Migrate() runs on startup, so pre-existing duplicates
            // would block the app from booting. Close the losers here, keeping the
            // "most recently opened wins" tie-break from GetActiveByCompoundId and
            // setting LogoutAt = LoginAt. Losers go into a temp table first so the
            // event insert and the session update see one snapshot; each closed session
            // also gets a Logout event (Type 2) so the audit trail stays consistent.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE _DuplicateActiveSessions AS
                SELECT Id, TenantId, Username, Ip, LoginAt
                FROM Sessions
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

            migrationBuilder.Sql("""
                INSERT INTO SessionEvents (SessionId, TenantId, Username, Ip, Tags, Timestamp, Type)
                SELECT Id, TenantId, Username, Ip, NULL, LoginAt, 2
                FROM _DuplicateActiveSessions;
                """);

            migrationBuilder.Sql("""
                UPDATE Sessions
                SET LogoutAt = LoginAt
                WHERE Id IN (SELECT Id FROM _DuplicateActiveSessions);
                """);

            migrationBuilder.Sql("DROP TABLE _DuplicateActiveSessions;");

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
