using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadedMosaic
{
    class PhotoMosaic : Mosaic
    {
        private ConcurrentBag<LoadedImage> _concurrentBag = new ConcurrentBag<LoadedImage>();
        private ConcurrentBag<LoadedImage> _alreadySelectedImages = new ConcurrentBag<LoadedImage>();

        public PhotoMosaic(List<String> fileLocations, String masterFileLocation)
            : base(fileLocations, masterFileLocation)
        {

        }

        public void CreatePhotoMosaic()
        {
            LoadImages();
            SaveImage(CreateMosaic(GetColorTilesFromBitmap(_masterBitmap)));
        }


        public Image CreateMosaic(Color[,] tileColors)
        {
            var mosaicImage = Image.FromFile(_masterFileLocation);

            using (var graphics = Graphics.FromImage(mosaicImage))
            {
                SetProgressBarMaximum(tileColors.GetLength(0) * tileColors.GetLength(1));
                for (var x = 0; x < tileColors.GetLength(0); x++)
                {
                    for (var y = 0; y < tileColors.GetLength(1); y++)
                    {
                        var xLeftCoordinate = x * XPixelCount;
                        var yTopCoordinate = y * YPixelCount;

                        Color transparentColor = Color.FromArgb(210, tileColors[x, y].R, tileColors[x, y].G, tileColors[x, y].B);

                        var colorBrush = new SolidBrush(transparentColor);

                        Image photoImage = ResizeImage(GetClosestMatchingImage(tileColors[x, y]), new Size(XPixelCount, YPixelCount));
                        graphics.DrawImage(photoImage, new Rectangle(xLeftCoordinate, yTopCoordinate, XPixelCount, YPixelCount));
                        graphics.FillRectangle(colorBrush,
                           new Rectangle(xLeftCoordinate, yTopCoordinate, XPixelCount, YPixelCount));
                        photoImage.Dispose();
                        SetProgressLabelText("Building Image, Coordinate x | y : " + x + " | " + y);
                        IncrementProgressBar();
                    }
                }
            }
            return mosaicImage;
        }

        private Image GetClosestMatchingImage(Color tileColor)
        {
            double closestDistance = double.MaxValue;


            String filePath = "";

            Parallel.ForEach(_concurrentBag, currentTile =>
            {
                double _tempDistance = CompareColors(tileColor, currentTile.AverageColor);

                bool containsItem = _alreadySelectedImages.Any(item => item.FilePath == currentTile.FilePath);

                if (_tempDistance < closestDistance && !containsItem)
                {
                    closestDistance = _tempDistance;
                    filePath = currentTile.FilePath;
                    _alreadySelectedImages.Add(currentTile);
                }
            });

            if (closestDistance == double.MaxValue)
            {

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
            }

            Image closestImage = Image.FromFile(filePath);
            GC.Collect();
            return closestImage;
        }

        private double CompareColors(Color firstColor, Color secondColor)
        {
            double redChannel = Math.Pow((double)firstColor.R - firstColor.R, 2);
            double blueChannel = Math.Pow((double)firstColor.B - firstColor.B, 2);
            double greenChannel = Math.Pow((double)firstColor.G - firstColor.G, 2);

            double distance = Math.Sqrt(redChannel + blueChannel + greenChannel);

            return distance;
        }


        private Color GetMostPrevelantColor(Bitmap sourceBitmap)
        {
            /* var mostUsedColors =
                 GetPixels(sourceBitmap)
                     .GroupBy(color => color)
                     .OrderByDescending(grp => grp.Count())
                     .Select(grp => grp.Key)
                     .Take(1);*/
            /*
            foreach (var color in mostUsedColors)
            {
                Console.WriteLine("Color {0}", color);
            }*/

            /*

            var max = results.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
            return GetPixels(sourceBitmap).ToList()[0];
            //return mostUsedColors.ToList()[0];*/

            return GetPixels(sourceBitmap).AsParallel().Aggregate((color, r) => color.Value > r.Value ? color : r).Key;
        }

        [HandleProcessCorruptedStateExceptions]
        private ConcurrentDictionary<Color, int> GetPixels(Bitmap bitmap)
        {
            try
            {
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

                    for (var y = 0; y < height; y++)
                    {
                        for (var x = 0; x < width; x++)
                        {
                            var idx = (y * stride) + x * bppModifier;
                            int red = pixel[idx + 2];
                            int green = pixel[idx + 1];
                            int blue = pixel[idx];
                            
                            Color calculatedColor = Color.FromArgb(red, green, blue);
                            int count = 0;
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
                bitmap.Dispose();
                return colors;
            }
            catch (Exception)
            {
                throw;
            }
        }
        private void LoadImages()
        {
            Console.WriteLine("Start of Loading Images: " + DateTime.Now);


            SetProgressLabelText("Loading Images");
            SetProgressBarMaximum(_fileLocations.Count);

            Parallel.ForEach(_fileLocations, currentFile =>
            {
                try
                {
                    //var mostPrevelantColor = GetMostPrevelantColor(GetBitmapFromFileName(currentFile));
                    var mostPrevelantColor = GetAverageColor(GetBitmapFromFileName(currentFile));
                    if (mostPrevelantColor != new Color())
                    {
                        _concurrentBag.Add(new LoadedImage { FilePath = currentFile, AverageColor = mostPrevelantColor });
                    }
                }
                finally
                {
                    Interlocked.Increment(ref _current);
                    SetProgressLabelText("Amount of Images loaded: " + _current);
                    IncrementProgressBar();
                }
            });

            Console.WriteLine("End of Loading images: " + DateTime.Now);
            Console.WriteLine("Amount of entries in concurrentbag: " + _concurrentBag.Count);
            Console.WriteLine("Amount of entires in List: " + _fileLocations.Count);
            Console.WriteLine("Skipped Entries : " + (_fileLocations.Count - _concurrentBag.Count));
            SetProgressLabelText("Skipped Entries: " + (_fileLocations.Count - _concurrentBag.Count));
        }
    }
}
