using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IdempotencyKey",
                table: "Loans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Loans_IdempotencyKey",
                table: "Loans",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Loans_IdempotencyKey",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Loans");
        }
    }
}
