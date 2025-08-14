

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingApi.EventReceiver.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                });
            
            migrationBuilder.InsertData(
                table: "BankAccounts",
                columns: new[] { "Id", "Balance" },
                values: new object[,]
                {
                    { Guid.Parse("7d445724-24ec-4d52-aa7a-ff2bac9f191d"), 0 },
                    { Guid.Parse("11111111-1111-1111-1111-111111111111"), 0 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankAccounts");
        }
    }
}