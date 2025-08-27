using System;
using System.Drawing;

namespace ThreadedMosaic.Mosaic
{
    public class ColorMosaic : Mosaic
    {
        public ColorMosaic(String masterFileLocation, String outputFileLocation)
            : base(masterFileLocation, outputFileLocation)
        {
        }

        public ColorMosaic(String masterFileLocation, String outputFileLocation, IProgressReporter progressReporter)
            : base(masterFileLocation, outputFileLocation, progressReporter)
        {
        }

        public ColorMosaic(String masterFileLocation, String outputFileLocation, IProgressReporter progressReporter, IFileOperations fileOperations)
            : base(masterFileLocation, outputFileLocation, progressReporter, fileOperations)
        {
        }

        /// <summary>
        /// Creates the ColorMosaic
        /// </summary>
        public void CreateColorMosaic()
        {
            SaveImage(CreateMosaic(GetColorTilesFromBitmap(MasterBitmap)));
        }

        /// <summary>
        ///  Build the image
        /// </summary>
        /// <param name="graphics"></param>
        /// <param name="xCoordinate"></param>
        /// <param name="yCoordinate"></param>
        /// <param name="tileColors"></param>
        protected override void BuildImage(Graphics graphics, int xCoordinate, int yCoordinate, Color[,] tileColors)
        {
            var colorBrush = new SolidBrush(tileColors[xCoordinate, yCoordinate]);

            var xLeftCoordinate = xCoordinate*XPixelCount;
            var yTopCoordinate = yCoordinate*YPixelCount;

            graphics.FillRectangle(colorBrush,
                new Rectangle(xLeftCoordinate, yTopCoordinate, XPixelCount, YPixelCount));
            IncrementProgressBar();
        }

        public MosaicTileMetadata BuildImageWithMetadata(Graphics graphics, int xCoordinate, int yCoordinate, Color[,] tileColors)
        {
            var targetColor = tileColors[xCoordinate, yCoordinate];
            var colorBrush = new SolidBrush(targetColor);

            var xLeftCoordinate = xCoordinate*XPixelCount;
            var yTopCoordinate = yCoordinate*YPixelCount;

            graphics.FillRectangle(colorBrush,
                new Rectangle(xLeftCoordinate, yTopCoordinate, XPixelCount, YPixelCount));
            colorBrush.Dispose();
            IncrementProgressBar();

            return new MosaicTileMetadata
            {
                XCoordinate = xCoordinate,
                YCoordinate = yCoordinate,
                TargetColor = targetColor,
                SelectedImagePath = null,
                TransparencyOverlayColor = Color.Empty,
                TilePixelSize = new Size(XPixelCount, YPixelCount)
            };
        }
    }
}