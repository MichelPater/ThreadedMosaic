using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Color = System.Drawing.Color;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace ThreadedMosaic
{
    public class Mosaic
    {
        private List<String> _fileLocations;
        private long current;
        private readonly int X_PIXEL_COUNT = 40;
        private readonly int Y_PIXEL_COUNT = 40;
        private readonly String _outputLocation = @"C:\Users\Michel\Desktop\Output folder\" + DateTime.Now.ToString().Replace(':', '-')  +".jpeg";
        private readonly Bitmap _masterBitmap;
        private readonly String _masterFileLocation;
        public Mosaic()
        {
            //Load Images

            //Get average of individual image and store the values

            //Load master image

            //divide image up into a grid

            //Find closest match for every gridelement

            //Assemble image from found gridelements
        }

        public Mosaic(List<String> fileLocations, String masterFileLocation)
        {
            _fileLocations = fileLocations;
            _masterFileLocation = masterFileLocation;
            _masterBitmap = GetBitmapFromFileName(masterFileLocation);
        }

        private Color[,] GetColorTilesFromBitmap(Bitmap sourceBitmap)
        {
            int amountOfTilesInWidth = sourceBitmap.Width / X_PIXEL_COUNT;
            int amountOfTilesInHeight = sourceBitmap.Height / Y_PIXEL_COUNT;
            Color[,] tileColors = new Color[amountOfTilesInWidth, amountOfTilesInHeight];

            for (int x = 0; x < amountOfTilesInWidth; x++)
            {
                for (int y = 0; y < amountOfTilesInHeight; y++)
                {
                    int xTopCoordinate = x * X_PIXEL_COUNT;
                    int yTopCoordinate = y * Y_PIXEL_COUNT;

                    int xBottomCoordinate = xTopCoordinate + X_PIXEL_COUNT;
                    int yBottomCoordinate = yTopCoordinate + Y_PIXEL_COUNT;

                    tileColors[x, y] = GetAverageColor(sourceBitmap, new Rectangle(xTopCoordinate, yTopCoordinate, xBottomCoordinate, yBottomCoordinate));
                }
            }
            return tileColors;
        }

        public void CreateOutput()
        {
            SaveImage(CreateMosaic(GetColorTilesFromBitmap(_masterBitmap)));
            Console.WriteLine("Done");
        }

        private Image CreateMosaic(Color[,] tileColors)
        {
            Image mosaicImage = Image.FromFile(_masterFileLocation);

            using (Graphics graphics = Graphics.FromImage(mosaicImage))
            {
                for (int x = 0; x < tileColors.GetLength(0); x++)
                {
                    for (int y = 0; y < tileColors.GetLength(1); y++)
                    {
                        SolidBrush shadowBrush = new SolidBrush(tileColors[x, y]);

                        int xTopCoordinate = x * X_PIXEL_COUNT;
                        int yTopCoordinate = y * Y_PIXEL_COUNT;

                        int xBottomCoordinate = xTopCoordinate + X_PIXEL_COUNT;
                        int yBottomCoordinate = yTopCoordinate + Y_PIXEL_COUNT;
                        graphics.FillRectangle(shadowBrush, new Rectangle(xTopCoordinate, yTopCoordinate, xBottomCoordinate, yBottomCoordinate));
                    }
                }
            }
            return mosaicImage;
        }

        public void SaveImage(Image imageToSave)
        {
            imageToSave.Save(_outputLocation, ImageFormat.Jpeg);
        }
        public void SaveImage(Bitmap imageToSave)
        {
            imageToSave.Save(_outputLocation, ImageFormat.Jpeg);
        }

        public void LoadImages()
        {
            Console.WriteLine("Start of Loading Images: " + DateTime.Now);
            ConcurrentBag<LoadedImage> concurrentBag = new ConcurrentBag<LoadedImage>();
            Parallel.ForEach(_fileLocations, currentFile =>
             {
                 try
                 {
                     Color averageColor = GetAverageColor(GetBitmapFromFileName(currentFile));

                     if (averageColor != new Color())
                     {
                         concurrentBag.Add(new LoadedImage { FilePath = currentFile, AverageColor = averageColor });
                     }
                 }
                 finally
                 {
                     Interlocked.Increment(ref this.current);
                 }
             });

            /*
            foreach (var currentFile in _filesNames)
            {
                Console.WriteLine("A file in _fileNames: " + currentFile);
                concurrentBag.Add(new Tile { FilePath = currentFile, AverageColor = GetQuickAverageColor(GetBitmapFromFileName(currentFile)) });
            }
            */
            Console.WriteLine("End of Loading images: " + DateTime.Now);
            Console.WriteLine("Amount of entries in concurrentbag: " + concurrentBag.Count);
            Console.WriteLine("Amount of entires in List: " + _fileLocations.Count);
            Console.WriteLine("Skipped Entries : " + (_fileLocations.Count - concurrentBag.Count));
        }

        private Bitmap GetBitmapFromFileName(String filePath)
        {
            Boolean bLoaded = false;
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

        [HandleProcessCorruptedStateExceptions]
        private Color GetAverageColor(Bitmap bitmap)
        {
            return GetAverageColor(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        }


        [HandleProcessCorruptedStateExceptions]
        private Color GetAverageColor(Bitmap bitmap, Rectangle subDivision)
        {
            try
            {
                int width = bitmap.Width;
                int height = bitmap.Height;
                int red = 0;
                int green = 0;
                int blue = 0;
                long[] totals = { 0, 0, 0 };
                int bppModifier = bitmap.PixelFormat == PixelFormat.Format24bppRgb ? 3 : 4; // cutting corners, will fail on anything else but 32 and 24 bit images

                BitmapData srcData = bitmap.LockBits(subDivision, ImageLockMode.ReadOnly, bitmap.PixelFormat);

                int stride = srcData.Stride;
                IntPtr Scan0 = srcData.Scan0;

                unsafe
                {
                    byte* pixel = (byte*)(void*)Scan0;

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int idx = (y * stride) + x * bppModifier;
                            red = pixel[idx + 2];
                            green = pixel[idx + 1];
                            blue = pixel[idx];

                            totals[2] += red;
                            totals[1] += green;
                            totals[0] += blue;
                        }
                    }
                }

                int count = width * height;
                int avgR = (int)(totals[2] / count);
                int avgG = (int)(totals[1] / count);
                int avgB = (int)(totals[0] / count);
                bitmap.Dispose();
                return Color.FromArgb(avgR, avgG, avgB);
            }
            catch (Exception exception)
            {
                return new Color();
            }
        }
    }
}
