using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadedMosaic
{
    public class Mosaic
    {
        private List<String> _filesNames;
        private long current;
        public Mosaic()
        {
            //Load Images

            //Get average of individual image and store the values

            //Load master image

            //divide image up into a grid

            //Find closest match for every gridelement

            //Assemble image from found gridelements
        }

        public Mosaic(List<String> filesNames)
        {
            _filesNames = filesNames;
        }

        public void CreateTiles()
        {
            Console.WriteLine("Start of CreateTiles");
            ConcurrentBag<Tile> concurrentBag = new ConcurrentBag<Tile>();
            Console.WriteLine(DateTime.Now);
            Parallel.ForEach(_filesNames, currentFile =>
             {
                 try
                 {
                     Color averageColor = GetAverageColor(GetBitmapFromFileName(currentFile));

                     if (averageColor != new Color())
                     {
                         concurrentBag.Add(new Tile { FilePath = currentFile, AverageColor = averageColor });
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
            Console.WriteLine(DateTime.Now);
            Console.WriteLine("Amount of entries in concurrentbag: " + concurrentBag.Count);
            Console.WriteLine("Amount of entires in List: " + _filesNames.Count);
            Console.WriteLine("Skipped Entries : " + (_filesNames.Count - concurrentBag.Count));
        }

        public Bitmap GetBitmapFromFileName(String filePath)
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
            try
            {
                int width = bitmap.Width;
                int height = bitmap.Height;
                int red = 0;
                int green = 0;
                int blue = 0;
                long[] totals = { 0, 0, 0 };
                int bppModifier = bitmap.PixelFormat == PixelFormat.Format24bppRgb ? 3 : 4; // cutting corners, will fail on anything else but 32 and 24 bit images

                BitmapData srcData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);

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
