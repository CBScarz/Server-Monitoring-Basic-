using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMISMonitor.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyStatisticsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyStatistics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceName = table.Column<string>(type: "TEXT", nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalPings = table.Column<int>(type: "INTEGER", nullable: false),
                    SuccessfulPings = table.Column<int>(type: "INTEGER", nullable: false),
                    UptimePercentage = table.Column<double>(type: "REAL", nullable: false),
                    AverageLatencyMs = table.Column<double>(type: "REAL", nullable: true),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyStatistics", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyStatistics");
        }
    }
}
