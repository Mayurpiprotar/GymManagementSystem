using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GymManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddRealisticPlatformFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApplicationId",
                table: "Trainers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Payments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionId",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedTrainerId",
                table: "Memberships",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExperienceLevel",
                table: "Memberships",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrainingGoalSpecializationId",
                table: "Memberships",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainingPreference",
                table: "Memberships",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Specializations",
                columns: table => new
                {
                    SpecializationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specializations", x => x.SpecializationId);
                });

            migrationBuilder.CreateTable(
                name: "TrainerApplications",
                columns: table => new
                {
                    TrainerApplicationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExperienceYears = table.Column<int>(type: "int", nullable: false),
                    QualificationsSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TemporaryPasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CertificationDocumentPath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    CertificationOriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ExperienceDocumentPath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ExperienceOriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    AppliedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AdminNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByAdminId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainerApplications", x => x.TrainerApplicationId);
                    table.ForeignKey(
                        name: "FK_TrainerApplications_AspNetUsers_ReviewedByAdminId",
                        column: x => x.ReviewedByAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrainerSpecializations",
                columns: table => new
                {
                    TrainerId = table.Column<int>(type: "int", nullable: false),
                    SpecializationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainerSpecializations", x => new { x.TrainerId, x.SpecializationId });
                    table.ForeignKey(
                        name: "FK_TrainerSpecializations_Specializations_SpecializationId",
                        column: x => x.SpecializationId,
                        principalTable: "Specializations",
                        principalColumn: "SpecializationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainerSpecializations_Trainers_TrainerId",
                        column: x => x.TrainerId,
                        principalTable: "Trainers",
                        principalColumn: "TrainerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainerApplicationSpecializations",
                columns: table => new
                {
                    TrainerApplicationId = table.Column<int>(type: "int", nullable: false),
                    SpecializationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainerApplicationSpecializations", x => new { x.TrainerApplicationId, x.SpecializationId });
                    table.ForeignKey(
                        name: "FK_TrainerApplicationSpecializations_Specializations_SpecializationId",
                        column: x => x.SpecializationId,
                        principalTable: "Specializations",
                        principalColumn: "SpecializationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainerApplicationSpecializations_TrainerApplications_TrainerApplicationId",
                        column: x => x.TrainerApplicationId,
                        principalTable: "TrainerApplications",
                        principalColumn: "TrainerApplicationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Specializations",
                columns: new[] { "SpecializationId", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "Strength development and muscle hypertrophy training.", "Strength & Hypertrophy" },
                    { 2, "Training focused on calorie expenditure, conditioning and fat-loss goals.", "Weight Loss & Fat Loss" },
                    { 3, "General health, fitness and physical conditioning.", "General Fitness" },
                    { 4, "Muscle development and structured resistance training.", "Muscle Building" },
                    { 5, "Movement quality, functional strength and conditioning.", "Functional Training" },
                    { 6, "Mobility, flexibility and movement improvement.", "Flexibility & Mobility" },
                    { 7, "Sport-specific conditioning, agility and performance training.", "Sports Conditioning" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trainers_ApplicationId",
                table: "Trainers",
                column: "ApplicationId",
                unique: true,
                filter: "[ApplicationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_AssignedTrainerId",
                table: "Memberships",
                column: "AssignedTrainerId");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TrainingGoalSpecializationId",
                table: "Memberships",
                column: "TrainingGoalSpecializationId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerApplications_ReviewedByAdminId",
                table: "TrainerApplications",
                column: "ReviewedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerApplicationSpecializations_SpecializationId",
                table: "TrainerApplicationSpecializations",
                column: "SpecializationId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerSpecializations_SpecializationId",
                table: "TrainerSpecializations",
                column: "SpecializationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Memberships_Specializations_TrainingGoalSpecializationId",
                table: "Memberships",
                column: "TrainingGoalSpecializationId",
                principalTable: "Specializations",
                principalColumn: "SpecializationId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Memberships_Trainers_AssignedTrainerId",
                table: "Memberships",
                column: "AssignedTrainerId",
                principalTable: "Trainers",
                principalColumn: "TrainerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Trainers_TrainerApplications_ApplicationId",
                table: "Trainers",
                column: "ApplicationId",
                principalTable: "TrainerApplications",
                principalColumn: "TrainerApplicationId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Memberships_Specializations_TrainingGoalSpecializationId",
                table: "Memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_Memberships_Trainers_AssignedTrainerId",
                table: "Memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_Trainers_TrainerApplications_ApplicationId",
                table: "Trainers");

            migrationBuilder.DropTable(
                name: "TrainerApplicationSpecializations");

            migrationBuilder.DropTable(
                name: "TrainerSpecializations");

            migrationBuilder.DropTable(
                name: "TrainerApplications");

            migrationBuilder.DropTable(
                name: "Specializations");

            migrationBuilder.DropIndex(
                name: "IX_Trainers_ApplicationId",
                table: "Trainers");

            migrationBuilder.DropIndex(
                name: "IX_Memberships_AssignedTrainerId",
                table: "Memberships");

            migrationBuilder.DropIndex(
                name: "IX_Memberships_TrainingGoalSpecializationId",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "Trainers");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "AssignedTrainerId",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "ExperienceLevel",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "TrainingGoalSpecializationId",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "TrainingPreference",
                table: "Memberships");
        }
    }
}
