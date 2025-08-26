using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadedMosaic.Mosaic
{
    public class HueMosaic : Mosaic
    {
        private readonly ConcurrentBag<LoadedImage> _concurrentBag = new ConcurrentBag<LoadedImage>();

        public HueMosaic(List<String> fileLocations, String masterFileLocation, String outputFileLocation)
            : base(fileLocations, masterFileLocation, outputFileLocation)
        {
        }

        public HueMosaic(List<String> fileLocations, String masterFileLocation, String outputFileLocation, IProgressReporter progressReporter)
            : base(fileLocations, masterFileLocation, outputFileLocation, progressReporter)
        {
        }

        public void CreateColorMosaic()
        {
            LoadImages();
            SaveImage(CreateMosaic(GetColorTilesFromBitmap(MasterBitmap)));
        }

        protected override void BuildImage(Graphics graphics, int xCoordinate, int yCoordinate, Color[,] tileColors)
        {
            var xLeftCoordinate = xCoordinate*XPixelCount;
            var yTopCoordinate = yCoordinate*YPixelCount;

            var transparentColor = Color.FromArgb(210, tileColors[xCoordinate, yCoordinate].R,
                tileColors[xCoordinate, yCoordinate].G, tileColors[xCoordinate, yCoordinate].B);

            var colorBrush = new SolidBrush(transparentColor);

            var photoImage = ResizeImage(GetRandomImage(), new Size(XPixelCount, YPixelCount));
            graphics.DrawImage(photoImage, new Rectangle(xLeftCoordinate, yTopCoordinate, XPixelCount, YPixelCount));
            graphics.FillRectangle(colorBrush,
                new Rectangle(xLeftCoordinate, yTopCoordinate, XPixelCount, YPixelCount));
            photoImage.Dispose();
        }

        /// <summary>
        ///     Get a random image from the loaded images
        /// </summary>
        /// <returns></returns>
        private Image GetRandomImage()
        {
            var random = new Random(DateTime.Now.Millisecond);

            var number = random.Next(0, _concurrentBag.Count);
            var randomImage = Image.FromFile(_concurrentBag.ToList()[number].FilePath);
            GC.Collect();
            return randomImage;
        }

        /// <summary>
        /// Load the images
        /// </summary>
        private void LoadImages()
        {
            ProgressReporter.UpdateStatus("Start of Loading Images:");
            ProgressReporter.SetMaximum(FileLocations.Count);

            Parallel.ForEach(FileLocations, currentFile =>
            {
                try
                {
                    _concurrentBag.Add(new LoadedImage {FilePath = currentFile});
                }
                finally
                {
                    Interlocked.Increment(ref Current);
                    ProgressReporter.UpdateStatus("Amount of Images loaded: " + Current);
                    ProgressReporter.IncrementProgress();
                }
            });
            ProgressReporter.UpdateStatus("Skipped Entries: " + (FileLocations.Count - _concurrentBag.Count));
        }
    }
}