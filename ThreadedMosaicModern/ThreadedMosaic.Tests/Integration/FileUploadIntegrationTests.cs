using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using ThreadedMosaic.Core.Interfaces;
using ThreadedMosaic.Tests.Infrastructure;

namespace ThreadedMosaic.Tests.Integration
{
    /// <summary>
    /// Integration tests for file upload and download scenarios
    /// </summary>
    public class FileUploadIntegrationTests : IClassFixture<TestWebApplicationFactory<Program>>, IDisposable
    {
        private readonly TestWebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private readonly TestFileOperations _testFileOperations;
        private readonly string _testDirectory;

        public FileUploadIntegrationTests(TestWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
            _testFileOperations = _factory.GetRequiredService<IFileOperations>() as TestFileOperations
                ?? throw new InvalidOperationException("Expected TestFileOperations in test environment");

            _testDirectory = Path.Combine(Path.GetTempPath(), "FileUploadIntegrationTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
        }

        [Fact]
        public async Task FileOperations_CreateAndReadFile_WorksEndToEnd()
        {
            // Test the complete file operations workflow
            
            // Arrange
            var testFilePath = "/uploads/test-image.jpg";
            var testContent = "fake image binary data";
            var testBytes = Encoding.UTF8.GetBytes(testContent);

            // Act - Create file through the test file operations
            await _testFileOperations.WriteAllBytesAsync(testFilePath, testBytes);

            // Verify file exists
            var exists = await _testFileOperations.FileExistsAsync(testFilePath);
            var retrievedBytes = await _testFileOperations.ReadAllBytesAsync(testFilePath);
            var retrievedContent = Encoding.UTF8.GetString(retrievedBytes);

            // Assert
            exists.Should().BeTrue();
            retrievedContent.Should().Be(testContent);
            retrievedBytes.Should().BeEquivalentTo(testBytes);
        }

        [Fact]
        public async Task FileOperations_DirectoryManagement_WorksCorrectly()
        {
            // Test directory creation and management
            
            // Arrange
            var testDir = "/uploads/testdir";
            var testFile = $"{testDir}/image.jpg";

            // Act - Create directory and file
            await _testFileOperations.CreateDirectoryAsync(testDir);
            await _testFileOperations.WriteAllTextAsync(testFile, "test content");

            // Verify operations
            var dirExists = await _testFileOperations.DirectoryExistsAsync(testDir);
            var fileExists = await _testFileOperations.FileExistsAsync(testFile);
            var files = await _testFileOperations.GetFilesAsync(testDir);

            // Assert
            dirExists.Should().BeTrue();
            fileExists.Should().BeTrue();
            files.Should().Contain(testFile);
        }

        [Fact]
        public async Task FileOperations_FileCopyAndMove_WorkCorrectly()
        {
            // Test file copy and move operations
            
            // Arrange
            var sourceFile = "/source/original.jpg";
            var copyDestination = "/copy/copied.jpg";
            var moveDestination = "/move/moved.jpg";
            var testContent = "original file content";

            // Setup original file
            _testFileOperations.AddTestFile(sourceFile, testContent);

            // Act - Copy file
            await _testFileOperations.CopyFileAsync(sourceFile, copyDestination);
            
            // Verify copy
            var originalExists = await _testFileOperations.FileExistsAsync(sourceFile);
            var copyExists = await _testFileOperations.FileExistsAsync(copyDestination);
            var copyContent = await _testFileOperations.ReadAllTextAsync(copyDestination);

            // Act - Move file
            await _testFileOperations.MoveFileAsync(copyDestination, moveDestination);
            
            // Verify move
            var copyExistsAfterMove = await _testFileOperations.FileExistsAsync(copyDestination);
            var moveExists = await _testFileOperations.FileExistsAsync(moveDestination);
            var moveContent = await _testFileOperations.ReadAllTextAsync(moveDestination);

            // Assert
            originalExists.Should().BeTrue(); // Original should still exist after copy
            copyExists.Should().BeTrue(); // Copy should exist before move
            copyContent.Should().Be(testContent);
            
            copyExistsAfterMove.Should().BeFalse(); // Copy should not exist after move
            moveExists.Should().BeTrue(); // Move destination should exist
            moveContent.Should().Be(testContent);
        }

        [Fact]
        public async Task FileOperations_FileMetadata_IsTrackedCorrectly()
        {
            // Test that file metadata (size, timestamps) is handled correctly
            
            // Arrange
            var testFile = "/metadata/test.txt";
            var testContent = "This is test content for metadata testing";
            var expectedSize = Encoding.UTF8.GetBytes(testContent).Length;

            // Act
            await _testFileOperations.WriteAllTextAsync(testFile, testContent);
            
            var fileSize = await _testFileOperations.GetFileSizeAsync(testFile);
            var lastWriteTime = await _testFileOperations.GetLastWriteTimeAsync(testFile);

            // Assert
            fileSize.Should().Be(expectedSize);
            lastWriteTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task FileOperations_ErrorHandling_WorksCorrectly()
        {
            // Test error handling for various file operation scenarios
            
            // Act & Assert - File not found scenarios
            await FluentActions.Invoking(async () => 
                await _testFileOperations.ReadAllBytesAsync("/nonexistent/file.txt"))
                .Should().ThrowAsync<FileNotFoundException>();

            await FluentActions.Invoking(async () => 
                await _testFileOperations.GetFileSizeAsync("/nonexistent/file.txt"))
                .Should().ThrowAsync<FileNotFoundException>();

            await FluentActions.Invoking(async () => 
                await _testFileOperations.GetLastWriteTimeAsync("/nonexistent/file.txt"))
                .Should().ThrowAsync<FileNotFoundException>();

            // Act & Assert - Copy from non-existent file
            await FluentActions.Invoking(async () => 
                await _testFileOperations.CopyFileAsync("/nonexistent/source.txt", "/destination.txt"))
                .Should().ThrowAsync<FileNotFoundException>();

            // Act & Assert - Move from non-existent file
            await FluentActions.Invoking(async () => 
                await _testFileOperations.MoveFileAsync("/nonexistent/source.txt", "/destination.txt"))
                .Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task FileOperations_FileOverwrite_BehavesCorrectly()
        {
            // Test file overwrite scenarios
            
            // Arrange
            var testFile = "/overwrite/test.txt";
            var originalContent = "original content";
            var newContent = "new content";

            // Setup original file
            await _testFileOperations.WriteAllTextAsync(testFile, originalContent);

            // Act - Overwrite file
            await _testFileOperations.WriteAllTextAsync(testFile, newContent);

            var finalContent = await _testFileOperations.ReadAllTextAsync(testFile);

            // Assert
            finalContent.Should().Be(newContent);
        }

        [Fact]
        public async Task FileOperations_DirectoryDeletion_WorksRecursively()
        {
            // Test recursive directory deletion
            
            // Arrange
            var parentDir = "/delete/parent";
            var childDir = $"{parentDir}/child";
            var file1 = $"{parentDir}/file1.txt";
            var file2 = $"{childDir}/file2.txt";

            // Create directory structure
            await _testFileOperations.CreateDirectoryAsync(parentDir);
            await _testFileOperations.CreateDirectoryAsync(childDir);
            await _testFileOperations.WriteAllTextAsync(file1, "content1");
            await _testFileOperations.WriteAllTextAsync(file2, "content2");

            // Act - Delete parent directory recursively
            await _testFileOperations.DeleteDirectoryAsync(parentDir, recursive: true);

            // Assert - All files and directories should be deleted
            var parentExists = await _testFileOperations.DirectoryExistsAsync(parentDir);
            var file1Exists = await _testFileOperations.FileExistsAsync(file1);
            var file2Exists = await _testFileOperations.FileExistsAsync(file2);

            parentExists.Should().BeFalse();
            file1Exists.Should().BeFalse();
            file2Exists.Should().BeFalse();
        }

        [Fact]
        public void TestFileOperations_TestHelpers_WorkCorrectly()
        {
            // Test the test-specific helper methods
            
            // Arrange
            var testFile = "/helpers/test.txt";
            var testContent = "helper test content";

            // Act
            _testFileOperations.AddTestFile(testFile, testContent);
            var allFiles = _testFileOperations.GetAllFiles();

            // Assert
            allFiles.Should().ContainKey(testFile);
            var retrievedContent = Encoding.UTF8.GetString(allFiles[testFile]);
            retrievedContent.Should().Be(testContent);

            // Act - Clear all files
            _testFileOperations.ClearAllFiles();
            var filesAfterClear = _testFileOperations.GetAllFiles();

            // Assert
            filesAfterClear.Should().BeEmpty();
        }

        public void Dispose()
        {
            _client?.Dispose();
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }
    }
}