using System.Drawing;
using FluentAssertions;
using ThreadedMosaic;
using Xunit;

namespace ThreadedMosaic.Tests
{
    public class LoadedImageTests
    {
        [Fact]
        public void LoadedImage_Should_Initialize_With_Default_Values()
        {
            // Arrange & Act
            var loadedImage = new LoadedImage();

            // Assert
            loadedImage.FilePath.Should().BeNull();
            loadedImage.AverageColor.Should().Be(default(Color));
        }

        [Fact]
        public void LoadedImage_Should_Set_FilePath_Property()
        {
            // Arrange
            var loadedImage = new LoadedImage();
            var filePath = @"C:\test\image.jpg";

            // Act
            loadedImage.FilePath = filePath;

            // Assert
            loadedImage.FilePath.Should().Be(filePath);
        }

        [Fact]
        public void LoadedImage_Should_Set_AverageColor_Property()
        {
            // Arrange
            var loadedImage = new LoadedImage();
            var expectedColor = Color.FromArgb(255, 128, 64);

            // Act
            loadedImage.AverageColor = expectedColor;

            // Assert
            loadedImage.AverageColor.Should().Be(expectedColor);
        }

        [Fact]
        public void LoadedImage_Should_Allow_Property_Updates()
        {
            // Arrange
            var loadedImage = new LoadedImage
            {
                FilePath = @"C:\initial\path.jpg",
                AverageColor = Color.Red
            };

            var newFilePath = @"C:\updated\path.jpg";
            var newColor = Color.Blue;

            // Act
            loadedImage.FilePath = newFilePath;
            loadedImage.AverageColor = newColor;

            // Assert
            loadedImage.FilePath.Should().Be(newFilePath);
            loadedImage.AverageColor.Should().Be(newColor);
        }
    }
}