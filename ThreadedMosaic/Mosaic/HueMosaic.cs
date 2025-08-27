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

        public HueMosaic(List<String> fileLocations, String masterFileLocation, String outputFileLocation, IProgressReporter progressReporter, IFileOperations fileOperations)
            : base(fileLocations, masterFileLocation, outputFileLocation, progressReporter, fileOperations)
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

        public MosaicTileMetadata BuildImageWithMetadata(Graphics graphics, int xCoordinate, int yCoordinate, Color[,] tileColors, int seed)
        {
            var xLeftCoordinate = xCoordinate*XPixelCount;
            var yTopCoordinate = yCoordinate*YPixelCount;
            var targetColor = tileColors[xCoordinate, yCoordinate];

            var transparentColor = Color.FromArgb(210, targetColor.R, targetColor.G, targetColor.B);
            var colorBrush = new SolidBrush(transparentColor);

            var selectedImagePath = string.Empty;
            if (_concurrentBag.Count > 0)
            {
                var random = new Random(seed);
                var number = random.Next(0, _concurrentBag.Count);
                selectedImagePath = _concurrentBag.ToList()[number].FilePath;
                var photoImage = ResizeImage(GetRandomImage(seed), new Size(XPixelCount, YPixelCount));
                graphics.DrawImage(photoImage, new Rectangle(xLeftCoordinate, yTopCoordinate, XPixelCount, YPixelCount));
                photoImage.Dispose();
            }

            graphics.FillRectangle(colorBrush,
                new Rectangle(xLeftCoordinate, yTopCoordinate, XPixelCount, YPixelCount));
            colorBrush.Dispose();

            return new MosaicTileMetadata
            {
                XCoordinate = xCoordinate,
                YCoordinate = yCoordinate,
                TargetColor = targetColor,
                SelectedImagePath = selectedImagePath,
                TransparencyOverlayColor = transparentColor,
                TilePixelSize = new Size(XPixelCount, YPixelCount)
            };
        }

        /// <summary>
        ///     Get a random image from the loaded images
        /// </summary>
        /// <returns></returns>
        private Image GetRandomImage()
        {
            return GetRandomImage(DateTime.Now.Millisecond);
        }

        /// <summary>
        ///     Get a random image from the loaded images with specific seed
        /// </summary>
        /// <param name="seed">Random seed for reproducible testing</param>
        /// <returns></returns>
        public Image GetRandomImage(int seed)
        {
            var random = new Random(seed);

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