using System;
using System.Drawing;

namespace ThreadedMosaic.Mosaic
{
    /// <summary>
    ///     Represents an Image loaded into memory
    /// </summary>
    public class LoadedImage
    {
        public Color AverageColor { get; set; }
        public String FilePath { get; set; }
    }
}