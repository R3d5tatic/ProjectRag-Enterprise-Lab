using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectRag.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeBaseAndDataSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DataSourceId",
                table: "Documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "KnowledgeBaseId",
                table: "Documents",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "KnowledgeBases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    KnowledgeBaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SourceUri = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataSources_KnowledgeBases_KnowledgeBaseId",
                        column: x => x.KnowledgeBaseId,
                        principalTable: "KnowledgeBases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DataSourceId",
                table: "Documents",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_KnowledgeBaseId",
                table: "Documents",
                column: "KnowledgeBaseId");

            migrationBuilder.CreateIndex(
                name: "IX_DataSources_KnowledgeBaseId",
                table: "DataSources",
                column: "KnowledgeBaseId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBases_Name",
                table: "KnowledgeBases",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_DataSources_DataSourceId",
                table: "Documents",
                column: "DataSourceId",
                principalTable: "DataSources",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_KnowledgeBases_KnowledgeBaseId",
                table: "Documents",
                column: "KnowledgeBaseId",
                principalTable: "KnowledgeBases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_DataSources_DataSourceId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_KnowledgeBases_KnowledgeBaseId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "DataSources");

            migrationBuilder.DropTable(
                name: "KnowledgeBases");

            migrationBuilder.DropIndex(
                name: "IX_Documents_DataSourceId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_KnowledgeBaseId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DataSourceId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "KnowledgeBaseId",
                table: "Documents");
        }
    }
}
