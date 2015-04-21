using System;
using System.Drawing;

namespace ThreadedMosaic.Mosaic
{
    internal class ColorMosaic : Mosaic
    {
        public ColorMosaic(String masterFileLocation) : base(masterFileLocation)
        {
        }

        /// <summary>
        /// Creates the ColorMosaic
        /// </summary>
        public void CreateColorMosaic()
        {
            SaveImage(CreateMosaic(GetColorTilesFromBitmap(_masterBitmap)));
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
    }
}