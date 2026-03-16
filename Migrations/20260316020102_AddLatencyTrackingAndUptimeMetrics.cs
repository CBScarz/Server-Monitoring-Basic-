using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMISMonitor.Migrations
{
    /// <inheritdoc />
    public partial class AddLatencyTrackingAndUptimeMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastChecked = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLatencyMs = table.Column<int>(type: "INTEGER", nullable: true),
                    AverageLatencyMs = table.Column<double>(type: "REAL", nullable: true),
                    UptimePercentage = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DowntimeEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    WentOfflineAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CameBackOnlineAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DowntimeEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LatencyRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LatencyMs = table.Column<int>(type: "INTEGER", nullable: true),
                    WasSuccessful = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LatencyRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_IpAddress",
                table: "Devices",
                column: "IpAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DowntimeEvents_DeviceId",
                table: "DowntimeEvents",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DowntimeEvents_WentOfflineAt",
                table: "DowntimeEvents",
                column: "WentOfflineAt");

            migrationBuilder.CreateIndex(
                name: "IX_LatencyRecords_DeviceId",
                table: "LatencyRecords",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_LatencyRecords_RecordedAt",
                table: "LatencyRecords",
                column: "RecordedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "DowntimeEvents");

            migrationBuilder.DropTable(
                name: "LatencyRecords");
        }
    }
}
