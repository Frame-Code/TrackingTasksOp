using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OAuthColumnsInfoAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Alias",
                table: "OpenProjectInstances",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedOAuthClientSecret",
                table: "OpenProjectInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OAuthClientId",
                table: "OpenProjectInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OAuthConnectedAt",
                table: "OpenProjectInstances",
                type: "datetime",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Alias",
                table: "OpenProjectInstances");

            migrationBuilder.DropColumn(
                name: "EncryptedOAuthClientSecret",
                table: "OpenProjectInstances");

            migrationBuilder.DropColumn(
                name: "OAuthClientId",
                table: "OpenProjectInstances");

            migrationBuilder.DropColumn(
                name: "OAuthConnectedAt",
                table: "OpenProjectInstances");
        }
    }
}
