using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFirstAidTypeAndLowStockNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "StockFacets",
                type: "text",
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.CreateTable(
                name: "LowStockNotificationDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrowserPushSubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LowStockNotificationDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LowStockNotificationDeliveries_BrowserPushSubscriptions_Bro~",
                        column: x => x.BrowserPushSubscriptionId,
                        principalTable: "BrowserPushSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LowStockNotificationDeliveries_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LowStockNotificationDeliveries_BrowserPushSubscriptionId",
                table: "LowStockNotificationDeliveries",
                column: "BrowserPushSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_LowStockNotificationDeliveries_ItemId_BrowserPushSubscripti~",
                table: "LowStockNotificationDeliveries",
                columns: new[] { "ItemId", "BrowserPushSubscriptionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LowStockNotificationDeliveries");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "StockFacets");
        }
    }
}
