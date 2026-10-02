using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralAndVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "Trainers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "Trainers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "Members",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferredByCode",
                table: "Members",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Referrals",
                columns: table => new
                {
                    ReferralId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferrerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReferrerRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReferrerMemberId = table.Column<int>(type: "int", nullable: true),
                    ReferrerTrainerId = table.Column<int>(type: "int", nullable: true),
                    ReferredMemberId = table.Column<int>(type: "int", nullable: false),
                    ReferralDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RewardClaimed = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Referrals", x => x.ReferralId);
                    table.ForeignKey(
                        name: "FK_Referrals_Members_ReferredMemberId",
                        column: x => x.ReferredMemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Referrals_Members_ReferrerMemberId",
                        column: x => x.ReferrerMemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Referrals_Trainers_ReferrerTrainerId",
                        column: x => x.ReferrerTrainerId,
                        principalTable: "Trainers",
                        principalColumn: "TrainerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferredMemberId",
                table: "Referrals",
                column: "ReferredMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferrerMemberId",
                table: "Referrals",
                column: "ReferrerMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferrerTrainerId",
                table: "Referrals",
                column: "ReferrerTrainerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Referrals");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "Trainers");

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "Trainers");

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "ReferredByCode",
                table: "Members");
        }
    }
}
