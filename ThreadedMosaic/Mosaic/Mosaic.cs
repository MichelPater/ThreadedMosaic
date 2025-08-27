using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using Image = System.Drawing.Image;

namespace ThreadedMosaic.Mosaic
{
    public abstract class Mosaic
    {
        protected readonly List<String> FileLocations;
        protected readonly Bitmap MasterBitmap;
        protected readonly String MasterFileLocation;
        protected readonly String OutputFilelocation;

        protected long Current;
        protected IProgressReporter ProgressReporter;
        protected IFileOperations FileOperations;
        public int XPixelCount = 40;
        public int YPixelCount = 40;

        public Mosaic(String masterFileLocation, String outputFilelocation) 
            : this(masterFileLocation, outputFilelocation, NullProgressReporter.Instance)
        {
        }

        public Mosaic(String masterFileLocation, String outputFilelocation, IProgressReporter progressReporter)
            : this(masterFileLocation, outputFilelocation, progressReporter, DefaultFileOperations.Instance)
        {
        }

        public Mosaic(String masterFileLocation, String outputFilelocation, IProgressReporter progressReporter, IFileOperations fileOperations)
        {
            MasterFileLocation = masterFileLocation;
            MasterBitmap = GetBitmapFromFileName(masterFileLocation);
            OutputFilelocation = outputFilelocation;
            ProgressReporter = progressReporter ?? NullProgressReporter.Instance;
            FileOperations = fileOperations ?? DefaultFileOperations.Instance;
        }

        public Mosaic(List<String> fileLocations, String masterFileLocation, String outputFilelocation)
            : this(fileLocations, masterFileLocation, outputFilelocation, NullProgressReporter.Instance)
        {
        }

        public Mosaic(List<String> fileLocations, String masterFileLocation, String outputFilelocation, IProgressReporter progressReporter)
            : this(fileLocations, masterFileLocation, outputFilelocation, progressReporter, DefaultFileOperations.Instance)
        {
        }

        public Mosaic(List<String> fileLocations, String masterFileLocation, String outputFilelocation, IProgressReporter progressReporter, IFileOperations fileOperations)
        {
            FileLocations = fileLocations;
            MasterFileLocation = masterFileLocation;
            MasterBitmap = GetBitmapFromFileName(masterFileLocation);
            OutputFilelocation = outputFilelocation;
            ProgressReporter = progressReporter ?? NullProgressReporter.Instance;
            FileOperations = fileOperations ?? DefaultFileOperations.Instance;
        }

        /// <summary>
        ///     Sets the progress reporter for UI updates
        /// </summary>
        /// <param name="progressReporter">The progress reporter implementation</param>
        public void SetProgressReporter(IProgressReporter progressReporter)
        {
            ProgressReporter = progressReporter ?? NullProgressReporter.Instance;
        }

        /// <summary>
        ///     Sets the Progressbar and the ProgressBarLabel (legacy method for backward compatibility)
        /// </summary>
        /// <param name="progressBar">The physical progressbar</param>
        /// <param name="progressBarLabel">The label with text updates</param>
        public void SetProgressBar(ProgressBar progressBar, Label progressBarLabel)
        {
            SetProgressReporter(new WpfProgressReporter(progressBar, progressBarLabel));
        }

        /// <summary>
        ///     Saves the image to the disk
        /// </summary>
        /// <param name="imageToSave"></param>
        protected void SaveImage(Image imageToSave)
        {
            ProgressReporter.UpdateStatus("Saving image");
            imageToSave.Save(OutputFilelocation, ImageFormat.Jpeg);
            FileOperations.OpenImageFile(OutputFilelocation);
            ProgressReporter.UpdateStatus("Done");
        }

        /// <summary>
        ///     Loads a bitmap from a filelocation
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        protected Bitmap GetBitmapFromFileName(String filePath)
        {
            var bLoaded = false;
            Bitmap bitmap = null;
            while (!bLoaded)
            {
                try
                {
                    bitmap = new Bitmap(filePath);
                    bLoaded = true;
                }
                catch (OutOfMemoryException)
                {
                    GC.WaitForPendingFinalizers();
                }
            }
            return bitmap;
        }

        /// <summary>
        ///     Gets a ColorAverage of a whole bitmap
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        [HandleProcessCorruptedStateExceptions]
        protected Color GetAverageColor(Bitmap bitmap)
        {
            return GetAverageColor(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        }

        /// <summary>
        ///     Gets the Average color of a Bitmap within a divined subdivision
        /// </summary>
        /// <param name="bitmap">The source image</param>
        /// <param name="subDivision">The location that is being calculated</param>
        /// <returns></returns>
        [HandleProcessCorruptedStateExceptions]
        protected Color GetAverageColor(Bitmap bitmap, Rectangle subDivision)
        {
            try
            {
                var width = subDivision.Width;
                var height = subDivision.Height;
                long[] totals = {0, 0, 0};
                var bppModifier = bitmap.PixelFormat == PixelFormat.Format24bppRgb ? 3 : 4;
                // cutting corners, will fail on anything else but 32 and 24 bit images, but is acceptable about 40 in 12000 fail

                var srcData = bitmap.LockBits(subDivision, ImageLockMode.ReadOnly, bitmap.PixelFormat);

                var stride = srcData.Stride;
                var scan0 = srcData.Scan0;

                //Run a parallel anaylisis on each pixel
                unsafe
                {
                    var pixel = (byte*) (void*) scan0;

                    for (var y = 0; y < height; y++)
                    {
                        for (var x = 0; x < width; x++)
                        {
                            //Get a pixel and add it to the array
                            var index = (y*stride) + x*bppModifier;
                            int red = pixel[index + 2];
                            int green = pixel[index + 1];
                            int blue = pixel[index];

                            totals[2] += red;
                            totals[1] += green;
                            totals[0] += blue;
                        }
                    }
                }
                //Calculate the average of each channel
                var count = width*height;
                var avgR = (int) (totals[2]/count);
                var avgG = (int) (totals[1]/count);
                var avgB = (int) (totals[0]/count);
                bitmap.UnlockBits(srcData);
                return Color.FromArgb(avgR, avgG, avgB);
            }
                //Catch any error and return an empty Color
            catch (Exception)
            {
                return new Color();
            }
        }

        /// <summary>
        ///     Initalizes the ColorArray with the correct width and height depending on the imagewidth and heigh
        /// </summary>
        /// <param name="imageWidth">The image width</param>
        /// <param name="imageHeight">The image Height</param>
        /// <returns></returns>
        public Color[,] InitializeColorTiles(int imageWidth, int imageHeight)
        {
            ProgressReporter.UpdateStatus("Initializing Color Tiles");
            Color[,] tileColors;

            var amountOfTilesInWidth = imageWidth/XPixelCount;
            var amountOfTilesInHeight = imageHeight/YPixelCount;

            var amountOfWidthLeftOver = imageWidth%XPixelCount;
            var amountOfHeightLeftOver = imageHeight%YPixelCount;

            //Check if the height and widht fit perfectly in the grid
            //if not expand the grid in the direction that has left over pixels
            if (amountOfHeightLeftOver > 0 && amountOfWidthLeftOver > 0)
            {
                tileColors = new Color[amountOfTilesInWidth + 1, amountOfTilesInHeight + 1];
            }
            else if (amountOfWidthLeftOver > 0)
            {
                tileColors = new Color[amountOfTilesInWidth + 1, amountOfTilesInHeight];
            }
            else if (amountOfHeightLeftOver > 0)
            {
                tileColors = new Color[amountOfTilesInWidth, amountOfTilesInHeight + 1];
            }
            else
            {
                tileColors = new Color[amountOfTilesInWidth, amountOfTilesInHeight];
            }

            return tileColors;
        }

        /// <summary>
        ///     Create the correct rectangle for the current tile
        /// </summary>
        /// <param name="currentXCoordinate">The current X coordinate</param>
        /// <param name="currentYCoordinate">The current Y coordinate</param>
        /// <param name="tileColors">The array with the colortiles</param>
        /// <param name="sourceBitmapWidth">The source image width</param>
        /// <param name="sourceBitmapHeight">The source image height</param>
        /// <returns></returns>
        public Rectangle CreateSizeAppropriateRectangle(int currentXCoordinate, int currentYCoordinate,
            Color[,] tileColors, int sourceBitmapWidth, int sourceBitmapHeight)
        {
            var xTopCoordinate = currentXCoordinate*XPixelCount;
            var yTopCoordinate = currentYCoordinate*YPixelCount;

            var amountOfWidthLeftOver = sourceBitmapWidth%XPixelCount;
            var amountOfHeightLeftOver = sourceBitmapHeight%YPixelCount;

            //Check if there are any left over pixels if not make a normal rectangle
            if (currentXCoordinate < tileColors.GetLength(0) - 1 && currentYCoordinate < tileColors.GetLength(1) - 1)
            {
                return new Rectangle(xTopCoordinate, yTopCoordinate, XPixelCount, YPixelCount);
            }
            //if there are left over pixels that do not fit into a regular grid check on which side the excess exists and compensate
            int tempXPixelCount, tempYPixelCount;

            if (amountOfWidthLeftOver > 0)
            {
                tempXPixelCount = amountOfHeightLeftOver;
            }
            else
            {
                tempXPixelCount = XPixelCount;
            }
            if (amountOfHeightLeftOver > 0)
            {
                tempYPixelCount = amountOfHeightLeftOver;
            }
            else
            {
                tempYPixelCount = YPixelCount;
            }
            return new Rectangle(xTopCoordinate, yTopCoordinate, tempXPixelCount, tempYPixelCount);
        }


        protected void SetProgressBarMaximum(int count)
        {
            ProgressReporter.SetMaximum(count);
        }

        protected void IncrementProgressBar()
        {
            ProgressReporter.IncrementProgress();
        }

        protected void SetProgressLabelText(String text)
        {
            ProgressReporter.UpdateStatus(text);
        }

        public Color[,] GetColorTilesFromBitmap(Bitmap sourceBitmap)
        {
            var tileColors = InitializeColorTiles(sourceBitmap.Width, sourceBitmap.Height);
            ProgressReporter.UpdateStatus("Calculating average of Tiles");
            ProgressReporter.SetMaximum(tileColors.GetLength(0)*tileColors.GetLength(1));

            for (var x = 0; x < tileColors.GetLength(0); x++)
            {
                for (var y = 0; y < tileColors.GetLength(1); y++)
                {
                    var tempBitmap = new Bitmap(sourceBitmap);

                    tileColors[x, y] = GetAverageColor(tempBitmap,
                        CreateSizeAppropriateRectangle(x, y, tileColors, sourceBitmap.Width, sourceBitmap.Height));

                    tempBitmap.Dispose();
                    IncrementProgressBar();
                }
            }
            /*
             Bitmap[] bitmaps = new Bitmap[tileColors.GetLength(0)];
             int width= tileColors.GetLength(0);
             int height = tileColors.GetLength(1);

             Parallel.For(0, width, x =>
             {
                 bitmaps[x] = (Bitmap)sourceBitmap.Clone();
                 for (var y = 0; y < height; y++)
                 {
                     lock (tileColors)
                     {
                         tileColors[x, y] = GetAverageColor(bitmaps[x],
                 CreateSizeAppropriateRectangle(x, y, (Color[,])tileColors.Clone(), sourceBitmap.Width, sourceBitmap.Height));
 
                     }
           
                     bitmaps[x].Dispose();
                     IncrementProgressBar();
                 }
             });*/
            return tileColors;
        }

        /// <summary>
        ///     Resizes an image according to a specified size
        /// </summary>
        /// <param name="imgToResize"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        protected Image ResizeImage(Image imgToResize, Size size)
        {
            return new Bitmap(imgToResize, size);
        }

        /// <summary>
        ///     Sets the pixel size of the tiles used in the mosaic
        /// </summary>
        /// <param name="widthInPixels">The width in pixels</param>
        /// <param name="heightInPixels">The height in pixels</param>
        public void SetPixelSize(int widthInPixels, int heightInPixels)
        {
            XPixelCount = widthInPixels;
            YPixelCount = heightInPixels;
        }

        protected abstract void BuildImage(Graphics graphics, int xCoordinate, int yCoordinate, Color[,] tileColors);

        /// <summary>
        ///     Create the Mosaic
        /// </summary>
        /// <param name="tileColors"></param>
        /// <returns></returns>
        protected Image CreateMosaic(Color[,] tileColors)
        {
            var mosaicImage = Image.FromFile(MasterFileLocation);

            using (var graphics = Graphics.FromImage(mosaicImage))
            {
                ProgressReporter.SetMaximum(tileColors.GetLength(0)*tileColors.GetLength(1));
                for (var x = 0; x < tileColors.GetLength(0); x++)
                {
                    for (var y = 0; y < tileColors.GetLength(1); y++)
                    {
                        BuildImage(graphics, x, y, tileColors);

                        //Update GUI information
                        ProgressReporter.UpdateStatus("Building Image, Coordinate x | y : " + x + " | " + y);
                        ProgressReporter.IncrementProgress();
                    }
                }
            }
            return mosaicImage;
        }
    }
}