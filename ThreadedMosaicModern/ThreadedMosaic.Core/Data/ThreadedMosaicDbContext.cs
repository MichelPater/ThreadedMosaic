using Microsoft.EntityFrameworkCore;
using ThreadedMosaic.Core.Data.Models;

namespace ThreadedMosaic.Core.Data
{
    /// <summary>
    /// Entity Framework DbContext for ThreadedMosaic application
    /// </summary>
    public class ThreadedMosaicDbContext : DbContext
    {
        public ThreadedMosaicDbContext(DbContextOptions<ThreadedMosaicDbContext> options) : base(options)
        {
        }

        public DbSet<ImageMetadata> ImageMetadata { get; set; }
        public DbSet<MosaicProcessingResult> MosaicResults { get; set; }
        public DbSet<MosaicSeedImage> MosaicSeedImages { get; set; }
        public DbSet<ProcessingStep> ProcessingSteps { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure ImageMetadata
            modelBuilder.Entity<ImageMetadata>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(256);
                entity.Property(e => e.FileHash).HasMaxLength(64);
                entity.Property(e => e.Format).HasMaxLength(50);
                entity.Property(e => e.Notes).HasMaxLength(1000);

                // Add index for performance
                entity.HasIndex(e => e.FilePath).HasDatabaseName("IX_ImageMetadata_FilePath");
                entity.HasIndex(e => e.FileHash).HasDatabaseName("IX_ImageMetadata_FileHash");
                entity.HasIndex(e => e.LastAccessedAt).HasDatabaseName("IX_ImageMetadata_LastAccessedAt");
            });

            // Configure MosaicProcessingResult
            modelBuilder.Entity<MosaicProcessingResult>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MosaicType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.OutputPath).HasMaxLength(500);
                entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
                entity.Property(e => e.OutputFormat).HasMaxLength(20);
                entity.Property(e => e.MostUsedTilePath).HasMaxLength(500);
                entity.Property(e => e.Notes).HasMaxLength(1000);

                // Configure relationship with master image
                entity.HasOne(e => e.MasterImage)
                    .WithMany(e => e.MosaicsAsMaster)
                    .HasForeignKey(e => e.MasterImageId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Add indexes for performance
                entity.HasIndex(e => e.MosaicType).HasDatabaseName("IX_MosaicResult_MosaicType");
                entity.HasIndex(e => e.Status).HasDatabaseName("IX_MosaicResult_Status");
                entity.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_MosaicResult_CreatedAt");
                entity.HasIndex(e => e.MasterImageId).HasDatabaseName("IX_MosaicResult_MasterImageId");
            });

            // Configure MosaicSeedImage junction table
            modelBuilder.Entity<MosaicSeedImage>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Configure relationships
                entity.HasOne(e => e.MosaicResult)
                    .WithMany(e => e.SeedImages)
                    .HasForeignKey(e => e.MosaicResultId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.SeedImage)
                    .WithMany(e => e.MosaicsAsSeed)
                    .HasForeignKey(e => e.SeedImageId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Add unique constraint to prevent duplicate seed images per mosaic
                entity.HasIndex(e => new { e.MosaicResultId, e.SeedImageId })
                    .IsUnique()
                    .HasDatabaseName("IX_MosaicSeedImage_MosaicResult_SeedImage");
            });

            // Configure ProcessingStep
            modelBuilder.Entity<ProcessingStep>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.StepName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
                entity.Property(e => e.AdditionalData).HasMaxLength(2000);

                // Configure relationship
                entity.HasOne(e => e.MosaicResult)
                    .WithMany(e => e.ProcessingSteps)
                    .HasForeignKey(e => e.MosaicResultId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Add indexes for performance
                entity.HasIndex(e => e.MosaicResultId).HasDatabaseName("IX_ProcessingStep_MosaicResultId");
                entity.HasIndex(e => e.StartedAt).HasDatabaseName("IX_ProcessingStep_StartedAt");
                entity.HasIndex(e => e.Status).HasDatabaseName("IX_ProcessingStep_Status");
            });

            // Seed data
            SeedData(modelBuilder);
        }

        private static void SeedData(ModelBuilder modelBuilder)
        {
            // Add some default configuration or sample data if needed
            // This is optional and can be removed if not needed
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Update LastAccessedAt for ImageMetadata entities when they are accessed
            var imageMetadataEntries = ChangeTracker.Entries<ImageMetadata>()
                .Where(e => e.State == EntityState.Modified || e.State == EntityState.Added);

            foreach (var entry in imageMetadataEntries)
            {
                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.LastAccessedAt = DateTime.UtcNow;
                }
            }

            // Auto-calculate processing time for completed steps
            var processingStepEntries = ChangeTracker.Entries<ProcessingStep>()
                .Where(e => e.State == EntityState.Modified && e.Entity.CompletedAt.HasValue && !e.Entity.Duration.HasValue);

            foreach (var entry in processingStepEntries)
            {
                entry.Entity.Duration = entry.Entity.CompletedAt - entry.Entity.StartedAt;
            }

            // Auto-calculate processing time for completed mosaic results
            var mosaicResultEntries = ChangeTracker.Entries<MosaicProcessingResult>()
                .Where(e => e.State == EntityState.Modified && e.Entity.CompletedAt.HasValue && !e.Entity.ProcessingTime.HasValue && e.Entity.StartedAt.HasValue);

            foreach (var entry in mosaicResultEntries)
            {
                entry.Entity.ProcessingTime = entry.Entity.CompletedAt - entry.Entity.StartedAt;
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}