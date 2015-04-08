using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadedMosaic
{
    public class Mosaic
    {
        private List<String> _filesNames;
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
            /*  Parallel.ForEach(_filesNames, currentFile =>
              {
                  concurrentBag.Add(new Tile { FilePath = currentFile, AverageColor = GetBitmapColorAverage(GetBitmapFromFileName(currentFile)) });
              });*/

            foreach (var currentFile in _filesNames)
            {
                Console.WriteLine("A file in _fileNames");
                concurrentBag.Add(new Tile { FilePath = currentFile, AverageColor = GetBitmapColorAverage(GetBitmapFromFileName(currentFile)) });
            }

            Console.WriteLine("Amount of entries in concurrentbag: " + concurrentBag.Count);
            Console.WriteLine("Amount of entires in List: " + _filesNames.Count);
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
                    //Thread.Sleep(500);
                    GC.WaitForPendingFinalizers();
                }
            }
            bitmap = new Bitmap(filePath);
            return bitmap;
        }


        public Color GetBitmapColorAverage(Bitmap sourceBitmap)
        {
            //Bitmap blurredSourceBitmap = Blur.GausianBlur(sourceBitmap, 5);
            Bitmap blurredSourceBitmap = sourceBitmap;
            long redChannel = 0;
            long greenChannel = 0;
            long blueChannel = 0;

            long totalPixels = 0;

            for (int xCoordinate = 0; xCoordinate < blurredSourceBitmap.Width; xCoordinate++)
            {
                long tempRedChannel = 0;
                long tempGreenChannel = 0;
                long tempBlueChannel = 0;
                int tempTotalPixels = 0;

                for (int yCoordinate = 0; yCoordinate < blurredSourceBitmap.Height; yCoordinate++)
                {
                    Color color = blurredSourceBitmap.GetPixel(xCoordinate, yCoordinate);
                    tempRedChannel = color.R;
                    tempGreenChannel = color.G;
                    tempBlueChannel = color.B;
                    tempTotalPixels++;

                }
                redChannel += tempRedChannel;
                greenChannel += tempGreenChannel;
                blueChannel += tempBlueChannel;
                totalPixels += tempTotalPixels;
            }

            redChannel /= totalPixels;
            greenChannel /= totalPixels;
            blueChannel /= totalPixels;
            sourceBitmap.Dispose();
            blurredSourceBitmap.Dispose();
            return Color.FromArgb(255, Convert.ToInt32(redChannel), Convert.ToInt32(greenChannel), Convert.ToInt32(blueChannel));
        }
    }
}
