using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadedMosaic.Mosaic
{
    public class PhotoMosaic : Mosaic
    {
        private ConcurrentBag<LoadedImage> _concurrentBag = new ConcurrentBag<LoadedImage>();
        private ConcurrentBag<LoadedImage> _alreadySelectedImages = new ConcurrentBag<LoadedImage>();

        public PhotoMosaic(List<String> fileLocations, String masterFileLocation, String outputFileLocation)
            : base(fileLocations, masterFileLocation, outputFileLocation)
        {

        }

        public void CreatePhotoMosaic()
        {
            LoadImages();
            SaveImage(CreateMosaic(GetColorTilesFromBitmap(MasterBitmap)));
        }


        protected override void BuildImage(Graphics graphics, int xCoordinate, int yCoordinate, Color[,] tileColors)
        {
            var xLeftCoordinate = xCoordinate * XPixelCount;
            var yTopCoordinate = yCoordinate * YPixelCount;

            //Get and set the best fitting image
            Image photoImage = ResizeImage(GetClosestMatchingImage(tileColors[xCoordinate, yCoordinate]), new Size(XPixelCount, YPixelCount));
            graphics.DrawImage(photoImage, new Rectangle(xLeftCoordinate, yTopCoordinate, XPixelCount, YPixelCount));

            //Adjust the color to beter fit the original design
            Color transparentColor = Color.FromArgb(210, tileColors[xCoordinate, yCoordinate].R, tileColors[xCoordinate, yCoordinate].G, tileColors[xCoordinate, yCoordinate].B);
            var colorBrush = new SolidBrush(transparentColor);
            graphics.FillRectangle(colorBrush,
              new Rectangle(xLeftCoordinate, yTopCoordinate, XPixelCount, YPixelCount));

            //Dispose unnessacery objects
            photoImage.Dispose();
            colorBrush.Dispose();

        }

        private Image GetClosestMatchingImage(Color tileColor)
        {
            double closestDistance = double.MaxValue;

            String filePath = "";

            Parallel.ForEach(_concurrentBag, currentTile =>
            {
                double tempDistance = CompareColors(tileColor, currentTile.AverageColor);

                if (tempDistance < closestDistance)
                {
                    closestDistance = tempDistance;
                    filePath = currentTile.FilePath;
                    _alreadySelectedImages.Add(currentTile);
                }
            });

            Image closestImage = Image.FromFile(filePath);
            GC.Collect();
            return closestImage;
        }

        private double CompareColors(Color firstColor, Color secondColor)
        {
            double redChannel = Math.Pow((double)firstColor.R - secondColor.R, 2);
            double blueChannel = Math.Pow((double)firstColor.B - secondColor.B, 2);
            double greenChannel = Math.Pow((double)firstColor.G - secondColor.G, 2);

            double distance = Math.Sqrt(redChannel + blueChannel + greenChannel);

            return distance;
        }

        /// <summary>
        /// Gets the most prevelantColor in a given Bitmap
        /// </summary>
        /// <param name="sourceBitmap"></param>
        /// <returns></returns>
        private Color GetMostPrevelantColor(Bitmap sourceBitmap)
        {
            return GetPixels(sourceBitmap).AsParallel().Aggregate((color, r) => color.Value > r.Value ? color : r).Key;
        }

        /// <summary>
        /// Get the pixels from a given bitmap
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        [HandleProcessCorruptedStateExceptions]
        private ConcurrentDictionary<Color, int> GetPixels(Bitmap bitmap)
        {
            //Setup a dictionary for colors and their corresponding count
            var colors = new ConcurrentDictionary<Color, int>();

            var width = bitmap.Width;
            var height = bitmap.Height;
          
            var bppModifier = bitmap.PixelFormat == PixelFormat.Format24bppRgb ? 3 : 4;
            var srcData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly,
                bitmap.PixelFormat);

            var stride = srcData.Stride;
            var scan0 = srcData.Scan0;

            unsafe
            {
                var pixel = (byte*)(void*)scan0;
                //Iterate over every pixel
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        //Get the colorchannels of each pixel
                        var idx = (y * stride) + x * bppModifier;
                        int red = pixel[idx + 2];
                        int green = pixel[idx + 1];
                        int blue = pixel[idx];

                        //Add the color to the dictionary
                        Color calculatedColor = Color.FromArgb(red, green, blue);
                        int count;
                        if (colors.TryGetValue(calculatedColor, out count))
                        {
                            colors.TryAdd(calculatedColor, count + 1);
                        }
                        else
                        {
                            colors.TryAdd(calculatedColor, 1);
                        }
                    }
                }
            }
            //Dispose the bitmap
            bitmap.Dispose();
            GC.Collect();
            return colors;
        }
        /// <summary>
        /// Load the images
        /// </summary>
        private void LoadImages()
        {
            SetProgressLabelText("Loading Images");
            SetProgressBarMaximum(FileLocations.Count);

            Parallel.ForEach(FileLocations, currentFile =>
            {
                try
                {
                    var mostPrevelantColor = GetMostPrevelantColor(GetBitmapFromFileName(currentFile));
                    if (mostPrevelantColor != new Color())
                    {
                        _concurrentBag.Add(new LoadedImage { FilePath = currentFile, AverageColor = mostPrevelantColor });
                    }
                }
                finally
                {
                    Interlocked.Increment(ref Current);
                    SetProgressLabelText("Amount of Images loaded: " + Current);
                    IncrementProgressBar();
                }
            });
            //Update gui
            SetProgressLabelText("Skipped Entries: " + (FileLocations.Count - _concurrentBag.Count));
        }
    }
}
