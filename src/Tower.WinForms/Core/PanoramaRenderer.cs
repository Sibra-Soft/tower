using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Tower.Infrastructure;

namespace Tower.Core;

internal sealed class PanoramaRenderer(OriginalPanorama panorama) : IDisposable
{
    private Bitmap? frame;
    private (Size Size, double Bearing, double FieldOfView, bool Night, double Pitch) last;

    public void Draw(Graphics graphics, Size size, double bearing, double fieldOfView, bool night, double pitch = 0)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var key = (size, bearing, fieldOfView, night, pitch);
        var displaySize = size;

        if (frame is null || last != key)
        {
            // The source strip is low-resolution. Bound the background raster work
            // on large monitors while keeping model geometry at native resolution.
            var scale = Math.Min(1, Math.Min(1920d / size.Width, 1080d / size.Height));
            
            size = new(Math.Max(1, (int)(size.Width * scale)), Math.Max(1, (int)(size.Height * scale)));
            
            var focal = size.Width / (2 * Math.Tan(fieldOfView * Math.PI / 360));
            var horizon = size.Height * 0.43 + Math.Tan(pitch * Math.PI / 180) * focal;
            var pixelsPerRadian = panorama.Width / (2 * Math.PI);
            var pixels = new int[checked(size.Width * size.Height)];
            var sky = night ? Color.FromArgb(12, 18, 35).ToArgb() : Color.FromArgb(130, 165, 190).ToArgb();
            var ground = night ? Color.FromArgb(15, 24, 18).ToArgb() : Color.FromArgb(67, 89, 57).ToArgb();

            for (var x = 0; x < size.Width; x++)
            {
                var angle = Math.Atan((x - size.Width / 2d) / focal);
                var column = panorama.Column(bearing + angle * 180 / Math.PI);
                
                // Use the same angular scale in both axes. TowerView fits its
                // viewport to the finite strip instead of stretching the photograph.
                var verticalScale = Math.Cos(angle) * pixelsPerRadian / focal;
                
                for (var y = 0; y < size.Height; y++)
                {
                    var row = (int)Math.Floor(panorama.HorizonRow + (y - horizon) * verticalScale);
                    var color = row < 0 ? sky : ground;
                    if (row >= 0 && row < panorama.Height)
                    {
                        var value = panorama.Palette[panorama.Pixels[row * panorama.Width + column]];
                        if (value != 0)
                        {
                            if (night)
                            {
                                var red = (value >> 16 & 255) / 4;
                                var green = (value >> 8 & 255) / 4;
                                var blue = (value & 255) / 3;
                                value = unchecked((int)0xFF000000) | red << 16 | green << 8 | blue;
                            }
                            color = value;
                        }
                        else
                        {
                            color = sky;
                        }
                    }

                    pixels[y * size.Width + x] = color;
                }
            }

            var replacement = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            var bitmapData = replacement.LockBits(new(Point.Empty, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            
            try
            {
                Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);
            }
            finally
            {
                replacement.UnlockBits(bitmapData);
            }

            frame?.Dispose();
            frame = replacement;
            last = key;
        }

        if (frame.Size == displaySize)
        {
            graphics.DrawImageUnscaled(frame, 0, 0);
        }
        else
        {
            graphics.DrawImage(frame, new Rectangle(Point.Empty, displaySize));
        }
    }

    public void Dispose() => frame?.Dispose();
}
