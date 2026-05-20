using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectRag.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDataSourceToIngestionRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DataSourceId",
                table: "IngestionRuns",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_IngestionRuns_DataSourceId",
                table: "IngestionRuns",
                column: "DataSourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_IngestionRuns_DataSources_DataSourceId",
                table: "IngestionRuns",
                column: "DataSourceId",
                principalTable: "DataSources",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IngestionRuns_DataSources_DataSourceId",
                table: "IngestionRuns");

            migrationBuilder.DropIndex(
                name: "IX_IngestionRuns_DataSourceId",
                table: "IngestionRuns");

            migrationBuilder.DropColumn(
                name: "DataSourceId",
                table: "IngestionRuns");
        }
    }
}
