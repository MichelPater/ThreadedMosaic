using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadedMosaic
{
    class HueMosaic : Mosaic
    {
        public HueMosaic(List<String> fileLocations, String masterFileLocation)
            : base(fileLocations, masterFileLocation)
        {
        }

        public void CreateColorMosaic()
        {
            LoadImages();
            SaveImage(CreateMosaic(GetColorTilesFromBitmap(_masterBitmap)));
        }

        private Image CreateMosaic(Color[,] tileColors)
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

                        Image photoImage = ResizeImage(GetRandomImage(), new Size(XPixelCount, YPixelCount));
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


        /// <summary>
        /// Get a random image from the loaded images
        /// </summary>
        /// <returns></returns>
        private Image GetRandomImage()
        {
            Random random = new Random(DateTime.Now.Millisecond);

            int number = random.Next(0, _fileLocations.Count);
            Image randomImage = Image.FromFile(_fileLocations[number]);
            GC.Collect();
            return randomImage;
        }

        private Image ResizeImage(Image imgToResize, Size size)
        {
            return (Image)(new Bitmap(imgToResize, size));
        }

        private void LoadImages()
        {
            Console.WriteLine("Start of Loading Images: " + DateTime.Now);
            SetProgressLabelText("Calculating average of Tiles");
            SetProgressBarMaximum(_fileLocations.Count);

            var concurrentBag = new ConcurrentBag<LoadedImage>();
            Parallel.ForEach(_fileLocations, currentFile =>
            {
                try
                {
                    concurrentBag.Add(new LoadedImage { FilePath = currentFile });
                }
                finally
                {
                    Interlocked.Increment(ref _current);
                    SetProgressLabelText("Amount of Images loaded: " + _current);
                    IncrementProgressBar();
                }
            });

            Console.WriteLine("End of Loading images: " + DateTime.Now);
            Console.WriteLine("Amount of entries in concurrentbag: " + concurrentBag.Count);
            Console.WriteLine("Amount of entires in List: " + _fileLocations.Count);
            Console.WriteLine("Skipped Entries : " + (_fileLocations.Count - concurrentBag.Count));
            SetProgressLabelText("Skipped Entries: " + (_fileLocations.Count - concurrentBag.Count));
        }
    }
}
