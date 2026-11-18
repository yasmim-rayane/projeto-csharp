using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using FishermansMemory.Core;
using FishermansMemory.Infrastructure;

namespace FishermansMemory.UI
{
    public enum ArtView { Menu, Exploration, Combat, Map, Victory }

    public sealed class Arena : Control
    {
        private readonly ArtAssets art;
        private readonly Dictionary<string, Bitmap> backgrounds = new Dictionary<string, Bitmap>();
        private Size backgroundSize;

        private void ClearBackgrounds()
        {
            foreach (var bitmap in backgrounds.Values) bitmap.Dispose();
            backgrounds.Clear();
        }

        private readonly Timer timer = new Timer { Interval = 25 };
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private ArtView view, previousView;
        private int stage, previousStage;
        private double transition, action = -100;
        private AttackResult attack;
        private int attackNumber;
        private bool dead, playing;

        public Rectangle PlayArea { get; set; }
        public bool ShowCombatFooter { get; set; } = true;
        public bool ShowPlatforms { get; set; } = true;
        public int BackdropDimAlpha { get; set; } = 0;
        public string CustomFooterPrompt { get; set; } = "";
        public event EventHandler AttackFinished;

        private bool motionEnabled = true, presentationPaused;

        public void PausePresentation(bool paused)
        {
            presentationPaused = paused;
            RefreshTimer();
        }

        public bool MotionEnabled
        {
            get { return motionEnabled; }
            set { motionEnabled = value; RefreshTimer(); Invalidate(); }
        }

        private const double AttackDuration = .65;

        private void RefreshTimer()
        {
            if (!presentationPaused && Parent != null && Visible &&
                (playing || clock.Elapsed.TotalSeconds - transition < .35 ||
                 (view == ArtView.Combat && MotionEnabled && !dead)))
                timer.Start();
            else
                timer.Stop();
        }

        public Lane PlayerLane { get; set; } = Lane.Center;

        public Arena(ArtAssets assets)
        {
            art = assets;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            ResizeRedraw = true;
            Size = new Size(850, 390);
            Margin = new Padding(0, 0, 0, 14);

            timer.Tick += (sender, args) =>
            {
                if (Visible && Parent != null) Invalidate();
                if (playing && clock.Elapsed.TotalSeconds - action >= (MotionEnabled ? AttackDuration : .12))
                {
                    playing = false;
                    var handler = AttackFinished;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
                RefreshTimer();
            };
            ParentChanged += (sender, args) => RefreshTimer();
            VisibleChanged += (sender, args) => RefreshTimer();
        }

        public void Configure(int index, ArtView mode, bool center)
        {
            if (stage != index || view != mode)
            {
                previousStage = stage;
                previousView = view;
                transition = clock.Elapsed.TotalSeconds;
                stage = index;
                view = mode;
                if (mode != ArtView.Combat)
                {
                    attack = null;
                    attackNumber = 0;
                    dead = false;
                    playing = false;
                }
            }
            timer.Start();
            Invalidate();
        }

        public void Play(AttackResult result, bool isDead, int resolvedIndex = 0)
        {
            attack = result;
            attackNumber = resolvedIndex;
            dead = isDead;
            PlayerLane = result.Chosen;
            action = clock.Elapsed.TotalSeconds;
            playing = true;
            timer.Start();
            Invalidate();
        }

        public void SetDefeated(bool value)
        {
            dead = value;
            RefreshTimer();
            Invalidate();
        }

        public void ClearAttack()
        {
            attack = null;
            attackNumber = 0;
            dead = false;
            playing = false;
            CustomFooterPrompt = "";
            Invalidate();
        }

        // Posições absolutas; não há ciclo de caminhada nem interpolação de deslocamento.
        private static PointF Position(Lane lane)
        {
            switch (lane)
            {
                case Lane.Left: return new PointF(190, 292);
                case Lane.Up: return new PointF(425, 236);
                case Lane.Right: return new PointF(660, 292);
                default: return new PointF(425, 340);
            }
        }

        private void Draw(Graphics g, string clip, RectangleF bounds, float opacity = 1, int frame = 0)
        {
            var bitmap = art.Frame(clip, frame);
            if (bitmap == null) return;
            float scale = Math.Min(bounds.Width / bitmap.Width, bounds.Height / bitmap.Height);
            float w = bitmap.Width * scale, h = bitmap.Height * scale;
            var dest = new RectangleF(bounds.X + (bounds.Width - w) / 2, bounds.Y + (bounds.Height - h) / 2, w, h);
            if (opacity >= .999f) g.DrawImage(bitmap, dest);
            else using (var attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });
                g.DrawImage(bitmap, Rectangle.Round(dest), 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        private void Background(Graphics g, int level, ArtView mode, float opacity)
        {
            string key = mode == ArtView.Menu ? "menu" : mode == ArtView.Victory ? "victory" : "scene." + Math.Max(0, Math.Min(Levels.All.Count - 1, level));
            if (Width <= 0 || Height <= 0) return;
            if (backgroundSize != ClientSize) { ClearBackgrounds(); backgroundSize = ClientSize; }
            Bitmap cached;
            if (!backgrounds.TryGetValue(key, out cached))
            {
                var bitmap = art.Frame(key, 0);
                if (bitmap == null) return;
                // At most two backgrounds are retained for the crossfade.
                if (backgrounds.Count >= 2) ClearBackgrounds();
                cached = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
                using (var target = Graphics.FromImage(cached))
                using (var edges = new ImageAttributes())
                {
                    target.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    target.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    float scale = Math.Max(Width / (float)bitmap.Width, Height / (float)bitmap.Height);
                    // Keep the Leviathan's head inside the frame on wide monitors.
                    int y = key == "scene.2" ? 0 : (int)((Height - bitmap.Height * scale) / 2);
                    var dest = new Rectangle((int)((Width - bitmap.Width * scale) / 2), y,
                        (int)Math.Ceiling(bitmap.Width * scale), (int)Math.Ceiling(bitmap.Height * scale));
                    edges.SetWrapMode(WrapMode.TileFlipXY);
                    target.DrawImage(bitmap, dest, 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, edges);
                }
                backgrounds.Add(key, cached);
            }
            if (opacity >= .999f) g.DrawImageUnscaled(cached, 0, 0);
            else using (var attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });
                g.DrawImage(cached, ClientRectangle, 0, 0, cached.Width, cached.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(Color.FromArgb(9, 20, 31));
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var saved = g.Save();

            if (!art.Available)
            {
                g.DrawString("Copie Assets/Art junto ao executável.\n" + art.Error, Font, Brushes.White, 20, 20);
                g.Restore(saved);
                return;
            }

            float fade = MotionEnabled ? (float)Math.Min(1, (clock.Elapsed.TotalSeconds - transition) / .35) : 1;
            if (fade < 1) Background(g, previousStage, previousView, 1);
            Background(g, stage, view, fade);

            Rectangle area = PlayArea.Width > 0 && PlayArea.Height > 0 ? PlayArea : ClientRectangle;
            int footerReserve = (view == ArtView.Combat && ShowCombatFooter) ? Math.Min(46, Math.Max(36, area.Height * 9 / 100)) : 0;
            Rectangle stageArea = new Rectangle(area.X, area.Y, area.Width, Math.Max(100, area.Height - footerReserve));

            float scale = Math.Min(stageArea.Width / 850f, stageArea.Height / 390f);
            g.TranslateTransform(stageArea.X + (stageArea.Width - 850 * scale) / 2f, stageArea.Y + (stageArea.Height - 390 * scale) / 2f);
            g.ScaleTransform(scale, scale);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            if (view == ArtView.Combat)
                Combat(g);

            g.Restore(saved);

            if (view == ArtView.Combat && ShowCombatFooter && footerReserve > 0)
            {
                Rectangle footerRect = new Rectangle(area.X + 12, area.Bottom - footerReserve - 4, Math.Max(100, area.Width - 24), footerReserve);
                DrawCombatFooterBar(g, footerRect);
            }

            if (BackdropDimAlpha > 0)
            {
                using (var dimBrush = new SolidBrush(Color.FromArgb(Math.Min(230, BackdropDimAlpha), 6, 12, 20)))
                    g.FillRectangle(dimBrush, ClientRectangle);
            }
        }

        private void Enemy(Graphics g, float progress)
        {
            int validStage = Math.Max(0, Math.Min(Levels.All.Count - 1, stage));
            bool strike = playing && MotionEnabled;
            double t = MotionEnabled && !dead ? clock.Elapsed.TotalSeconds : 0;
            float breath = (float)Math.Sin(t * 2.2);
            // Wind-up, impact and recovery occupy separate parts of one visual action.
            float thrust = strike ? (float)Math.Sin(Math.PI * Math.Max(0, (progress - .2f) / .8f)) : 0;
            float windup = strike && progress < .2f ? progress / .2f : 0;
            float width = validStage == 2 ? 300 : validStage == 0 ? 164 : 160;
            float height = validStage == 2 ? 165 : validStage == 0 ? 146 : 163;
            float x = 425 + (validStage == 1 && MotionEnabled ? (float)Math.Sin(t * 1.6) * 6 : 0);
            float y = validStage == 2 ? 92 : validStage == 0 ? 104 : 96;
            y += breath * 2 + thrust * (validStage == 1 ? 28 : validStage == 0 ? 24 : 18) - windup * 7;
            float rotation = strike ? (validStage == 1 ? -7 : validStage == 0 ? 5 : 3) * thrust : breath * (validStage == 0 ? 1.2f : .8f);

            var state = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(rotation);
            g.ScaleTransform(1 + thrust * .10f, 1 + breath * .018f + thrust * .08f);
            int frame = MotionEnabled
                ? (strike ? (progress < .16f ? 0 : progress < .78f ? 1 : 0) : ((int)(t * 3) % 4 == 1 ? 1 : 0))
                : (strike ? 1 : 0);
            Draw(g, "enemy." + validStage + (strike ? ".attack" : ".idle"),
                new RectangleF(-width / 2, -height / 2, width, height), 1, frame);
            g.Restore(state);
        }

        private static void PutPixelBlock(Graphics g, Brush brush, float gx, float gy, int step)
        {
            int sx = (int)Math.Round(gx / step) * step;
            int sy = (int)Math.Round(gy / step) * step;
            g.FillRectangle(brush, sx, sy, step, step);
        }

        private static void DrawPixelRingCluster(Graphics g, Brush outline, Brush main, Brush highlight, float cx, float cy, int radiusPx, int step)
        {
            for (int dy = -radiusPx; dy <= radiusPx; dy++)
            {
                for (int dx = -radiusPx; dx <= radiusPx; dx++)
                {
                    int dist2 = dx * dx + dy * dy;
                    if (dist2 > radiusPx * radiusPx) continue;
                    Brush b = dist2 >= (radiusPx - 1) * (radiusPx - 1) ? outline : (dx < 0 && dy < 0 ? highlight : main);
                    PutPixelBlock(g, b, cx + dx * step, cy + dy * step, step);
                }
            }
        }

        private void StrikeEffect(Graphics g, PointF destination, float progress)
        {
            if (!playing || progress < .18f || progress > .95f) return;
            int validStage = Math.Max(0, Math.Min(Levels.All.Count - 1, stage));
            float hit = Math.Min(1, (progress - .18f) / .47f);
            float x = destination.X, y = destination.Y - 14;

            var oldSmooth = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;
            const int step = 4;

            if (validStage == 0)
            {
                // Pixel-art twin crab pincer claws curving outward "( )" and snapping inward onto the platform
                float gap = (1f - hit) * 26f;
                using (var outline = new SolidBrush(Color.FromArgb(26, 8, 12)))
                using (var crimson = new SolidBrush(Color.FromArgb(218, 56, 42)))
                using (var coral = new SolidBrush(Color.FromArgb(255, 168, 64)))
                using (var spark = new SolidBrush(Color.FromArgb(255, 245, 190)))
                {
                    const int steps = 32;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        for (int i = 0; i <= steps; i++)
                        {
                            float u = i / (float)steps;
                            float angle = (u - 0.5f) * 2.25f;
                            float px = x + side * (4f + gap + (float)Math.Cos(angle) * 22f);
                            float py = y + (float)Math.Sin(angle) * 32f - 4f;
                            PutPixelBlock(g, outline, px - step, py, step);
                            PutPixelBlock(g, outline, px + step, py, step);
                            PutPixelBlock(g, outline, px, py - step, step);
                            PutPixelBlock(g, outline, px, py + step, step);
                            if (i > 4 && i < steps - 4)
                            {
                                PutPixelBlock(g, outline, px + side * step, py - step, step);
                                PutPixelBlock(g, outline, px + side * step, py + step, step);
                            }
                        }
                        for (int i = 0; i <= steps; i++)
                        {
                            float u = i / (float)steps;
                            float angle = (u - 0.5f) * 2.25f;
                            float px = x + side * (4f + gap + (float)Math.Cos(angle) * 22f);
                            float py = y + (float)Math.Sin(angle) * 32f - 4f;
                            PutPixelBlock(g, crimson, px, py, step);
                            if (i > 4 && i < steps - 4)
                                PutPixelBlock(g, coral, px - side * step * 0.5f, py, step);
                            // Inner serrated claw teeth facing inward
                            if (i == 9 || i == 16 || i == 23)
                            {
                                PutPixelBlock(g, outline, px - side * step * 2f, py, step);
                                PutPixelBlock(g, spark, px - side * step * 1.2f, py, step);
                            }
                        }
                    }

                    // Impact pixel sparks when claws snap shut
                    if (hit > .52f)
                    {
                        float burst = (hit - .52f) / .48f;
                        int spread = (int)(12 + burst * 18);
                        PutPixelBlock(g, outline, x, y - step, step);
                        PutPixelBlock(g, spark, x, y, step);
                        PutPixelBlock(g, spark, x - step, y, step);
                        PutPixelBlock(g, spark, x + step, y, step);
                        PutPixelBlock(g, coral, x - spread, y - spread / 2f, step);
                        PutPixelBlock(g, coral, x + spread, y - spread / 2f, step);
                        PutPixelBlock(g, spark, x - spread * 0.7f, y + spread * 0.4f, step);
                        PutPixelBlock(g, spark, x + spread * 0.7f, y + spread * 0.4f, step);
                    }
                }
            }
            else if (validStage == 1)
            {
                // Pixel-art triple diagonal water-blade slashes (continuous, no checkerboard gaps)
                using (var outline = new SolidBrush(Color.FromArgb(10, 26, 48)))
                using (var aqua = new SolidBrush(Color.FromArgb(40, 196, 240)))
                using (var foam = new SolidBrush(Color.FromArgb(240, 250, 255)))
                {
                    const int segs = 32;
                    for (int blade = -1; blade <= 1; blade++)
                    {
                        float offset = blade * 18f;
                        float startX = x - 34f + offset;
                        float startY = y - 42f;
                        float endX = x + 22f + offset;
                        float endY = y + 14f;
                        int maxSeg = Math.Max(4, (int)(segs * Math.Min(1f, hit * 1.25f)));

                        for (int i = 0; i <= maxSeg; i++)
                        {
                            float u = i / (float)segs;
                            float px = startX + (endX - startX) * u;
                            float py = startY + (endY - startY) * u;
                            PutPixelBlock(g, outline, px - step, py, step);
                            PutPixelBlock(g, outline, px + step, py, step);
                            PutPixelBlock(g, outline, px, py - step, step);
                            PutPixelBlock(g, outline, px, py + step, step);
                        }
                        for (int i = 0; i <= maxSeg; i++)
                        {
                            float u = i / (float)segs;
                            float px = startX + (endX - startX) * u;
                            float py = startY + (endY - startY) * u;
                            PutPixelBlock(g, aqua, px, py, step);
                            if (i > 2 && i < maxSeg - 1)
                                PutPixelBlock(g, foam, px, py - step, step);
                        }
                    }
                }
            }
            else
            {
                // Pixel-art segmented tentacle slam extending from the Leviathan
                PointF p0 = new PointF(425, 140);
                PointF p1 = new PointF(x - 110, 165);
                PointF p2 = new PointF(x + 65, y - 65);
                PointF p3 = new PointF(x, y);

                using (var outline = new SolidBrush(Color.FromArgb(10, 14, 32)))
                using (var body = new SolidBrush(Color.FromArgb(72, 60, 152)))
                using (var ridge = new SolidBrush(Color.FromArgb(128, 112, 224)))
                using (var sucker = new SolidBrush(Color.FromArgb(208, 196, 255)))
                {
                    const int steps = 56;
                    int maxStep = Math.Max(4, (int)(steps * hit));
                    // Pass 1: continuous dark pixel outline
                    for (int i = 0; i <= maxStep; i++)
                    {
                        float u = i / (float)steps;
                        float inv = 1f - u;
                        float bx = inv * inv * inv * p0.X + 3 * inv * inv * u * p1.X + 3 * inv * u * u * p2.X + u * u * u * p3.X;
                        float by = inv * inv * inv * p0.Y + 3 * inv * inv * u * p1.Y + 3 * inv * u * u * p2.Y + u * u * u * p3.Y;
                        int rad = u < 0.72f ? 2 : 1;
                        DrawPixelRingCluster(g, outline, outline, outline, bx, by, rad, step);
                    }
                    // Pass 2: inner indigo body & highlight ridge
                    for (int i = 0; i <= maxStep; i++)
                    {
                        float u = i / (float)steps;
                        float inv = 1f - u;
                        float bx = inv * inv * inv * p0.X + 3 * inv * inv * u * p1.X + 3 * inv * u * u * p2.X + u * u * u * p3.X;
                        float by = inv * inv * inv * p0.Y + 3 * inv * inv * u * p1.Y + 3 * inv * u * u * p2.Y + u * u * u * p3.Y;
                        PutPixelBlock(g, body, bx, by, step);
                        PutPixelBlock(g, ridge, bx - step, by - step, step);
                        if (u < 0.72f)
                        {
                            PutPixelBlock(g, body, bx + step, by, step);
                            PutPixelBlock(g, body, bx, by + step, step);
                        }
                        if (i % 6 == 2 && i > 4 && i < maxStep - 2)
                            PutPixelBlock(g, sucker, bx + step, by + step, step);
                    }
                    if (hit > .6f)
                    {
                        PutPixelBlock(g, sucker, x - 16, y - 8, step);
                        PutPixelBlock(g, sucker, x + 16, y - 8, step);
                        PutPixelBlock(g, ridge, x - 12, y - 20, step);
                        PutPixelBlock(g, ridge, x + 12, y - 20, step);
                    }
                }
            }

            if (hit > .60f) Draw(g, "splash", new RectangleF(x - 32, y - 24, 64, 54));
            g.SmoothingMode = oldSmooth;
        }

        private void Combat(Graphics g)
        {
            int validStage = Math.Max(0, Math.Min(Levels.All.Count - 1, stage));
            double elapsed = clock.Elapsed.TotalSeconds - action;
            float progress = MotionEnabled ? (float)Math.Min(1, elapsed / AttackDuration) : .65f;

            Enemy(g, progress);
            if (!ShowPlatforms) return;

            // Subtle arena ground shadow ellipse to anchor the 4 platforms without obscuring the pixel-art scene
            var oldSmooth = g.SmoothingMode;
            var oldTextHint = g.TextRenderingHint;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (var arenaShade = new SolidBrush(Color.FromArgb(72, 6, 14, 24)))
                g.FillEllipse(arenaShade, 75, 204, 700, 176);

            var keyFont = CoastalTheme.TitleFont(13f, FontStyle.Bold);
            var dirFont = CoastalTheme.TitleFont(8f, FontStyle.Bold);
            var tagFont = CoastalTheme.TitleFont(8.5f, FontStyle.Bold);

            using (var centerFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            foreach (Lane lane in Levels.Lanes(Levels.All[validStage]))
            {
                PointF p = Position(lane);
                bool selected = PlayerLane == lane;
                bool showingResult = attack != null && playing;
                bool wasLastSafe = attack != null && !playing && attack.Safe == lane;

                Color ringColor = showingResult
                    ? (attack.Safe == lane ? CoastalTheme.SafeGreenBright : CoastalTheme.DangerCoral)
                    : (selected ? CoastalTheme.BrassBright : Color.FromArgb(215, 228, 240));

                var platform = new RectangleF(p.X - 58, p.Y - 13, 116, 34);

                // Drop shadow & dark rim
                using (var dropBrush = new SolidBrush(Color.FromArgb(160, 4, 8, 14)))
                    g.FillEllipse(dropBrush, platform.X - 3, platform.Y + 4, platform.Width + 6, platform.Height + 2);

                using (var fill = new SolidBrush(showingResult
                    ? (attack.Safe == lane ? Color.FromArgb(240, 14, 52, 36) : Color.FromArgb(240, 56, 18, 22))
                    : Color.FromArgb(240, 14, 28, 44)))
                    g.FillEllipse(fill, platform);

                using (var outline = new Pen(CoastalTheme.WoodOutline, 7))
                    g.DrawEllipse(outline, platform);

                using (var pen = new Pen(selected && !showingResult ? CoastalTheme.BrassBright : ringColor, selected || showingResult ? 4.5f : 2.5f))
                    g.DrawEllipse(pen, platform);

                if (selected)
                {
                    var innerRing = RectangleF.Inflate(platform, -5, -3);
                    using (var goldInner = new Pen(Color.FromArgb(190, CoastalTheme.BrassGold), 1.5f) { DashStyle = DashStyle.Dot })
                        g.DrawEllipse(goldInner, innerRing);
                }

                // Nautical Key Plaque associated with the platform (drawn using GDI+ so it scales with Arena transform)
                float badgeX = lane == Lane.Left ? p.X - 146 : p.X + 64;
                Rectangle badge = new Rectangle((int)badgeX, (int)(p.Y - 19), 82, 38);
                Color badgeBorder = showingResult ? ringColor : (selected ? CoastalTheme.BrassBright : CoastalTheme.BrassGold);
                CoastalTheme.DrawSubCard(g, badge, CoastalTheme.WoodShadow, CoastalTheme.WoodOutline, badgeBorder);

                RectangleF keyTop = new RectangleF(badge.X + 2, badge.Y + 2, badge.Width - 4, 21);
                RectangleF dirBot = new RectangleF(badge.X + 2, badge.Y + 21, badge.Width - 4, 15);
                g.DrawString(Levels.Symbol(lane), keyFont, Brushes.White, keyTop, centerFormat);
                using (var dirBrush = new SolidBrush(selected ? CoastalTheme.BrassBright : CoastalTheme.SandMuted))
                    g.DrawString(CoastalTheme.LaneName(lane), dirFont, dirBrush, dirBot, centerFormat);

                // Result Tag below the platform during attack
                if (showingResult)
                {
                    bool isSafe = attack.Safe == lane;
                    string label = isSafe ? "✓ SEGURA" : "✕ ATAQUE";
                    Rectangle tag = new Rectangle((int)(p.X - 48), (int)(p.Y + 22), 96, 20);
                    CoastalTheme.DrawSubCard(g, tag,
                        isSafe ? CoastalTheme.SafeGreenDark : CoastalTheme.DangerDark,
                        CoastalTheme.WoodOutline,
                        isSafe ? CoastalTheme.SafeGreenBright : CoastalTheme.DangerBright);
                    using (var tagBrush = new SolidBrush(isSafe ? CoastalTheme.SafeGreenBright : CoastalTheme.DangerBright))
                        g.DrawString(label, tagFont, tagBrush, new RectangleF(tag.X, tag.Y + 1, tag.Width, tag.Height), centerFormat);
                }

                if (showingResult && attack.Safe != lane)
                    StrikeEffect(g, p, progress);
            }

            // Draw Hero Sprite on the chosen platform
            g.SmoothingMode = SmoothingMode.None;
            var pos = Position(PlayerLane);
            string pose = dead ? "hero.dead" : (attack != null && playing ? (attack.Correct ? "hero.dodge" : "hero.hurt") : "hero.idle");
            RectangleF heroBounds = dead
                ? new RectangleF(pos.X - 46, pos.Y - 30, 92, 36)
                : (pose == "hero.idle"
                    ? new RectangleF(pos.X - 32, pos.Y - 74, 64, 76)
                    : new RectangleF(pos.X - 38, pos.Y - 76, 76, 80));
            Draw(g, pose, heroBounds);

            // Floating Immediate Feedback Callout above the combat area while an attack is playing
            if (attack != null && playing)
            {
                DrawFloatingCombatCallout(g, attack);
            }

            g.TextRenderingHint = oldTextHint;
            g.SmoothingMode = oldSmooth;
        }

        private void DrawFloatingCombatCallout(Graphics g, AttackResult res)
        {
            string text;
            Color bg, border, fg;
            NauticalIcon icon;

            if (res.Correct && res.Combo)
            {
                text = "★ COMBO ×3! +100 PTS • +150 BÔNUS • +" + res.Healing + " HP!";
                bg = Color.FromArgb(240, 18, 62, 74);
                border = CoastalTheme.ComboCyan;
                fg = CoastalTheme.BrassBright;
                icon = NauticalIcon.SunTrophy;
            }
            else if (res.Correct)
            {
                text = "✓ ESQUIVA CORRETA! +100 PONTOS";
                bg = Color.FromArgb(240, 20, 68, 42);
                border = CoastalTheme.SafeGreenBright;
                fg = CoastalTheme.SafeGreenBright;
                icon = NauticalIcon.ShieldCheck;
            }
            else
            {
                text = "✕ GOLPE SOFRIDO! −" + res.Damage + " HP e −50 PTS • Segura: " + Levels.Symbol(res.Safe);
                bg = Color.FromArgb(240, 82, 22, 22);
                border = CoastalTheme.DangerBright;
                fg = Color.FromArgb(255, 220, 215);
                icon = NauticalIcon.CrossDanger;
            }

            var font = CoastalTheme.TitleFont(10.5f, FontStyle.Bold);
            SizeF sz = g.MeasureString(text, font);
            int w = Math.Min(780, (int)Math.Ceiling(sz.Width) + 52);
            int h = 32;
            Rectangle banner = new Rectangle(425 - w / 2, 6, w, h);

            CoastalTheme.DrawSubCard(g, banner, bg, CoastalTheme.WoodOutline, border);
            CoastalTheme.DrawNauticalIcon(g, icon, new RectangleF(banner.X + 8, banner.Y + 6, 22, 22), fg, border);
            using (var fgBrush = new SolidBrush(fg))
            using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(text, font, fgBrush, new RectangleF(banner.X + 35, banner.Y + 1, banner.Width - 40, banner.Height - 2), sf);
            }
        }

        private void DrawCombatFooterBar(Graphics g, Rectangle r)
        {
            bool compact = r.Width < 760 || r.Height < 42;
            CoastalTheme.DrawPixelBox(g, r, CoastalTheme.OceanDeep, CoastalTheme.WoodOutline, CoastalTheme.OceanBorder, 2);

            Color stAccent = CoastalTheme.Seafoam;
            NauticalIcon stIcon = NauticalIcon.Compass;
            string stText = !string.IsNullOrEmpty(CustomFooterPrompt)
                ? CustomFooterPrompt
                : "Use WASD ou as Setas para escolher a posição segura.";

            if (attack != null)
            {
                string atkPrefix = attackNumber > 0 ? ("Ataque #" + attackNumber + ": ") : "";
                if (attack.Correct && attack.Combo)
                {
                    stAccent = CoastalTheme.ComboCyan;
                    stIcon = NauticalIcon.SunTrophy;
                    stText = atkPrefix + "★ Esquiva correta + Combo ×3! (+250 pts e +" + attack.Healing + " HP)";
                }
                else if (attack.Correct)
                {
                    stAccent = CoastalTheme.SafeGreenBright;
                    stIcon = NauticalIcon.ShieldCheck;
                    stText = atkPrefix + "✓ Esquiva correta em " + Levels.Symbol(attack.Chosen) + " (+100 pts)";
                }
                else
                {
                    stAccent = CoastalTheme.DangerBright;
                    stIcon = NauticalIcon.CrossDanger;
                    stText = atkPrefix + "✕ Golpe sofrido (−" + attack.Damage + " HP) • Segura era " + Levels.Symbol(attack.Safe) + " (" + CoastalTheme.LaneName(attack.Safe) + ")";
                }
            }

            int iconSz = compact ? 16 : 20;
            RectangleF iconRect = new RectangleF(r.X + 10, r.Y + (r.Height - iconSz) / 2f, iconSz, iconSz);
            CoastalTheme.DrawNauticalIcon(g, stIcon, iconRect, stAccent, CoastalTheme.BrassBright);

            var bodyFont = CoastalTheme.BodyFont(compact ? 8.5f : 10f, FontStyle.Bold);
            int tx = (int)iconRect.Right + 8;
            TextRenderer.DrawText(g, stText, bodyFont,
                new Rectangle(tx, r.Y + 2, Math.Max(40, r.Right - tx - 10), r.Height - 4),
                stAccent, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Stop();
                timer.Dispose();
                clock.Stop();
                ClearBackgrounds();
            }
            base.Dispose(disposing);
        }
    }
}
