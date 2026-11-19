using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ConfigurationEngine.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnimationSource",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    FrameWidth = table.Column<int>(type: "INTEGER", nullable: true),
                    FrameHeight = table.Column<int>(type: "INTEGER", nullable: true),
                    Columns = table.Column<int>(type: "INTEGER", nullable: true),
                    Rows = table.Column<int>(type: "INTEGER", nullable: true),
                    FrameCount = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimationSource", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CharacterSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterWidth = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterHeight = table.Column<int>(type: "INTEGER", nullable: false),
                    WalkSpeedPxPerSec = table.Column<int>(type: "INTEGER", nullable: false),
                    GravityPxPerSec2 = table.Column<int>(type: "INTEGER", nullable: false),
                    FollowCursor = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetricPolling",
                columns: table => new
                {
                    Metric = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IntervalMs = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricPolling", x => x.Metric);
                });

            migrationBuilder.CreateTable(
                name: "BehaviorRule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Metric = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Operator = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Threshold = table.Column<int>(type: "INTEGER", nullable: false),
                    ThresholdUpper = table.Column<int>(type: "INTEGER", nullable: true),
                    AnimationSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    FramesPerSecond = table.Column<double>(type: "REAL", nullable: false),
                    ZIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    ExclusiveGroup = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BehaviorRule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BehaviorRule_AnimationSource_AnimationSourceId",
                        column: x => x.AnimationSourceId,
                        principalTable: "AnimationSource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovementAnimation",
                columns: table => new
                {
                    MovementState = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AnimationSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    FramesPerSecond = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementAnimation", x => x.MovementState);
                    table.ForeignKey(
                        name: "FK_MovementAnimation_AnimationSource_AnimationSourceId",
                        column: x => x.AnimationSourceId,
                        principalTable: "AnimationSource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CharacterSettings",
                columns: new[] { "Id", "CharacterHeight", "CharacterWidth", "FollowCursor", "GravityPxPerSec2", "WalkSpeedPxPerSec" },
                values: new object[] { 1, 100, 100, true, 2000, 120 });

            migrationBuilder.InsertData(
                table: "MetricPolling",
                columns: new[] { "Metric", "IntervalMs", "IsEnabled" },
                values: new object[,]
                {
                    { "Battery", 30000, true },
                    { "Cpu", 1000, true },
                    { "Disk", 30000, true },
                    { "Inactivity", 2000, true },
                    { "Memory", 1000, true },
                    { "Time", 30000, true },
                    { "WindowCount", 2000, true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorRule_AnimationSourceId",
                table: "BehaviorRule",
                column: "AnimationSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_MovementAnimation_AnimationSourceId",
                table: "MovementAnimation",
                column: "AnimationSourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BehaviorRule");

            migrationBuilder.DropTable(
                name: "CharacterSettings");

            migrationBuilder.DropTable(
                name: "MetricPolling");

            migrationBuilder.DropTable(
                name: "MovementAnimation");

            migrationBuilder.DropTable(
                name: "AnimationSource");
        }
    }
}
