using System;
using System.Collections.Generic;

namespace ThreadedMosaic.Core.Models
{
    /// <summary>
    /// Enhanced metadata for a single tile in the mosaic with better performance tracking
    /// </summary>
    public class MosaicTileMetadata
    {
        public int XCoordinate { get; set; }
        public int YCoordinate { get; set; }
        public ColorInfo TargetColor { get; set; }
        public string SelectedImagePath { get; set; } = string.Empty;
        public ColorInfo TransparencyOverlayColor { get; set; }
        public TileSize TilePixelSize { get; set; }
        
        /// <summary>
        /// Enhanced matching information
        /// </summary>
        public double ColorDistance { get; set; }
        public double MatchingScore { get; set; }
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Alternative matches for better variety
        /// </summary>
        public List<AlternativeMatch> AlternativeMatches { get; set; } = new();
        
        /// <summary>
        /// Processing metadata
        /// </summary>
        public int ProcessingAttempts { get; set; } = 1;
        public string? ProcessingError { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        
        /// <summary>
        /// Gets the tile center coordinates
        /// </summary>
        public (int X, int Y) GetCenterCoordinates()
        {
            return (XCoordinate + TilePixelSize.Width / 2, YCoordinate + TilePixelSize.Height / 2);
        }
        
        /// <summary>
        /// Gets the tile bounds
        /// </summary>
        public TileBounds GetBounds()
        {
            return new TileBounds
            {
                Left = XCoordinate,
                Top = YCoordinate,
                Right = XCoordinate + TilePixelSize.Width,
                Bottom = YCoordinate + TilePixelSize.Height,
                Width = TilePixelSize.Width,
                Height = TilePixelSize.Height
            };
        }
    }

    /// <summary>
    /// Cross-platform tile size structure
    /// </summary>
    public struct TileSize : IEquatable<TileSize>
    {
        public int Width { get; set; }
        public int Height { get; set; }

        public TileSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Area => Width * Height;
        
        public double AspectRatio => Height == 0 ? 0 : (double)Width / Height;

        public bool IsSquare => Width == Height;

        public static bool operator ==(TileSize left, TileSize right) => left.Equals(right);
        public static bool operator !=(TileSize left, TileSize right) => !left.Equals(right);

        public bool Equals(TileSize other) => Width == other.Width && Height == other.Height;

        public override bool Equals(object? obj) => obj is TileSize other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Width, Height);

        public override string ToString() => $"{Width}x{Height}";

        // Common tile sizes
        public static readonly TileSize Small = new(8, 8);
        public static readonly TileSize Medium = new(16, 16);
        public static readonly TileSize Large = new(32, 32);
        public static readonly TileSize ExtraLarge = new(64, 64);
    }

    /// <summary>
    /// Tile boundary information
    /// </summary>
    public struct TileBounds
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
        
        public bool Intersects(TileBounds other) => 
            Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
    }

    /// <summary>
    /// Alternative match information for better image variety
    /// </summary>
    public class AlternativeMatch
    {
        public string ImagePath { get; set; } = string.Empty;
        public double MatchingScore { get; set; }
        public double ColorDistance { get; set; }
        public ColorInfo AverageColor { get; set; }
        public string MatchingReason { get; set; } = string.Empty;
    }
}