using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectRag.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentToIngestionItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocumentId",
                table: "IngestionItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngestionItems_DocumentId",
                table: "IngestionItems",
                column: "DocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_IngestionItems_Documents_DocumentId",
                table: "IngestionItems",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IngestionItems_Documents_DocumentId",
                table: "IngestionItems");

            migrationBuilder.DropIndex(
                name: "IX_IngestionItems_DocumentId",
                table: "IngestionItems");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                table: "IngestionItems");
        }
    }
}
