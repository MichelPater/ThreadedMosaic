using System;
using System.Drawing;

namespace ThreadedMosaic.Mosaic
{
    public class MosaicTileMetadata
    {
        public int XCoordinate { get; set; }
        public int YCoordinate { get; set; }
        public Color TargetColor { get; set; }
        public String SelectedImagePath { get; set; }
        public Color TransparencyOverlayColor { get; set; }
        public Size TilePixelSize { get; set; }
    }
}