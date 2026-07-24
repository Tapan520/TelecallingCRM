using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelecallingCRM.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AgentId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PunchIn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PunchOut = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    WorkMinutes = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsManualEntry = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PunchedInById = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PunchedOutById = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceLogs_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceLogs_AspNetUsers_PunchedInById",
                        column: x => x.PunchedInById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceLogs_AspNetUsers_PunchedOutById",
                        column: x => x.PunchedOutById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLogs_AgentId_PunchIn",
                table: "AttendanceLogs",
                columns: new[] { "AgentId", "PunchIn" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLogs_PunchedInById",
                table: "AttendanceLogs",
                column: "PunchedInById");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLogs_PunchedOutById",
                table: "AttendanceLogs",
                column: "PunchedOutById");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLogs_TenantId_AgentId",
                table: "AttendanceLogs",
                columns: new[] { "TenantId", "AgentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceLogs");
        }
    }
}
