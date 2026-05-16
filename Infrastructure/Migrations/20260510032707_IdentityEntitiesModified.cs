using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IdentityEntitiesModified : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCredentials");

            migrationBuilder.CreateTable(
                name: "LocalCredentials",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    EncryptedApiKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApiKeyStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApiKeyLastValidatedAt = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalCredentials", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_LocalCredentials_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OAuthCredentials",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    EncryptedOAuthAccessToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EncryptedOAuthRefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OAuthTokenExpiresAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    OAuthScope = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthCredentials", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_OAuthCredentials_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalCredentials");

            migrationBuilder.DropTable(
                name: "OAuthCredentials");

            migrationBuilder.CreateTable(
                name: "UserCredentials",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ApiKeyLastValidatedAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    ApiKeyStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    EncryptedApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EncryptedOAuthAccessToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EncryptedOAuthRefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OAuthScope = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OAuthTokenExpiresAt = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCredentials", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserCredentials_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }
    }
}
