using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenProjectInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    OAuthClientId = table.Column<string>(type: "text", nullable: true),
                    EncryptedOAuthClientSecret = table.Column<string>(type: "text", nullable: true),
                    Alias = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OAuthConnectedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenProjectInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
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
                    Id = table.Column<string>(type: "text", nullable: false),
                    OpenProjectUserId = table.Column<int>(type: "integer", nullable: false),
                    OpenProjectInstanceId = table.Column<int>(type: "integer", nullable: false),
                    OpenProjectInstanceBaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AuthMethod = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PauseDefaultBehavior = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SkipCancelConfirmation = table.Column<bool>(type: "boolean", nullable: false),
                    AddRandomSlackTime = table.Column<bool>(type: "boolean", nullable: false),
                    EncryptedGroqApiKey = table.Column<string>(type: "text", nullable: true),
                    DefaultStatusFilterIds = table.Column<string>(type: "text", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
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
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    OpenProjectInstanceId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Identifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => new { x.Id, x.OpenProjectInstanceId });
                    table.ForeignKey(
                        name: "FK_Projects_OpenProjectInstances_OpenProjectInstanceId",
                        column: x => x.OpenProjectInstanceId,
                        principalTable: "OpenProjectInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StatusTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    OpenProjectInstanceId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusTasks", x => new { x.Id, x.OpenProjectInstanceId });
                    table.ForeignKey(
                        name: "FK_StatusTasks_OpenProjectInstances_OpenProjectInstanceId",
                        column: x => x.OpenProjectInstanceId,
                        principalTable: "OpenProjectInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
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
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
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
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
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
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
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
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                name: "LocalCredentials",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EncryptedApiKey = table.Column<string>(type: "text", nullable: false),
                    ApiKeyStatus = table.Column<string>(type: "text", nullable: false),
                    ApiKeyLastValidatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                name: "MigrationsData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OpenProjectInstanceId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationsData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationsData_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MigrationsData_OpenProjectInstances_OpenProjectInstanceId",
                        column: x => x.OpenProjectInstanceId,
                        principalTable: "OpenProjectInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OAuthCredentials",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EncryptedOAuthAccessToken = table.Column<string>(type: "text", nullable: false),
                    EncryptedOAuthRefreshToken = table.Column<string>(type: "text", nullable: true),
                    OAuthTokenExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    OAuthScope = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
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

            migrationBuilder.CreateTable(
                name: "UserAvatars",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Jpeg = table.Column<byte[]>(type: "bytea", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAvatars", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserAvatars_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserNotificationSettings",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TypeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    IntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    ScheduleType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotificationSettings", x => new { x.UserId, x.TypeCode });
                    table.ForeignKey(
                        name: "FK_UserNotificationSettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    WorkPackageId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    OpenProjectInstanceId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    StatusTaskId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => new { x.UserId, x.WorkPackageId });
                    table.ForeignKey(
                        name: "FK_Tasks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasks_OpenProjectInstances_OpenProjectInstanceId",
                        column: x => x.OpenProjectInstanceId,
                        principalTable: "OpenProjectInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasks_Projects_ProjectId_OpenProjectInstanceId",
                        columns: x => new { x.ProjectId, x.OpenProjectInstanceId },
                        principalTable: "Projects",
                        principalColumns: new[] { "Id", "OpenProjectInstanceId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasks_StatusTasks_StatusTaskId_OpenProjectInstanceId",
                        columns: x => new { x.StatusTaskId, x.OpenProjectInstanceId },
                        principalTable: "StatusTasks",
                        principalColumns: new[] { "Id", "OpenProjectInstanceId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskTimeDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Uploaded = table.Column<bool>(type: "boolean", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    IdTask = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskTimeDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskTimeDetails_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskTimeDetails_Tasks_UserId_IdTask",
                        columns: x => new { x.UserId, x.IdTask },
                        principalTable: "Tasks",
                        principalColumns: new[] { "UserId", "WorkPackageId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

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
                unique: true);

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
                name: "IX_LocalCredentials_UserId",
                table: "LocalCredentials",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationsData_OpenProjectInstanceId",
                table: "MigrationsData",
                column: "OpenProjectInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationsData_UserId",
                table: "MigrationsData",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthCredentials_UserId",
                table: "OAuthCredentials",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenProjectInstances_BaseUrl",
                table: "OpenProjectInstances",
                column: "BaseUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OpenProjectInstanceId",
                table: "Projects",
                column: "OpenProjectInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_StatusTasks_OpenProjectInstanceId",
                table: "StatusTasks",
                column: "OpenProjectInstanceId");

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
                name: "IX_TaskTimeDetails_UserId_IdTask",
                table: "TaskTimeDetails",
                columns: new[] { "UserId", "IdTask" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "LocalCredentials");

            migrationBuilder.DropTable(
                name: "MigrationsData");

            migrationBuilder.DropTable(
                name: "OAuthCredentials");

            migrationBuilder.DropTable(
                name: "TaskTimeDetails");

            migrationBuilder.DropTable(
                name: "UserAvatars");

            migrationBuilder.DropTable(
                name: "UserNotificationSettings");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Tasks");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "StatusTasks");

            migrationBuilder.DropTable(
                name: "OpenProjectInstances");
        }
    }
}
