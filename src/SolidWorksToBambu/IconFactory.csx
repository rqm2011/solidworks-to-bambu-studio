using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace SolidWorksToBambu
{
    internal static class IconFactory
    {
        public static void CreateIconLists(int small, int medium, int large, out string[] commandIcons, out string[] mainIcons)
        {
            int[] sizes =
            {
                NormalizeSize(small, 20),
                NormalizeSize(medium, 32),
                NormalizeSize(large, 40)
            };

            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SolidWorksToBambu",
                "icons-v1");
            Directory.CreateDirectory(root);

            commandIcons = new string[3];
            mainIcons = new string[3];
            for (int index = 0; index < sizes.Length; index++)
            {
                int size = sizes[index];
                string commandPath = Path.Combine(root, "commands-" + size + ".png");
                string mainPath = Path.Combine(root, "main-" + size + ".png");
                DrawCommandStrip(commandPath, size);
                DrawMainIcon(mainPath, size);
                commandIcons[index] = commandPath;
                mainIcons[index] = mainPath;
            }
        }

        private static int NormalizeSize(int value, int fallback)
        {
            return value >= 16 && value <= 256 ? value : fallback;
        }

        private static void DrawCommandStrip(string path, int size)
        {
            using (Bitmap bitmap = new Bitmap(size * 2, size, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                PrepareGraphics(graphics);
                graphics.Clear(Color.Transparent);
                DrawSendIcon(graphics, new Rectangle(0, 0, size, size));
                DrawSettingsIcon(graphics, new Rectangle(size, 0, size, size));
                bitmap.Save(path, ImageFormat.Png);
            }
        }

        private static void DrawMainIcon(string path, int size)
        {
            using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                PrepareGraphics(graphics);
                graphics.Clear(Color.Transparent);
                DrawSendIcon(graphics, new Rectangle(0, 0, size, size));
                bitmap.Save(path, ImageFormat.Png);
            }
        }

        private static void PrepareGraphics(Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        private static void DrawSendIcon(Graphics graphics, Rectangle bounds)
        {
            float scale = bounds.Width / 40f;
            float x = bounds.X;
            float y = bounds.Y;
            using (Pen shell = new Pen(Color.FromArgb(56, 66, 73), Math.Max(1.4f, 2.2f * scale)))
            using (SolidBrush green = new SolidBrush(Color.FromArgb(0, 174, 104)))
            using (SolidBrush plate = new SolidBrush(Color.FromArgb(210, 221, 225)))
            {
                graphics.FillRectangle(plate, x + 5 * scale, y + 27 * scale, 30 * scale, 6 * scale);
                graphics.DrawRectangle(shell, x + 5 * scale, y + 27 * scale, 30 * scale, 6 * scale);
                PointF[] arrow =
                {
                    new PointF(x + 17 * scale, y + 6 * scale),
                    new PointF(x + 26 * scale, y + 15 * scale),
                    new PointF(x + 22 * scale, y + 15 * scale),
                    new PointF(x + 22 * scale, y + 25 * scale),
                    new PointF(x + 12 * scale, y + 25 * scale),
                    new PointF(x + 12 * scale, y + 15 * scale),
                    new PointF(x + 8 * scale, y + 15 * scale)
                };
                graphics.FillPolygon(green, arrow);
                graphics.DrawPolygon(shell, arrow);
            }
        }

        private static void DrawSettingsIcon(Graphics graphics, Rectangle bounds)
        {
            float scale = bounds.Width / 40f;
            float centerX = bounds.X + bounds.Width / 2f;
            float centerY = bounds.Y + bounds.Height / 2f;
            using (Pen gearPen = new Pen(Color.FromArgb(80, 91, 98), Math.Max(1.5f, 3f * scale)))
            using (SolidBrush gearBrush = new SolidBrush(Color.FromArgb(164, 177, 184)))
            using (SolidBrush centerBrush = new SolidBrush(Color.White))
            {
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4.0;
                    float x1 = centerX + (float)Math.Cos(angle) * 11 * scale;
                    float y1 = centerY + (float)Math.Sin(angle) * 11 * scale;
                    float x2 = centerX + (float)Math.Cos(angle) * 15 * scale;
                    float y2 = centerY + (float)Math.Sin(angle) * 15 * scale;
                    graphics.DrawLine(gearPen, x1, y1, x2, y2);
                }

                graphics.FillEllipse(gearBrush, centerX - 11 * scale, centerY - 11 * scale, 22 * scale, 22 * scale);
                graphics.DrawEllipse(gearPen, centerX - 11 * scale, centerY - 11 * scale, 22 * scale, 22 * scale);
                graphics.FillEllipse(centerBrush, centerX - 4 * scale, centerY - 4 * scale, 8 * scale, 8 * scale);
                graphics.DrawEllipse(gearPen, centerX - 4 * scale, centerY - 4 * scale, 8 * scale, 8 * scale);
            }
        }
    }
}
