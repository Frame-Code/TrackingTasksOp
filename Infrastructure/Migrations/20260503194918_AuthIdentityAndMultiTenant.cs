using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuthIdentityAndMultiTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Projects_ProjectId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_StatusTasks_StatusTaskId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskTimeDetails_Tasks_IdTask",
                table: "TaskTimeDetails");

            migrationBuilder.DropIndex(
                name: "IX_TaskTimeDetails_IdTask",
                table: "TaskTimeDetails");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Tasks",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_StatusTaskId",
                table: "Tasks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StatusTasks",
                table: "StatusTasks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Projects",
                table: "Projects");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "TaskTimeDetails",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Tasks",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "OpenProjectInstanceId",
                table: "Tasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OpenProjectInstanceId",
                table: "StatusTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OpenProjectInstanceId",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "MigrationsData",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<int>(
                name: "OpenProjectInstanceId",
                table: "MigrationsData",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "MigrationsData",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Tasks",
                table: "Tasks",
                columns: new[] { "UserId", "WorkPackageId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_StatusTasks",
                table: "StatusTasks",
                columns: new[] { "Id", "OpenProjectInstanceId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Projects",
                table: "Projects",
                columns: new[] { "Id", "OpenProjectInstanceId" });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenProjectInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenProjectInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OpenProjectUserId = table.Column<int>(type: "int", nullable: false),
                    OpenProjectInstanceId = table.Column<int>(type: "int", nullable: false),
                    AuthMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_OpenProjectInstances_OpenProjectInstanceId",
                        column: x => x.OpenProjectInstanceId,
                        principalTable: "OpenProjectInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuthAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuthAuditLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserCredentials",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EncryptedApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApiKeyStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApiKeyLastValidatedAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    EncryptedOAuthAccessToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EncryptedOAuthRefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OAuthTokenExpiresAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    OAuthScope = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_TaskTimeDetails_UserId_IdTask",
                table: "TaskTimeDetails",
                columns: new[] { "UserId", "IdTask" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_OpenProjectInstanceId",
                table: "Tasks",
                column: "OpenProjectInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId_OpenProjectInstanceId",
                table: "Tasks",
                columns: new[] { "ProjectId", "OpenProjectInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_StatusTaskId_OpenProjectInstanceId",
                table: "Tasks",
                columns: new[] { "StatusTaskId", "OpenProjectInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_StatusTasks_OpenProjectInstanceId",
                table: "StatusTasks",
                column: "OpenProjectInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OpenProjectInstanceId",
                table: "Projects",
                column: "OpenProjectInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationsData_OpenProjectInstanceId",
                table: "MigrationsData",
                column: "OpenProjectInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationsData_UserId",
                table: "MigrationsData",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_OpenProjectInstanceId",
                table: "AspNetUsers",
                column: "OpenProjectInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_OpenProjectUserId",
                table: "AspNetUsers",
                column: "OpenProjectUserId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuthAuditLogs_IpAddress_EventType_CreatedAt",
                table: "AuthAuditLogs",
                columns: new[] { "IpAddress", "EventType", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuthAuditLogs_UserId_CreatedAt",
                table: "AuthAuditLogs",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_OpenProjectInstances_BaseUrl",
                table: "OpenProjectInstances",
                column: "BaseUrl",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MigrationsData_AspNetUsers_UserId",
                table: "MigrationsData",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MigrationsData_OpenProjectInstances_OpenProjectInstanceId",
                table: "MigrationsData",
                column: "OpenProjectInstanceId",
                principalTable: "OpenProjectInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_OpenProjectInstances_OpenProjectInstanceId",
                table: "Projects",
                column: "OpenProjectInstanceId",
                principalTable: "OpenProjectInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StatusTasks_OpenProjectInstances_OpenProjectInstanceId",
                table: "StatusTasks",
                column: "OpenProjectInstanceId",
                principalTable: "OpenProjectInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_AspNetUsers_UserId",
                table: "Tasks",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_OpenProjectInstances_OpenProjectInstanceId",
                table: "Tasks",
                column: "OpenProjectInstanceId",
                principalTable: "OpenProjectInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Projects_ProjectId_OpenProjectInstanceId",
                table: "Tasks",
                columns: new[] { "ProjectId", "OpenProjectInstanceId" },
                principalTable: "Projects",
                principalColumns: new[] { "Id", "OpenProjectInstanceId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_StatusTasks_StatusTaskId_OpenProjectInstanceId",
                table: "Tasks",
                columns: new[] { "StatusTaskId", "OpenProjectInstanceId" },
                principalTable: "StatusTasks",
                principalColumns: new[] { "Id", "OpenProjectInstanceId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskTimeDetails_AspNetUsers_UserId",
                table: "TaskTimeDetails",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskTimeDetails_Tasks_UserId_IdTask",
                table: "TaskTimeDetails",
                columns: new[] { "UserId", "IdTask" },
                principalTable: "Tasks",
                principalColumns: new[] { "UserId", "WorkPackageId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MigrationsData_AspNetUsers_UserId",
                table: "MigrationsData");

            migrationBuilder.DropForeignKey(
                name: "FK_MigrationsData_OpenProjectInstances_OpenProjectInstanceId",
                table: "MigrationsData");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_OpenProjectInstances_OpenProjectInstanceId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_StatusTasks_OpenProjectInstances_OpenProjectInstanceId",
                table: "StatusTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_AspNetUsers_UserId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_OpenProjectInstances_OpenProjectInstanceId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Projects_ProjectId_OpenProjectInstanceId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_StatusTasks_StatusTaskId_OpenProjectInstanceId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskTimeDetails_AspNetUsers_UserId",
                table: "TaskTimeDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskTimeDetails_Tasks_UserId_IdTask",
                table: "TaskTimeDetails");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AuthAuditLogs");

            migrationBuilder.DropTable(
                name: "UserCredentials");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "OpenProjectInstances");

            migrationBuilder.DropIndex(
                name: "IX_TaskTimeDetails_UserId_IdTask",
                table: "TaskTimeDetails");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Tasks",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_OpenProjectInstanceId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId_OpenProjectInstanceId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_StatusTaskId_OpenProjectInstanceId",
                table: "Tasks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StatusTasks",
                table: "StatusTasks");

            migrationBuilder.DropIndex(
                name: "IX_StatusTasks_OpenProjectInstanceId",
                table: "StatusTasks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Projects",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OpenProjectInstanceId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_MigrationsData_OpenProjectInstanceId",
                table: "MigrationsData");

            migrationBuilder.DropIndex(
                name: "IX_MigrationsData_UserId",
                table: "MigrationsData");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "TaskTimeDetails");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "OpenProjectInstanceId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "OpenProjectInstanceId",
                table: "StatusTasks");

            migrationBuilder.DropColumn(
                name: "OpenProjectInstanceId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OpenProjectInstanceId",
                table: "MigrationsData");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "MigrationsData");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "MigrationsData",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Tasks",
                table: "Tasks",
                column: "WorkPackageId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StatusTasks",
                table: "StatusTasks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Projects",
                table: "Projects",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTimeDetails_IdTask",
                table: "TaskTimeDetails",
                column: "IdTask");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId",
                table: "Tasks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_StatusTaskId",
                table: "Tasks",
                column: "StatusTaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Projects_ProjectId",
                table: "Tasks",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_StatusTasks_StatusTaskId",
                table: "Tasks",
                column: "StatusTaskId",
                principalTable: "StatusTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskTimeDetails_Tasks_IdTask",
                table: "TaskTimeDetails",
                column: "IdTask",
                principalTable: "Tasks",
                principalColumn: "WorkPackageId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
