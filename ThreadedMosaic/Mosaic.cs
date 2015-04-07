using System;
using System.Drawing;

namespace ThreadedMosaic
{
    public class Mosaic
    {

        public Mosaic()
        {
            
        }


        public static Color GetTileAverage(Bitmap bSource, int x, int y, int width, int height)
        {
            long aR = 0;
            long aG = 0;
            long aB = 0;
            for (int w = x; w < x + width; w++)
            {
                for (int h = y; h < y + height; h++)
                {
                    Color cP = bSource.GetPixel(w, h);
                    aR += cP.R;
                    aG += cP.G;
                    aB += cP.B;
                }
            }
            aR = aR / (width * height);
            aG = aG / (width * height);
            aB = aB / (width * height);
            return Color.FromArgb(255, Convert.ToInt32(aR), Convert.ToInt32(aG), Convert.ToInt32(aB));
        }
    }
}
