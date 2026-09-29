using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservationSystem.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFeedbackTableSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Classes_ClassId",
                table: "Feedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Terms_TermId",
                table: "Reservations");

            migrationBuilder.RenameColumn(
                name: "SubmittedDate",
                table: "Feedbacks",
                newName: "SpecificDate");

            migrationBuilder.AlterColumn<int>(
                name: "TermId",
                table: "Reservations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Feedbacks",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "ReservationId",
                table: "Feedbacks",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "Feedbacks",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ClassId",
                table: "Feedbacks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssociatedInstructorId",
                table: "Feedbacks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FeedbackDate",
                table: "Feedbacks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "SpecificTimeSlot",
                table: "Feedbacks",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubmitterEmail",
                table: "Feedbacks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubmitterFullName",
                table: "Feedbacks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_AssociatedInstructorId",
                table: "Feedbacks",
                column: "AssociatedInstructorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Classes_ClassId",
                table: "Feedbacks",
                column: "ClassId",
                principalTable: "Classes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Users_AssociatedInstructorId",
                table: "Feedbacks",
                column: "AssociatedInstructorId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Terms_TermId",
                table: "Reservations",
                column: "TermId",
                principalTable: "Terms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Classes_ClassId",
                table: "Feedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Users_AssociatedInstructorId",
                table: "Feedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Terms_TermId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Feedbacks_AssociatedInstructorId",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "AssociatedInstructorId",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "FeedbackDate",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "SpecificTimeSlot",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "SubmitterEmail",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "SubmitterFullName",
                table: "Feedbacks");

            migrationBuilder.RenameColumn(
                name: "SpecificDate",
                table: "Feedbacks",
                newName: "SubmittedDate");

            migrationBuilder.AlterColumn<int>(
                name: "TermId",
                table: "Reservations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Feedbacks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ReservationId",
                table: "Feedbacks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "Feedbacks",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ClassId",
                table: "Feedbacks",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Classes_ClassId",
                table: "Feedbacks",
                column: "ClassId",
                principalTable: "Classes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Terms_TermId",
                table: "Reservations",
                column: "TermId",
                principalTable: "Terms",
                principalColumn: "Id");
        }
    }
}
