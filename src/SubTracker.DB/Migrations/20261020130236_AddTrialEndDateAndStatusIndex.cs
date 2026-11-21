using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubTracker.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddTrialEndDateAndStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "TrialEndDate",
                table: "Subscriptions",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_Status_NextChargeDate",
                table: "Subscriptions",
                columns: new[] { "Status", "NextChargeDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_Status_NextChargeDate",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "TrialEndDate",
                table: "Subscriptions");
        }
    }
}
