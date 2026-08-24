using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicationReminderDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MedicationReminderDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrowserPushSubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledOn = table.Column<DateOnly>(type: "date", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationReminderDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicationReminderDeliveries_BrowserPushSubscriptions_Brows~",
                        column: x => x.BrowserPushSubscriptionId,
                        principalTable: "BrowserPushSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MedicationReminderDeliveries_Medications_MedicationId",
                        column: x => x.MedicationId,
                        principalTable: "Medications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicationReminderDeliveries_BrowserPushSubscriptionId",
                table: "MedicationReminderDeliveries",
                column: "BrowserPushSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicationReminderDeliveries_MedicationId_BrowserPushSubscr~",
                table: "MedicationReminderDeliveries",
                columns: new[] { "MedicationId", "BrowserPushSubscriptionId", "ScheduledOn" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicationReminderDeliveries");
        }
    }
}
