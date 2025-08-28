using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreadedMosaic.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImageMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    FileHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    Format = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    AverageRed = table.Column<byte>(type: "INTEGER", nullable: false),
                    AverageGreen = table.Column<byte>(type: "INTEGER", nullable: false),
                    AverageBlue = table.Column<byte>(type: "INTEGER", nullable: false),
                    AverageHue = table.Column<double>(type: "REAL", nullable: false),
                    AverageSaturation = table.Column<double>(type: "REAL", nullable: false),
                    AverageBrightness = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastAccessedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsValid = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageMetadata", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MosaicResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MosaicType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MasterImageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OutputPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProcessingTime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    PixelSize = table.Column<int>(type: "INTEGER", nullable: false),
                    Quality = table.Column<int>(type: "INTEGER", nullable: true),
                    OutputFormat = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    TilesProcessed = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalTiles = table.Column<int>(type: "INTEGER", nullable: true),
                    SeedImagesUsed = table.Column<int>(type: "INTEGER", nullable: true),
                    UniqueTilesUsed = table.Column<int>(type: "INTEGER", nullable: true),
                    AverageColorDistance = table.Column<double>(type: "REAL", nullable: true),
                    AverageProcessingTimePerTile = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    MostUsedTilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    MostUsedTileCount = table.Column<int>(type: "INTEGER", nullable: true),
                    ColorTolerance = table.Column<int>(type: "INTEGER", nullable: true),
                    HueTolerance = table.Column<double>(type: "REAL", nullable: true),
                    SaturationWeight = table.Column<int>(type: "INTEGER", nullable: true),
                    BrightnessWeight = table.Column<int>(type: "INTEGER", nullable: true),
                    SimilarityThreshold = table.Column<int>(type: "INTEGER", nullable: true),
                    AvoidImageRepetition = table.Column<bool>(type: "INTEGER", nullable: true),
                    MaxImageReuse = table.Column<int>(type: "INTEGER", nullable: true),
                    UseEdgeDetection = table.Column<bool>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MosaicResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MosaicResults_ImageMetadata_MasterImageId",
                        column: x => x.MasterImageId,
                        principalTable: "ImageMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MosaicSeedImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MosaicResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SeedImageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UsageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageColorDistance = table.Column<double>(type: "REAL", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MosaicSeedImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MosaicSeedImages_ImageMetadata_SeedImageId",
                        column: x => x.SeedImageId,
                        principalTable: "ImageMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MosaicSeedImages_MosaicResults_MosaicResultId",
                        column: x => x.MosaicResultId,
                        principalTable: "MosaicResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MosaicResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ItemsProcessed = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalItems = table.Column<int>(type: "INTEGER", nullable: true),
                    ProgressPercentage = table.Column<double>(type: "REAL", nullable: true),
                    MemoryUsedBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    AdditionalData = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessingSteps_MosaicResults_MosaicResultId",
                        column: x => x.MosaicResultId,
                        principalTable: "MosaicResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageMetadata_FileHash",
                table: "ImageMetadata",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_ImageMetadata_FilePath",
                table: "ImageMetadata",
                column: "FilePath");

            migrationBuilder.CreateIndex(
                name: "IX_ImageMetadata_LastAccessedAt",
                table: "ImageMetadata",
                column: "LastAccessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MosaicResult_CreatedAt",
                table: "MosaicResults",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MosaicResult_MasterImageId",
                table: "MosaicResults",
                column: "MasterImageId");

            migrationBuilder.CreateIndex(
                name: "IX_MosaicResult_MosaicType",
                table: "MosaicResults",
                column: "MosaicType");

            migrationBuilder.CreateIndex(
                name: "IX_MosaicResult_Status",
                table: "MosaicResults",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MosaicSeedImage_MosaicResult_SeedImage",
                table: "MosaicSeedImages",
                columns: new[] { "MosaicResultId", "SeedImageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MosaicSeedImages_SeedImageId",
                table: "MosaicSeedImages",
                column: "SeedImageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingStep_MosaicResultId",
                table: "ProcessingSteps",
                column: "MosaicResultId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingStep_StartedAt",
                table: "ProcessingSteps",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingStep_Status",
                table: "ProcessingSteps",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MosaicSeedImages");

            migrationBuilder.DropTable(
                name: "ProcessingSteps");

            migrationBuilder.DropTable(
                name: "MosaicResults");

            migrationBuilder.DropTable(
                name: "ImageMetadata");
        }
    }
}
