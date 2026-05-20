using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectRag.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeBaseToIngestionRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "KnowledgeBaseId",
                table: "IngestionRuns",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_IngestionRuns_KnowledgeBaseId",
                table: "IngestionRuns",
                column: "KnowledgeBaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_IngestionRuns_KnowledgeBases_KnowledgeBaseId",
                table: "IngestionRuns",
                column: "KnowledgeBaseId",
                principalTable: "KnowledgeBases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IngestionRuns_KnowledgeBases_KnowledgeBaseId",
                table: "IngestionRuns");

            migrationBuilder.DropIndex(
                name: "IX_IngestionRuns_KnowledgeBaseId",
                table: "IngestionRuns");

            migrationBuilder.DropColumn(
                name: "KnowledgeBaseId",
                table: "IngestionRuns");
        }
    }
}
