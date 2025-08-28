using FluentAssertions;
using ThreadedMosaic.Core.Models;

namespace ThreadedMosaic.Tests
{
    /// <summary>
    /// Basic tests to verify the testing framework is working
    /// </summary>
    public class BasicTests
    {
        [Fact]
        public void ColorInfo_Creation_WorksCorrectly()
        {
            // Arrange & Act
            var color = new ColorInfo { R = 255, G = 128, B = 64 };

            // Assert
            color.R.Should().Be(255);
            color.G.Should().Be(128);
            color.B.Should().Be(64);
        }

        [Theory]
        [InlineData(ImageFormat.Jpeg)]
        [InlineData(ImageFormat.Png)]
        [InlineData(ImageFormat.Bmp)]
        [InlineData(ImageFormat.Webp)]
        public void ImageFormat_AllFormatsSupported(ImageFormat format)
        {
            // Act & Assert
            format.Should().BeDefined();
        }

        [Fact]
        public void MosaicConfiguration_DefaultValues_AreSet()
        {
            // Act
            var config = new MosaicConfiguration();

            // Assert
            config.Should().NotBeNull();
            config.Performance.Should().NotBeNull();
            config.ImageProcessing.Should().NotBeNull();
            config.FileManagement.Should().NotBeNull();
            config.Logging.Should().NotBeNull();
        }

        [Fact]
        public void LoadedImage_PropertiesSet_WorkCorrectly()
        {
            // Arrange
            var loadedImage = new LoadedImage
            {
                FilePath = "test.jpg",
                Width = 100,
                Height = 200,
                AverageColor = new ColorInfo { R = 128, G = 64, B = 32 }
            };

            // Assert
            loadedImage.FilePath.Should().Be("test.jpg");
            loadedImage.Width.Should().Be(100);
            loadedImage.Height.Should().Be(200);
            loadedImage.AverageColor.Should().NotBeNull();
            loadedImage.AverageColor.R.Should().Be(128);
        }

        [Theory]
        [InlineData(8, 8)]
        [InlineData(16, 16)]
        [InlineData(32, 32)]
        [InlineData(64, 64)]
        public void MosaicTileMetadata_DifferentSizes_WorkCorrectly(int width, int height)
        {
            // Arrange & Act
            var metadata = new MosaicTileMetadata
            {
                XCoordinate = 10,
                YCoordinate = 20,
                TilePixelSize = new TileSize(width, height)
            };

            // Assert
            metadata.TilePixelSize.Width.Should().Be(width);
            metadata.TilePixelSize.Height.Should().Be(height);
            metadata.XCoordinate.Should().Be(10);
            metadata.YCoordinate.Should().Be(20);
        }

        [Fact]
        public void ColorDistance_Calculation_IsCorrect()
        {
            // Arrange
            var color1 = new ColorInfo { R = 255, G = 255, B = 255 };
            var color2 = new ColorInfo { R = 0, G = 0, B = 0 };

            // Act
            var distance = CalculateColorDistance(color1, color2);

            // Assert
            distance.Should().BeApproximately(441.67, 0.01); // sqrt(255^2 + 255^2 + 255^2)
        }

        [Fact]
        public void ColorDistance_SameColor_IsZero()
        {
            // Arrange
            var color = new ColorInfo { R = 128, G = 128, B = 128 };

            // Act
            var distance = CalculateColorDistance(color, color);

            // Assert
            distance.Should().Be(0);
        }

        private static double CalculateColorDistance(ColorInfo color1, ColorInfo color2)
        {
            var dr = color1.R - color2.R;
            var dg = color1.G - color2.G;
            var db = color1.B - color2.B;
            return Math.Sqrt(dr * dr + dg * dg + db * db);
        }
    }
}