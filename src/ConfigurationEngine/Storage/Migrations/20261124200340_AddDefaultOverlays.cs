using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ConfigurationEngine.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultOverlays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AnimationSource",
                columns: new[] { "Id", "Columns", "FrameCount", "FrameHeight", "FrameWidth", "Name", "Path", "Rows", "SourceType" },
                values: new object[,]
                {
                    { 5, null, null, null, null, "Kis tűz", "overlays/kis_tuz", null, "ImageFolder" },
                    { 6, null, null, null, null, "Nagy tűz", "overlays/nagy_tuz", null, "ImageFolder" },
                    { 7, null, null, null, null, "Hálósapka", "overlays/halosapka", null, "ImageFolder" },
                    { 8, null, null, null, null, "Álmosság", "overlays/zzz", null, "ImageFolder" },
                    { 9, null, null, null, null, "Lemerülőben", "overlays/lemerulo", null, "ImageFolder" },
                    { 10, null, null, null, null, "Unatkozás", "overlays/unatkozas", null, "ImageFolder" },
                    { 11, null, null, null, null, "Ablakkáosz", "overlays/ablakkaosz", null, "ImageFolder" },
                    { 12, null, null, null, null, "Floppytorony", "overlays/floppytorony", null, "ImageFolder" },
                    { 13, null, null, null, null, "Banánhéjak", "overlays/bananhejak", null, "ImageFolder" },
                    { 14, null, null, null, null, "Cetlik", "overlays/cetlik", null, "ImageFolder" }
                });

            migrationBuilder.InsertData(
                table: "BehaviorRule",
                columns: new[] { "Id", "AnimationSourceId", "ExclusiveGroup", "FramesPerSecond", "IsEnabled", "Metric", "Name", "Operator", "Priority", "Threshold", "ThresholdUpper", "ZIndex" },
                values: new object[,]
                {
                    { 1, 5, "fejtető", 10.0, true, "Cpu", "Kis tűz", "Gt", 40, 60, null, 20 },
                    { 2, 6, "oldalak", 10.0, true, "Cpu", "Nagy tűz", "Gt", 20, 80, null, 10 },
                    { 3, 7, "fejtető", 10.0, true, "Time", "Hálósapka", "Between", 10, 1320, 360, 20 },
                    { 4, 8, "jobb felső", 10.0, true, "Time", "Álmosság", "Between", 10, 1320, 360, 30 },
                    { 5, 9, "bal felső", 10.0, true, "Battery", "Lemerülőben", "Lt", 10, 10, null, 30 },
                    { 6, 10, "jobb felső", 10.0, true, "Inactivity", "Unatkozás", "Gt", 20, 5, null, 30 },
                    { 7, 11, "fejtető", 10.0, true, "WindowCount", "Ablakkáosz", "Gt", 30, 10, null, 20 },
                    { 8, 12, "fejtető", 10.0, true, "Disk", "Floppytorony", "Gt", 20, 99, null, 20 },
                    { 9, 13, "oldalak", 10.0, true, "Memory", "Banánhéjak", "Gt", 10, 90, null, 10 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AnimationSource",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "BehaviorRule",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "BehaviorRule",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "BehaviorRule",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "BehaviorRule",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "BehaviorRule",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "BehaviorRule",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "BehaviorRule",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "BehaviorRule",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "BehaviorRule",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "AnimationSource",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "AnimationSource",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "AnimationSource",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "AnimationSource",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "AnimationSource",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "AnimationSource",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "AnimationSource",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "AnimationSource",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "AnimationSource",
                keyColumn: "Id",
                keyValue: 13);
        }
    }
}
