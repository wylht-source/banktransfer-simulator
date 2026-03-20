using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollLoan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmployerName",
                table: "Loans",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmploymentStatus",
                table: "Loans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExistingPayrollDeductions",
                table: "Loans",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoanType",
                table: "Loans",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlySalary",
                table: "Loans",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmployerName",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "EmploymentStatus",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "ExistingPayrollDeductions",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "LoanType",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "MonthlySalary",
                table: "Loans");
        }
    }
}
