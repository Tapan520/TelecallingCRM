using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelecallingCRM.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_TenantId",
                table: "ActivityLogs");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TenantId_AssignedToId",
                table: "Leads",
                columns: new[] { "TenantId", "AssignedToId" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TenantId_CreatedAt",
                table: "Leads",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Calls_TenantId_StartedAt",
                table: "Calls",
                columns: new[] { "TenantId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_TenantId_OccurredAt",
                table: "ActivityLogs",
                columns: new[] { "TenantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_TenantId_UserId",
                table: "ActivityLogs",
                columns: new[] { "TenantId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Leads_TenantId_AssignedToId",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_TenantId_CreatedAt",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Calls_TenantId_StartedAt",
                table: "Calls");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_TenantId_OccurredAt",
                table: "ActivityLogs");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_TenantId_UserId",
                table: "ActivityLogs");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_TenantId",
                table: "ActivityLogs",
                column: "TenantId");
        }
    }
}
