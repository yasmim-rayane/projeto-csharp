using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using FishermansMemory.Core;

namespace FishermansMemory.UI
{
    public enum NauticalIcon
    {
        None,
        Anchor,
        Compass,
        Journal,
        Lighthouse,
        Helm,
        Heart,
        Pearl,
        ShieldCheck,
        CrossDanger,
        SoundOn,
        SoundOff,
        MotionOn,
        MotionOff,
        Fullscreen,
        Lock,
        PlayArrow,
        WaveSkull,
        SunTrophy,
        PracticeTarget
    }

    public static class CoastalTheme
    {
        // Wood & Brass structural palette (inspired by the coastal dock & fisherman's hut)
        public static readonly Color WoodOutline = Color.FromArgb(24, 14, 9);
        public static readonly Color WoodShadow = Color.FromArgb(54, 32, 19);
        public static readonly Color WoodBase = Color.FromArgb(92, 56, 34);
        public static readonly Color WoodLight = Color.FromArgb(128, 82, 50);
        public static readonly Color WoodHighlight = Color.FromArgb(166, 112, 70);

        public static readonly Color BrassGold = Color.FromArgb(230, 178, 74);
        public static readonly Color BrassBright = Color.FromArgb(255, 222, 122);
        public static readonly Color BrassDark = Color.FromArgb(142, 96, 32);

        // Expedition Journal Parchment palette (high-contrast comfortable reading)
        public static readonly Color ParchmentLight = Color.FromArgb(247, 237, 214);
        public static readonly Color ParchmentMid = Color.FromArgb(236, 221, 190);
        public static readonly Color ParchmentDark = Color.FromArgb(214, 193, 154);
        public static readonly Color ParchmentBorder = Color.FromArgb(164, 132, 88);

        public static readonly Color InkDark = Color.FromArgb(36, 23, 14);
        public static readonly Color InkMedium = Color.FromArgb(78, 53, 34);
        public static readonly Color InkMuted = Color.FromArgb(118, 88, 62);

        // Ocean Slate & Seafoam palette (HUD, tactical overlays, dark badges)
        public static readonly Color OceanDeep = Color.FromArgb(13, 27, 42);
        public static readonly Color OceanMid = Color.FromArgb(22, 46, 68);
        public static readonly Color OceanLight = Color.FromArgb(36, 72, 102);
        public static readonly Color OceanBorder = Color.FromArgb(64, 114, 148);

        public static readonly Color Seafoam = Color.FromArgb(76, 212, 186);
        public static readonly Color SeafoamLight = Color.FromArgb(182, 246, 232);
        public static readonly Color SandLight = Color.FromArgb(246, 235, 210);
        public static readonly Color SandMuted = Color.FromArgb(192, 178, 152);

        // Semantic Feedback palette
        public static readonly Color SafeGreen = Color.FromArgb(58, 208, 132);
        public static readonly Color SafeGreenBright = Color.FromArgb(110, 242, 174);
        public static readonly Color SafeGreenDark = Color.FromArgb(22, 88, 52);

        public static readonly Color DangerCoral = Color.FromArgb(240, 92, 80);
        public static readonly Color DangerBright = Color.FromArgb(255, 138, 128);
        public static readonly Color DangerDark = Color.FromArgb(118, 26, 26);

        public static readonly Color ComboCyan = Color.FromArgb(98, 226, 245);

        private static PrivateFontCollection fontCollection;
        private static FontFamily displayFamily;
        private static FontFamily bodyFamily;
        private static readonly Dictionary<string, Font> fontCache = new Dictionary<string, Font>();
        private static readonly object syncRoot = new object();
        private static Image cachedLogo;
        private static bool logoLoaded;

        public static Action OnButtonHoverSound;
        public static Action OnButtonSelectSound;
        internal static void NotifyButtonHover() { var a = OnButtonHoverSound; if (a != null) a(); }
        internal static void NotifyButtonSelect() { var a = OnButtonSelectSound; if (a != null) a(); }

        private static string ResolveArtPath(string fileName)
        {
            string p1 = Path.Combine(AppContext.BaseDirectory, "Assets", "Art", fileName);
            if (File.Exists(p1)) return p1;
            string asmDir = Path.GetDirectoryName(typeof(CoastalTheme).Assembly.Location) ?? AppContext.BaseDirectory;
            string p2 = Path.Combine(asmDir, "Assets", "Art", fileName);
            return File.Exists(p2) ? p2 : null;
        }

        public static Image GetLogoImage()
        {
            if (logoLoaded) return cachedLogo;
            lock (syncRoot)
            {
                if (logoLoaded) return cachedLogo;
                logoLoaded = true;
                try
                {
                    string path = ResolveArtPath("logo.png");
                    if (path != null)
                    {
                        using (var img = Image.FromFile(path))
                            cachedLogo = new Bitmap(img);
                    }
                }
                catch
                {
                    cachedLogo = null;
                }
                return cachedLogo;
            }
        }

        public static Icon LoadAppIcon()
        {
            try
            {
                string path = ResolveArtPath("app.ico");
                if (path != null) return new Icon(path);
                return Icon.ExtractAssociatedIcon(typeof(CoastalTheme).Assembly.Location);
            }
            catch
            {
                return null;
            }
        }

        private static void EnsureFontsLoaded()
        {
            if (fontCollection != null) return;
            lock (syncRoot)
            {
                if (fontCollection != null) return;
                fontCollection = new PrivateFontCollection();
                try
                {
                    string baseDir = AppContext.BaseDirectory;
                    string fontDir = Path.Combine(baseDir, "Assets", "Fonts");
                    if (!Directory.Exists(fontDir))
                    {
                        string asmDir = Path.GetDirectoryName(typeof(CoastalTheme).Assembly.Location) ?? baseDir;
                        fontDir = Path.Combine(asmDir, "Assets", "Fonts");
                    }

                    if (Directory.Exists(fontDir))
                    {
                        foreach (string file in new[] { "PixelifySans.ttf", "ChakraPetch-Regular.ttf", "ChakraPetch-Bold.ttf" })
                        {
                            string path = Path.Combine(fontDir, file);
                            if (File.Exists(path)) fontCollection.AddFontFile(path);
                        }
                    }

                    foreach (var fam in fontCollection.Families)
                    {
                        if (fam.Name.StartsWith("Pixelify Sans", StringComparison.OrdinalIgnoreCase) && displayFamily == null)
                            displayFamily = fam;
                        else if (fam.Name.StartsWith("Chakra Petch", StringComparison.OrdinalIgnoreCase) && bodyFamily == null)
                            bodyFamily = fam;
                    }
                }
                catch
                {
                    // Fallback to system fonts if custom font loading fails for any reason
                }

                if (displayFamily == null) displayFamily = FontFamily.GenericMonospace;
                if (bodyFamily == null) bodyFamily = FontFamily.GenericSansSerif;
            }
        }

        public static Font TitleFont(float sizePt, FontStyle style = FontStyle.Bold)
        {
            EnsureFontsLoaded();
            return GetCachedFont(displayFamily, sizePt, style);
        }

        public static Font BodyFont(float sizePt, FontStyle style = FontStyle.Regular)
        {
            EnsureFontsLoaded();
            return GetCachedFont(bodyFamily, sizePt, style);
        }

        private static Font GetCachedFont(FontFamily family, float sizePt, FontStyle style)
        {
            float clamped = Math.Max(7f, (float)Math.Round(sizePt * 2f) / 2f);
            string key = family.Name + ":" + clamped.ToString("0.0") + ":" + (int)style;
            lock (syncRoot)
            {
                Font font;
                if (!fontCache.TryGetValue(key, out font))
                {
                    FontStyle actualStyle = family.IsStyleAvailable(style) ? style :
                        (family.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold);
                    font = new Font(family, clamped, actualStyle, GraphicsUnit.Point);
                    fontCache[key] = font;
                }
                return font;
            }
        }

        public static Region CreatePixelRegion(Rectangle bounds, int step = 4)
        {
            if (bounds.Width <= step * 4 || bounds.Height <= step * 4 || step <= 0)
                return new Region(bounds);

            int s = step;
            var region = new Region(new Rectangle(bounds.X + s * 2, bounds.Y, bounds.Width - s * 4, bounds.Height));
            region.Union(new Rectangle(bounds.X + s, bounds.Y + s, bounds.Width - s * 2, bounds.Height - s * 2));
            region.Union(new Rectangle(bounds.X, bounds.Y + s * 2, bounds.Width, bounds.Height - s * 4));
            return region;
        }

        public static void DrawPixelBox(Graphics g, Rectangle r, Color fill, Color borderOuter, Color borderInner, int step = 3)
        {
            if (r.Width <= 4 || r.Height <= 4) return;
            var oldSmooth = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;

            using (var outerBrush = new SolidBrush(borderOuter))
            {
                g.FillRectangle(outerBrush, r.X + step, r.Y, r.Width - step * 2, r.Height);
                g.FillRectangle(outerBrush, r.X, r.Y + step, r.Width, r.Height - step * 2);
            }

            Rectangle inner1 = Rectangle.Inflate(r, -2, -2);
            if (inner1.Width > 2 && inner1.Height > 2)
            {
                using (var inBrush = new SolidBrush(borderInner))
                {
                    g.FillRectangle(inBrush, inner1.X + Math.Max(1, step - 1), inner1.Y, inner1.Width - Math.Max(1, step - 1) * 2, inner1.Height);
                    g.FillRectangle(inBrush, inner1.X, inner1.Y + Math.Max(1, step - 1), inner1.Width, inner1.Height - Math.Max(1, step - 1) * 2);
                }
            }

            Rectangle core = Rectangle.Inflate(r, -4, -4);
            if (core.Width > 0 && core.Height > 0)
            {
                using (var fillBrush = new SolidBrush(fill))
                    g.FillRectangle(fillBrush, core);
            }

            g.SmoothingMode = oldSmooth;
        }

        public static void DrawWoodFrame(Graphics g, Rectangle r, bool parchmentInterior, bool ruledLines = false, bool brassPins = true)
        {
            if (r.Width < 16 || r.Height < 16) return;
            var oldSmooth = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;

            // 1. Outer dark pixel silhouette
            using (var outline = new SolidBrush(WoodOutline))
            {
                g.FillRectangle(outline, r.X + 6, r.Y, r.Width - 12, r.Height);
                g.FillRectangle(outline, r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6);
                g.FillRectangle(outline, r.X, r.Y + 6, r.Width, r.Height - 12);
            }

            // 2. Main wood plank body
            Rectangle woodRect = Rectangle.Inflate(r, -3, -3);
            using (var woodBase = new SolidBrush(WoodBase))
            {
                g.FillRectangle(woodBase, woodRect.X + 3, woodRect.Y, woodRect.Width - 6, woodRect.Height);
                g.FillRectangle(woodBase, woodRect.X, woodRect.Y + 3, woodRect.Width, woodRect.Height - 6);
            }

            // 3. Top/Left sunlit wood highlight and Bottom/Right weathered shadow
            using (var hiPen = new Pen(WoodHighlight, 2))
            using (var shPen = new Pen(WoodShadow, 3))
            {
                g.DrawLine(hiPen, woodRect.X + 4, woodRect.Y + 1, woodRect.Right - 5, woodRect.Y + 1);
                g.DrawLine(hiPen, woodRect.X + 1, woodRect.Y + 4, woodRect.X + 1, woodRect.Bottom - 5);
                g.DrawLine(shPen, woodRect.X + 4, woodRect.Bottom - 2, woodRect.Right - 4, woodRect.Bottom - 2);
                g.DrawLine(shPen, woodRect.Right - 2, woodRect.Y + 4, woodRect.Right - 2, woodRect.Bottom - 4);
            }

            // Subtle wood plank seams & grain on the frame rim
            using (var grainPen = new Pen(WoodShadow, 1))
            {
                int midY = r.Y + r.Height / 2;
                g.DrawLine(grainPen, woodRect.X + 1, midY, woodRect.X + 10, midY);
                g.DrawLine(grainPen, woodRect.Right - 11, midY, woodRect.Right - 1, midY);
            }

            // 4. Inner rim & interior surface
            int rimThickness = Math.Max(8, Math.Min(12, Math.Min(r.Width, r.Height) / 28));
            Rectangle inner = Rectangle.Inflate(r, -rimThickness, -rimThickness);
            if (inner.Width > 8 && inner.Height > 8)
            {
                using (var shadowRim = new SolidBrush(WoodOutline))
                    g.FillRectangle(shadowRim, inner);

                Rectangle brassRim = Rectangle.Inflate(inner, -2, -2);
                using (var brassPen = new Pen(parchmentInterior ? BrassDark : OceanBorder, 2))
                    g.DrawRectangle(brassPen, brassRim.X, brassRim.Y, brassRim.Width - 1, brassRim.Height - 1);

                Rectangle surface = Rectangle.Inflate(inner, -4, -4);
                if (surface.Width > 4 && surface.Height > 4)
                {
                    if (parchmentInterior)
                        DrawParchmentSurface(g, surface, ruledLines);
                    else
                        DrawOceanSurface(g, surface);
                }
            }

            // 5. Corner brass rivets
            if (brassPins && r.Width >= 60 && r.Height >= 48)
            {
                int pinOffset = Math.Max(6, rimThickness / 2);
                DrawBrassRivet(g, r.X + pinOffset, r.Y + pinOffset);
                DrawBrassRivet(g, r.Right - pinOffset - 4, r.Y + pinOffset);
                DrawBrassRivet(g, r.X + pinOffset, r.Bottom - pinOffset - 4);
                DrawBrassRivet(g, r.Right - pinOffset - 4, r.Bottom - pinOffset - 4);
            }

            g.SmoothingMode = oldSmooth;
        }

        public static void DrawParchmentSurface(Graphics g, Rectangle surface, bool ruledLines = false)
        {
            using (var bg = new SolidBrush(ParchmentLight))
                g.FillRectangle(bg, surface);

            // Warm aged border shading
            using (var edgePen = new Pen(ParchmentMid, 4))
                g.DrawRectangle(edgePen, surface.X + 2, surface.Y + 2, surface.Width - 4, surface.Height - 4);

            using (var linePen = new Pen(ParchmentBorder, 1))
                g.DrawRectangle(linePen, surface.X + 5, surface.Y + 5, surface.Width - 11, surface.Height - 11);

            if (ruledLines && surface.Height > 60)
            {
                using (var rulePen = new Pen(Color.FromArgb(95, ParchmentDark.R, ParchmentDark.G, ParchmentDark.B), 1))
                {
                    for (int y = surface.Y + 34; y < surface.Bottom - 16; y += 26)
                        g.DrawLine(rulePen, surface.X + 16, y, surface.Right - 16, y);
                }
            }
        }

        public static void DrawOceanSurface(Graphics g, Rectangle surface)
        {
            using (var bg = new SolidBrush(OceanDeep))
                g.FillRectangle(bg, surface);

            using (var innerPen = new Pen(OceanMid, 3))
                g.DrawRectangle(innerPen, surface.X + 1, surface.Y + 1, surface.Width - 3, surface.Height - 3);
        }

        private static void DrawBrassRivet(Graphics g, int x, int y)
        {
            using (var dark = new SolidBrush(WoodOutline))
            using (var gold = new SolidBrush(BrassGold))
            using (var hi = new SolidBrush(BrassBright))
            {
                g.FillRectangle(dark, x - 1, y - 1, 6, 6);
                g.FillRectangle(gold, x, y, 4, 4);
                g.FillRectangle(hi, x, y, 2, 2);
            }
        }

        public static void DrawSubCard(Graphics g, Rectangle r, Color bg, Color border, Color? topHighlight = null)
        {
            if (r.Width <= 4 || r.Height <= 4) return;
            var oldSmooth = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;

            using (var bBrush = new SolidBrush(border))
            {
                g.FillRectangle(bBrush, r.X + 2, r.Y, r.Width - 4, r.Height);
                g.FillRectangle(bBrush, r.X, r.Y + 2, r.Width, r.Height - 4);
            }

            Rectangle inner = Rectangle.Inflate(r, -2, -2);
            using (var fBrush = new SolidBrush(bg))
                g.FillRectangle(fBrush, inner);

            if (topHighlight.HasValue && inner.Width > 4)
            {
                using (var hiPen = new Pen(topHighlight.Value, 2))
                    g.DrawLine(hiPen, inner.X + 1, inner.Y + 1, inner.Right - 2, inner.Y + 1);
            }

            g.SmoothingMode = oldSmooth;
        }

        public static void DrawNauticalIcon(Graphics g, NauticalIcon icon, RectangleF bounds, Color primary, Color? accent = null)
        {
            if (icon == NauticalIcon.None || bounds.Width <= 4 || bounds.Height <= 4) return;
            Color sec = accent ?? BrassGold;
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float r = Math.Min(bounds.Width, bounds.Height) * 0.42f;

            using (var penMain = new Pen(primary, Math.Max(1.8f, r * 0.22f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var penSec = new Pen(sec, Math.Max(1.5f, r * 0.18f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var brushMain = new SolidBrush(primary))
            using (var brushSec = new SolidBrush(sec))
            {
                switch (icon)
                {
                    case NauticalIcon.Heart:
                    {
                        PointF[] heart =
                        {
                            new PointF(cx, cy + r * 0.85f),
                            new PointF(cx - r * 0.95f, cy - r * 0.05f),
                            new PointF(cx - r * 0.85f, cy - r * 0.65f),
                            new PointF(cx - r * 0.35f, cy - r * 0.85f),
                            new PointF(cx, cy - r * 0.35f),
                            new PointF(cx + r * 0.35f, cy - r * 0.85f),
                            new PointF(cx + r * 0.85f, cy - r * 0.65f),
                            new PointF(cx + r * 0.95f, cy - r * 0.05f)
                        };
                        g.FillPolygon(brushMain, heart);
                        g.DrawPolygon(penSec, heart);
                        break;
                    }
                    case NauticalIcon.Compass:
                    {
                        g.DrawEllipse(penSec, cx - r, cy - r, r * 2, r * 2);
                        PointF[] ns = { new PointF(cx, cy - r * 0.9f), new PointF(cx + r * 0.28f, cy), new PointF(cx, cy + r * 0.9f), new PointF(cx - r * 0.28f, cy) };
                        PointF[] ew = { new PointF(cx - r * 0.9f, cy), new PointF(cx, cy - r * 0.28f), new PointF(cx + r * 0.9f, cy), new PointF(cx, cy + r * 0.28f) };
                        g.FillPolygon(brushSec, ew);
                        g.FillPolygon(brushMain, ns);
                        g.FillEllipse(Brushes.White, cx - r * 0.16f, cy - r * 0.16f, r * 0.32f, r * 0.32f);
                        break;
                    }
                    case NauticalIcon.Anchor:
                    {
                        g.DrawEllipse(penSec, cx - r * 0.28f, cy - r * 0.92f, r * 0.56f, r * 0.56f);
                        g.DrawLine(penMain, cx, cy - r * 0.35f, cx, cy + r * 0.85f);
                        g.DrawLine(penMain, cx - r * 0.55f, cy - r * 0.15f, cx + r * 0.55f, cy - r * 0.15f);
                        g.DrawArc(penMain, cx - r * 0.8f, cy - r * 0.2f, r * 1.6f, r * 1.1f, 15, 150);
                        break;
                    }
                    case NauticalIcon.Journal:
                    {
                        RectangleF book = new RectangleF(cx - r * 0.85f, cy - r * 0.75f, r * 1.7f, r * 1.5f);
                        g.FillRectangle(brushSec, book);
                        g.DrawRectangle(penMain, book.X, book.Y, book.Width, book.Height);
                        g.DrawLine(penMain, cx, book.Top, cx, book.Bottom);
                        g.DrawLine(penMain, book.X + r * 0.2f, cy - r * 0.3f, cx - r * 0.15f, cy - r * 0.3f);
                        g.DrawLine(penMain, book.X + r * 0.2f, cy + r * 0.1f, cx - r * 0.15f, cy + r * 0.1f);
                        g.DrawLine(penMain, cx + r * 0.15f, cy - r * 0.3f, book.Right - r * 0.2f, cy - r * 0.3f);
                        g.DrawLine(penMain, cx + r * 0.15f, cy + r * 0.1f, book.Right - r * 0.2f, cy + r * 0.1f);
                        break;
                    }
                    case NauticalIcon.Lighthouse:
                    {
                        PointF[] tower =
                        {
                            new PointF(cx - r * 0.28f, cy - r * 0.35f),
                            new PointF(cx + r * 0.28f, cy - r * 0.35f),
                            new PointF(cx + r * 0.48f, cy + r * 0.85f),
                            new PointF(cx - r * 0.48f, cy + r * 0.85f)
                        };
                        g.FillPolygon(brushMain, tower);
                        g.FillRectangle(brushSec, cx - r * 0.35f, cy - r * 0.75f, r * 0.7f, r * 0.4f);
                        PointF[] roof = { new PointF(cx, cy - r * 1.0f), new PointF(cx - r * 0.45f, cy - r * 0.72f), new PointF(cx + r * 0.45f, cy - r * 0.72f) };
                        g.FillPolygon(brushMain, roof);
                        g.DrawLine(penSec, cx - r * 0.95f, cy - r * 0.55f, cx - r * 0.42f, cy - r * 0.55f);
                        g.DrawLine(penSec, cx + r * 0.42f, cy - r * 0.55f, cx + r * 0.95f, cy - r * 0.55f);
                        break;
                    }
                    case NauticalIcon.Helm:
                    {
                        g.DrawEllipse(penMain, cx - r * 0.65f, cy - r * 0.65f, r * 1.3f, r * 1.3f);
                        for (int i = 0; i < 8; i++)
                        {
                            double a = i * Math.PI / 4.0;
                            g.DrawLine(penSec, cx, cy, cx + (float)Math.Cos(a) * r * 0.95f, cy + (float)Math.Sin(a) * r * 0.95f);
                        }
                        g.FillEllipse(brushMain, cx - r * 0.25f, cy - r * 0.25f, r * 0.5f, r * 0.5f);
                        break;
                    }
                    case NauticalIcon.Pearl:
                    {
                        g.FillEllipse(brushMain, cx - r * 0.75f, cy - r * 0.75f, r * 1.5f, r * 1.5f);
                        g.DrawEllipse(penSec, cx - r * 0.75f, cy - r * 0.75f, r * 1.5f, r * 1.5f);
                        g.FillEllipse(Brushes.White, cx - r * 0.4f, cy - r * 0.45f, r * 0.35f, r * 0.35f);
                        break;
                    }
                    case NauticalIcon.ShieldCheck:
                    {
                        PointF[] shield =
                        {
                            new PointF(cx, cy - r * 0.9f),
                            new PointF(cx + r * 0.8f, cy - r * 0.55f),
                            new PointF(cx + r * 0.65f, cy + r * 0.35f),
                            new PointF(cx, cy + r * 0.95f),
                            new PointF(cx - r * 0.65f, cy + r * 0.35f),
                            new PointF(cx - r * 0.8f, cy - r * 0.55f)
                        };
                        g.FillPolygon(brushSec, shield);
                        g.DrawPolygon(penMain, shield);
                        g.DrawLines(penMain, new[] { new PointF(cx - r * 0.38f, cy + r * 0.02f), new PointF(cx - r * 0.08f, cy + r * 0.35f), new PointF(cx + r * 0.45f, cy - r * 0.28f) });
                        break;
                    }
                    case NauticalIcon.CrossDanger:
                    {
                        g.FillEllipse(brushSec, cx - r * 0.85f, cy - r * 0.85f, r * 1.7f, r * 1.7f);
                        g.DrawEllipse(penMain, cx - r * 0.85f, cy - r * 0.85f, r * 1.7f, r * 1.7f);
                        g.DrawLine(penMain, cx - r * 0.42f, cy - r * 0.42f, cx + r * 0.42f, cy + r * 0.42f);
                        g.DrawLine(penMain, cx + r * 0.42f, cy - r * 0.42f, cx - r * 0.42f, cy + r * 0.42f);
                        break;
                    }
                    case NauticalIcon.SoundOn:
                    case NauticalIcon.SoundOff:
                    {
                        PointF[] speaker =
                        {
                            new PointF(cx - r * 0.75f, cy - r * 0.3f),
                            new PointF(cx - r * 0.35f, cy - r * 0.3f),
                            new PointF(cx + r * 0.1f, cy - r * 0.75f),
                            new PointF(cx + r * 0.1f, cy + r * 0.75f),
                            new PointF(cx - r * 0.35f, cy + r * 0.3f),
                            new PointF(cx - r * 0.75f, cy + r * 0.3f)
                        };
                        g.FillPolygon(brushMain, speaker);
                        if (icon == NauticalIcon.SoundOn)
                        {
                            g.DrawArc(penSec, cx - r * 0.1f, cy - r * 0.5f, r * 0.7f, r * 1.0f, -55, 110);
                            g.DrawArc(penSec, cx + r * 0.05f, cy - r * 0.8f, r * 0.9f, r * 1.6f, -55, 110);
                        }
                        else
                        {
                            g.DrawLine(penSec, cx + r * 0.3f, cy - r * 0.4f, cx + r * 0.85f, cy + r * 0.4f);
                            g.DrawLine(penSec, cx + r * 0.85f, cy - r * 0.4f, cx + r * 0.3f, cy + r * 0.4f);
                        }
                        break;
                    }
                    case NauticalIcon.MotionOn:
                    case NauticalIcon.MotionOff:
                    {
                        g.DrawArc(penMain, cx - r * 0.85f, cy - r * 0.45f, r * 0.85f, r * 0.6f, 180, 180);
                        g.DrawArc(penMain, cx, cy - r * 0.45f, r * 0.85f, r * 0.6f, 0, 180);
                        g.DrawArc(penSec, cx - r * 0.85f, cy + r * 0.05f, r * 0.85f, r * 0.6f, 180, 180);
                        g.DrawArc(penSec, cx, cy + r * 0.05f, r * 0.85f, r * 0.6f, 0, 180);
                        if (icon == NauticalIcon.MotionOff)
                            g.DrawLine(penSec, cx - r * 0.7f, cy - r * 0.7f, cx + r * 0.7f, cy + r * 0.7f);
                        break;
                    }
                    case NauticalIcon.Fullscreen:
                    {
                        float d = r * 0.75f;
                        float s = r * 0.38f;
                        g.DrawLines(penMain, new[] { new PointF(cx - d, cy - s), new PointF(cx - d, cy - d), new PointF(cx - s, cy - d) });
                        g.DrawLines(penMain, new[] { new PointF(cx + s, cy - d), new PointF(cx + d, cy - d), new PointF(cx + d, cy - s) });
                        g.DrawLines(penMain, new[] { new PointF(cx - d, cy + s), new PointF(cx - d, cy + d), new PointF(cx - s, cy + d) });
                        g.DrawLines(penMain, new[] { new PointF(cx + s, cy + d), new PointF(cx + d, cy + d), new PointF(cx + d, cy + d) });
                        g.DrawLines(penMain, new[] { new PointF(cx + s, cy + d), new PointF(cx + d, cy + d), new PointF(cx + d, cy + s) });
                        break;
                    }
                    case NauticalIcon.Lock:
                    {
                        g.DrawArc(penSec, cx - r * 0.45f, cy - r * 0.85f, r * 0.9f, r * 0.9f, 180, 180);
                        g.FillRectangle(brushMain, cx - r * 0.65f, cy - r * 0.15f, r * 1.3f, r * 1.0f);
                        g.DrawRectangle(penSec, cx - r * 0.65f, cy - r * 0.15f, r * 1.3f, r * 1.0f);
                        break;
                    }
                    case NauticalIcon.PlayArrow:
                    {
                        PointF[] tri =
                        {
                            new PointF(cx - r * 0.45f, cy - r * 0.75f),
                            new PointF(cx + r * 0.75f, cy),
                            new PointF(cx - r * 0.45f, cy + r * 0.75f)
                        };
                        g.FillPolygon(brushMain, tri);
                        g.DrawPolygon(penSec, tri);
                        break;
                    }
                    case NauticalIcon.WaveSkull:
                    {
                        g.FillEllipse(brushMain, cx - r * 0.65f, cy - r * 0.75f, r * 1.3f, r * 1.15f);
                        g.FillRectangle(brushMain, cx - r * 0.35f, cy + r * 0.15f, r * 0.7f, r * 0.55f);
                        g.FillEllipse(brushSec, cx - r * 0.42f, cy - r * 0.3f, r * 0.3f, r * 0.3f);
                        g.FillEllipse(brushSec, cx + r * 0.12f, cy - r * 0.3f, r * 0.3f, r * 0.3f);
                        break;
                    }
                    case NauticalIcon.SunTrophy:
                    {
                        var star = new PointF[10];
                        for (int i = 0; i < 10; i++)
                        {
                            double a = -Math.PI / 2 + i * Math.PI / 5;
                            float rad = i % 2 == 0 ? r * 0.95f : r * 0.45f;
                            star[i] = new PointF(cx + (float)Math.Cos(a) * rad, cy + (float)Math.Sin(a) * rad);
                        }
                        g.FillPolygon(brushMain, star);
                        g.DrawPolygon(penSec, star);
                        break;
                    }
                    case NauticalIcon.PracticeTarget:
                    {
                        g.DrawEllipse(penMain, cx - r * 0.85f, cy - r * 0.85f, r * 1.7f, r * 1.7f);
                        g.DrawEllipse(penSec, cx - r * 0.45f, cy - r * 0.45f, r * 0.9f, r * 0.9f);
                        g.FillEllipse(brushMain, cx - r * 0.18f, cy - r * 0.18f, r * 0.36f, r * 0.36f);
                        break;
                    }
                }
            }

            g.Restore(state);
        }

        public static string LaneName(Lane lane)
        {
            switch (lane)
            {
                case Lane.Up: return "CIMA";
                case Lane.Left: return "ESQUERDA";
                case Lane.Right: return "DIREITA";
                default: return "BAIXO";
            }
        }

        public static string LaneKeyOnly(Lane lane)
        {
            switch (lane)
            {
                case Lane.Up: return "W";
                case Lane.Left: return "A";
                case Lane.Right: return "D";
                default: return "S";
            }
        }

        public static string LaneArrowOnly(Lane lane)
        {
            switch (lane)
            {
                case Lane.Up: return "↑";
                case Lane.Left: return "←";
                case Lane.Right: return "→";
                default: return "↓";
            }
        }

        // Draws a compact 4-node positional cross showing where a Lane is located spatially
        public static void DrawLaneCrossMini(Graphics g, Rectangle bounds, Lane highlighted, Color activeColor, Color inactiveColor)
        {
            var oldSmooth = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            int rx = Math.Max(8, bounds.Width * 32 / 100);
            int ry = Math.Max(6, bounds.Height * 28 / 100);
            int dot = Math.Max(6, Math.Min(bounds.Width, bounds.Height) / 4);

            using (var linePen = new Pen(Color.FromArgb(110, inactiveColor), 1.5f))
            {
                g.DrawLine(linePen, cx - rx, cy, cx + rx, cy);
                g.DrawLine(linePen, cx, cy - ry, cx, cy + ry);
            }

            foreach (Lane lane in new[] { Lane.Up, Lane.Left, Lane.Center, Lane.Right })
            {
                int px = cx + (lane == Lane.Left ? -rx : lane == Lane.Right ? rx : 0);
                int py = cy + (lane == Lane.Up ? -ry : lane == Lane.Center ? ry : 0);
                bool isTarget = lane == highlighted;
                int sz = isTarget ? dot + 3 : dot - 1;
                Rectangle r = new Rectangle(px - sz / 2, py - sz / 2, sz, sz);
                using (var fill = new SolidBrush(isTarget ? activeColor : inactiveColor))
                using (var outline = new Pen(WoodOutline, isTarget ? 2f : 1f))
                {
                    g.FillEllipse(fill, r);
                    g.DrawEllipse(outline, r);
                }
            }

            g.SmoothingMode = oldSmooth;
        }
    }
}

