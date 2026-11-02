using System;
using System.Drawing;
using System.Windows.Forms;

namespace FishermansMemory.UI
{
    // Painted UI: no external icon font or raster HUD dependency.
    public sealed class GameHud : Control
    {
        private int hp = 100, score, combo;
        public GameHud()
        {
            DoubleBuffered = true; ResizeRedraw = true;
            BackColor = Color.FromArgb(10, 18, 29);
            AccessibleRole = AccessibleRole.StaticText;
        }
        public void UpdateValues(int health, int points, int streak)
        {
            hp = health; score = points; combo = streak;
            AccessibleName = "Vida " + hp + " de 100. Pontos " + score + ". Combo " + combo + " de 3.";
            Invalidate();
        }
        private static void Frame(Graphics g, Rectangle r)
        {
            g.FillRectangle(Brushes.Black, r);
            using (var rim = new Pen(Color.FromArgb(203, 189, 145), 3)) g.DrawRectangle(rim, r);
            using (var inside = new SolidBrush(Color.FromArgb(26, 35, 47)))
                g.FillRectangle(inside, r.X + 6, r.Y + 6, r.Width - 12, r.Height - 12);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float scale = Math.Min(Width / 850f, Height / 84f);
            var g = e.Graphics; g.TranslateTransform((Width - 850 * scale) / 2, (Height - 84 * scale) / 2); g.ScaleTransform(scale, scale);
            Frame(g, new Rectangle(5, 5, 375, 72));
            Frame(g, new Rectangle(392, 5, 245, 72));
            Frame(g, new Rectangle(649, 5, 195, 72));
            Point[] heart = { new Point(20,29),new Point(20,18),new Point(31,14),new Point(41,22),new Point(51,14),new Point(62,18),new Point(62,29),new Point(41,49) };
            g.FillPolygon(Brushes.Crimson, heart); g.DrawPolygon(Pens.MistyRose, heart);
            using (var small = new Font("Consolas", 13, FontStyle.Bold))
            using (var large = new Font("Consolas", 22, FontStyle.Bold))
            {
                g.DrawString("HP " + hp + "/100", small, Brushes.White, 78, 12);
                for (int i = 0; i < 20; i++)
                {
                    var cell = new Rectangle(78 + i * 14, 40, 11, 22);
                    g.FillRectangle(Brushes.DimGray, cell);
                    int fill = Math.Max(0, Math.Min(5, hp - i * 5));
                    if (fill > 0) using (var brush = new SolidBrush(hp <= 30 ? Color.OrangeRed : Color.FromArgb(239, 53, 65)))
                        g.FillRectangle(brush, cell.X, cell.Y, cell.Width * fill / 5f, cell.Height);
                }
                // A small star, intentionally drawn as geometry rather than a font glyph.
                var star = new PointF[10];
                for (int i = 0; i < star.Length; i++)
                { double angle = -Math.PI / 2 + i * Math.PI / 5; float radius = i % 2 == 0 ? 18 : 8;
                  star[i] = new PointF(422 + (float)Math.Cos(angle) * radius, 42 + (float)Math.Sin(angle) * radius); }
                g.FillPolygon(Brushes.Gold, star);
                g.DrawString("PONTOS", small, Brushes.Khaki, 450, 12);
                g.DrawString(score.ToString(), large, Brushes.White, 450, 31);
                g.DrawString("COMBO", small, Brushes.LightCyan, 670, 12);
                g.DrawString(combo + " / 3", large, Brushes.White, 670, 31);
            }
        }
    }
}
