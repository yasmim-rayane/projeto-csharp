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
        public bool ConfirmActionEnabled { get; set; }
        public event EventHandler AttackFinished;
        public event EventHandler ConfirmRequested;
        public event Action<Lane> LaneSelectedByMouse;

        private Rectangle confirmButtonRect;
        private bool confirmHovered, confirmPressed;
        private Lane? hoveredLane;
        private bool centerLaneAllowed = true;

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
            centerLaneAllowed = center;
            if (!center && PlayerLane == Lane.Center)
                PlayerLane = Lane.Left;

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
                case Lane.Left: return new PointF(185, 300);
                case Lane.Right: return new PointF(665, 300);
                default: return new PointF(425, 316);
            }
        }

        private RectangleF HitRectForLane(Lane lane)
        {
            PointF p = Position(lane);
            if (lane == Lane.Left) return new RectangleF(p.X - 148, p.Y - 68, 210, 96);
            return new RectangleF(p.X - 60, p.Y - 68, 208, 96);
        }

        private bool TryPointToStageCoords(Point clientPt, out PointF stagePt)
        {
            Rectangle area = PlayArea.Width > 0 && PlayArea.Height > 0 ? PlayArea : ClientRectangle;
            int footerReserve = (view == ArtView.Combat && ShowCombatFooter) ? Math.Min(46, Math.Max(36, area.Height * 9 / 100)) : 0;
            Rectangle stageArea = new Rectangle(area.X, area.Y, area.Width, Math.Max(100, area.Height - footerReserve));
            float scale = Math.Min(stageArea.Width / 850f, stageArea.Height / 390f);
            if (scale <= 0.01f)
            {
                stagePt = PointF.Empty;
                return false;
            }
            float ox = stageArea.X + (stageArea.Width - 850 * scale) / 2f;
            float oy = stageArea.Y + (stageArea.Height - 390 * scale) / 2f;
            stagePt = new PointF((clientPt.X - ox) / scale, (clientPt.Y - oy) / scale);
            return true;
        }

        private Lane[] ActiveLanes() =>
            centerLaneAllowed
                ? new[] { Lane.Left, Lane.Center, Lane.Right }
                : new[] { Lane.Left, Lane.Right };

        private Lane? HitTestLane(Point clientPt)
        {
            if (view != ArtView.Combat || !ShowPlatforms || !ConfirmActionEnabled || playing || dead)
                return null;
            PointF stagePt;
            if (!TryPointToStageCoords(clientPt, out stagePt)) return null;

            foreach (Lane lane in ActiveLanes())
            {
                if (HitRectForLane(lane).Contains(stagePt))
                    return lane;
            }
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool overConfirm = ConfirmActionEnabled && ShowCombatFooter && view == ArtView.Combat && confirmButtonRect.Contains(e.Location);
            Lane? overLane = HitTestLane(e.Location);

            if (overConfirm != confirmHovered || overLane != hoveredLane)
            {
                if (overConfirm && !confirmHovered) CoastalTheme.NotifyButtonHover();
                confirmHovered = overConfirm;
                hoveredLane = overLane;
                Cursor = (confirmHovered || hoveredLane.HasValue) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (confirmHovered || confirmPressed || hoveredLane.HasValue)
            {
                confirmHovered = false;
                confirmPressed = false;
                hoveredLane = null;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (ConfirmActionEnabled && ShowCombatFooter && view == ArtView.Combat && confirmButtonRect.Contains(e.Location))
            {
                confirmPressed = true;
                Invalidate();
                return;
            }
            Lane? lane = HitTestLane(e.Location);
            if (lane.HasValue)
            {
                CoastalTheme.NotifyButtonSelect();
                PlayerLane = lane.Value;
                Invalidate();
                var handler = LaneSelectedByMouse;
                if (handler != null) handler(lane.Value);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (confirmPressed)
            {
                bool trigger = ConfirmActionEnabled && confirmButtonRect.Contains(e.Location);
                confirmPressed = false;
                Invalidate();
                if (trigger)
                {
                    CoastalTheme.NotifyButtonSelect();
                    var handler = ConfirmRequested;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
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
                    int y = key == "scene.5" ? 0 : (int)((Height - bitmap.Height * scale) / 2);
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

            float width, height, baseY, swayAmp = 0, swaySpeed = 1.6f, thrustDist = 24, strikeRot = 4;
            switch (validStage)
            {
                case 0: // Caranguejo
                    width = 164; height = 146; baseY = 104; thrustDist = 24; strikeRot = 5;
                    break;
                case 1: // Tubarão
                    width = 160; height = 163; baseY = 96; swayAmp = 6; swaySpeed = 1.6f; thrustDist = 28; strikeRot = -7;
                    break;
                case 2: // Pelicano
                    width = 168; height = 158; baseY = 98; swayAmp = 4; swaySpeed = 1.4f; thrustDist = 26; strikeRot = 4;
                    break;
                case 3: // Arraia
                    width = 172; height = 161; baseY = 96; swayAmp = 9; swaySpeed = 2.0f; thrustDist = 26; strikeRot = -5;
                    break;
                case 4: // Moreia
                    width = 166; height = 156; baseY = 98; swayAmp = 5; swaySpeed = 1.8f; thrustDist = 30; strikeRot = 4;
                    break;
                default: // Fase 6: Boss Leviatã
                    width = 300; height = 165; baseY = 92; thrustDist = 18; strikeRot = 3;
                    break;
            }

            float x = 425 + (MotionEnabled && !dead && swayAmp > 0 ? (float)Math.Sin(t * swaySpeed) * swayAmp : 0);
            float y = baseY + breath * (validStage == 3 ? 2.8f : 2f) + thrust * thrustDist - windup * 7;
            float rotation = strike ? strikeRot * thrust : breath * (validStage == 0 || validStage == 4 ? 1.2f : .8f);

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
                            if (i == 9 || i == 16 || i == 23)
                            {
                                PutPixelBlock(g, outline, px - side * step * 2f, py, step);
                                PutPixelBlock(g, spark, px - side * step * 1.2f, py, step);
                            }
                        }
                    }

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
                // Pixel-art triple diagonal water-blade slashes
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
            else if (validStage == 2)
            {
                // Pelicano: mergulho de bico dourado com rajadas laterais de vento e penas
                float diveY = y - 54f + hit * 54f;
                using (var outline = new SolidBrush(Color.FromArgb(24, 16, 12)))
                using (var goldBeak = new SolidBrush(Color.FromArgb(236, 166, 48)))
                using (var cream = new SolidBrush(Color.FromArgb(248, 242, 228)))
                using (var gust = new SolidBrush(Color.FromArgb(220, 236, 250)))
                {
                    // Central downward beak wedge
                    for (int row = 0; row <= 10; row++)
                    {
                        int halfW = Math.Max(0, (10 - row) / 2);
                        float py = diveY - 36f + row * step;
                        for (int dx = -halfW - 1; dx <= halfW + 1; dx++)
                        {
                            bool edge = Math.Abs(dx) == halfW + 1 || row == 0 || row == 10;
                            Brush b = edge ? outline : (Math.Abs(dx) <= 1 ? cream : goldBeak);
                            PutPixelBlock(g, b, x + dx * step, py, step);
                        }
                    }
                    // Twin sweeping wind/feather arcs on left & right
                    if (hit > .35f)
                    {
                        float spread = (hit - .35f) / .65f;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            for (int i = 0; i <= 14; i++)
                            {
                                float u = i / 14f;
                                float gx = x + side * (12f + u * (24f + spread * 18f));
                                float gy = y - (float)Math.Sin(u * Math.PI) * 18f + 4f;
                                PutPixelBlock(g, outline, gx, gy - step, step);
                                PutPixelBlock(g, outline, gx, gy + step, step);
                                PutPixelBlock(g, gust, gx, gy, step);
                            }
                        }
                    }
                }
            }
            else if (validStage == 3)
            {
                // Arraia: chicote de cauda turquesa em arco com faíscas bioelétricas douradas
                using (var outline = new SolidBrush(Color.FromArgb(10, 24, 34)))
                using (var teal = new SolidBrush(Color.FromArgb(38, 138, 152)))
                using (var shock = new SolidBrush(Color.FromArgb(255, 224, 88)))
                using (var spark = new SolidBrush(Color.FromArgb(255, 250, 210)))
                {
                    const int steps = 36;
                    int maxStep = Math.Max(4, (int)(steps * hit));
                    for (int pass = 0; pass < 2; pass++)
                    {
                        for (int i = 0; i <= maxStep; i++)
                        {
                            float u = i / (float)steps;
                            float angle = -2.35f + u * 3.1f;
                            float ax = x + (float)Math.Cos(angle) * 34f;
                            float ay = y - 10f + (float)Math.Sin(angle) * 28f;
                            if (pass == 0)
                            {
                                DrawPixelRingCluster(g, outline, outline, outline, ax, ay, 1, step);
                            }
                            else
                            {
                                Brush b = (i > maxStep - 5) ? shock : teal;
                                PutPixelBlock(g, b, ax, ay, step);
                                if (i % 5 == 0)
                                    PutPixelBlock(g, spark, ax - step, ay - step, step);
                            }
                        }
                    }
                    if (hit > .55f)
                    {
                        float burst = (hit - .55f) / .45f;
                        int d = (int)(10 + burst * 18);
                        PutPixelBlock(g, shock, x - d, y - d / 2f, step);
                        PutPixelBlock(g, shock, x + d, y - d / 2f, step);
                        PutPixelBlock(g, spark, x - d / 2f, y + d / 2f, step);
                        PutPixelBlock(g, spark, x + d / 2f, y + d / 2f, step);
                    }
                }
            }
            else if (validStage == 4)
            {
                // Moreia: bote vertical de mandíbulas verde-esmeralda com presas brancas convergentes
                float jawGap = (1f - hit) * 28f;
                using (var outline = new SolidBrush(Color.FromArgb(10, 24, 16)))
                using (var emerald = new SolidBrush(Color.FromArgb(46, 148, 86)))
                using (var lime = new SolidBrush(Color.FromArgb(172, 224, 118)))
                using (var fang = new SolidBrush(Color.FromArgb(248, 252, 244)))
                {
                    for (int jaw = -1; jaw <= 1; jaw += 2)
                    {
                        float jy = y + jaw * (6f + jawGap);
                        for (int dx = -6; dx <= 6; dx++)
                        {
                            float curve = (dx * dx) * 0.45f * jaw;
                            float px = x + dx * step;
                            float py = jy - curve;
                            PutPixelBlock(g, outline, px, py - step, step);
                            PutPixelBlock(g, outline, px, py + step, step);
                            PutPixelBlock(g, emerald, px, py, step);
                            if (Math.Abs(dx) % 3 == 1)
                            {
                                // Sharp inward-pointing fangs
                                PutPixelBlock(g, outline, px, py - jaw * step * 2, step);
                                PutPixelBlock(g, fang, px, py - jaw * step, step);
                            }
                        }
                    }
                    if (hit > .55f)
                    {
                        PutPixelBlock(g, lime, x - 20, y, step);
                        PutPixelBlock(g, lime, x + 20, y, step);
                        PutPixelBlock(g, fang, x, y, step);
                    }
                }
            }
            else
            {
                // Boss Leviatã: tentáculo segmentado índigo/roxo com ventosas
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
                    for (int i = 0; i <= maxStep; i++)
                    {
                        float u = i / (float)steps;
                        float inv = 1f - u;
                        float bx = inv * inv * inv * p0.X + 3 * inv * inv * u * p1.X + 3 * inv * u * u * p2.X + u * u * u * p3.X;
                        float by = inv * inv * inv * p0.Y + 3 * inv * inv * u * p1.Y + 3 * inv * u * u * p2.Y + u * u * u * p3.Y;
                        int rad = u < 0.72f ? 2 : 1;
                        DrawPixelRingCluster(g, outline, outline, outline, bx, by, rad, step);
                    }
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
            double elapsed = clock.Elapsed.TotalSeconds - action;
            float progress = MotionEnabled ? (float)Math.Min(1, elapsed / AttackDuration) : .65f;

            Enemy(g, progress);
            if (!ShowPlatforms) return;

            var oldSmooth = g.SmoothingMode;
            var oldTextHint = g.TextRenderingHint;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (var arenaShade = new SolidBrush(Color.FromArgb(72, 6, 14, 24)))
                g.FillEllipse(arenaShade, 75, 224, 700, 152);

            var keyFont = CoastalTheme.TitleFont(13f, FontStyle.Bold);
            var dirFont = CoastalTheme.TitleFont(8f, FontStyle.Bold);
            var tagFont = CoastalTheme.TitleFont(8.5f, FontStyle.Bold);

            using (var centerFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            foreach (Lane lane in ActiveLanes())
            {
                PointF p = Position(lane);
                bool selected = PlayerLane == lane;
                bool hovered = hoveredLane == lane && !playing && ConfirmActionEnabled;
                bool showingResult = attack != null && playing;

                Color ringColor = showingResult
                    ? (attack.Safe == lane ? CoastalTheme.SafeGreenBright : CoastalTheme.DangerCoral)
                    : (selected ? CoastalTheme.BrassBright : (hovered ? CoastalTheme.SeafoamLight : Color.FromArgb(215, 228, 240)));

                var platform = new RectangleF(p.X - 58, p.Y - 13, 116, 34);

                using (var dropBrush = new SolidBrush(Color.FromArgb(160, 4, 8, 14)))
                    g.FillEllipse(dropBrush, platform.X - 3, platform.Y + 4, platform.Width + 6, platform.Height + 2);

                using (var fill = new SolidBrush(showingResult
                    ? (attack.Safe == lane ? Color.FromArgb(240, 14, 52, 36) : Color.FromArgb(240, 56, 18, 22))
                    : Color.FromArgb(240, 14, 28, 44)))
                    g.FillEllipse(fill, platform);

                using (var outline = new Pen(CoastalTheme.WoodOutline, 7))
                    g.DrawEllipse(outline, platform);

                using (var pen = new Pen(selected && !showingResult ? CoastalTheme.BrassBright : ringColor, selected || showingResult || hovered ? 4.5f : 2.5f))
                    g.DrawEllipse(pen, platform);

                if (selected)
                {
                    var innerRing = RectangleF.Inflate(platform, -5, -3);
                    using (var goldInner = new Pen(Color.FromArgb(190, CoastalTheme.BrassGold), 1.5f) { DashStyle = DashStyle.Dot })
                        g.DrawEllipse(goldInner, innerRing);
                }

                // Nautical Key Plaque associated with the platform
                float badgeX = lane == Lane.Left ? p.X - 146 : p.X + 64;
                Rectangle badge = new Rectangle((int)badgeX, (int)(p.Y - 19), 82, 38);
                Color badgeBorder = showingResult ? ringColor : (selected ? CoastalTheme.BrassBright : (hovered ? CoastalTheme.SeafoamLight : CoastalTheme.BrassGold));
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

            int textRightLimit = r.Right - 10;
            if (ConfirmActionEnabled && !playing && !dead)
            {
                int btnW = compact ? 154 : 210;
                int btnH = Math.Max(22, r.Height - 8);
                int pressOff = confirmPressed ? 1 : 0;
                confirmButtonRect = new Rectangle(r.Right - btnW - 6, r.Y + 4 + pressOff, btnW, btnH - pressOff);
                Color cBg = confirmHovered ? Color.FromArgb(214, 142, 42) : Color.FromArgb(186, 118, 28);
                Color cBorder = confirmHovered ? CoastalTheme.BrassBright : CoastalTheme.BrassGold;
                CoastalTheme.DrawSubCard(g, confirmButtonRect, cBg, CoastalTheme.WoodOutline, cBorder);
                var btnFont = CoastalTheme.TitleFont(compact ? 8f : 9.5f, FontStyle.Bold);
                string btnLbl = compact ? "✓ CONFIRMAR" : "✓ CONFIRMAR [ENTER]";
                TextRenderer.DrawText(g, btnLbl, btnFont, confirmButtonRect, Color.FromArgb(255, 250, 235),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                textRightLimit = confirmButtonRect.X - 8;
            }
            else
            {
                confirmButtonRect = Rectangle.Empty;
            }

            Color stAccent = CoastalTheme.Seafoam;
            NauticalIcon stIcon = NauticalIcon.Compass;
            string stText = !string.IsNullOrEmpty(CustomFooterPrompt)
                ? CustomFooterPrompt
                : (centerLaneAllowed
                    ? "Escolha A (Esq.), S (Centro) ou D (Dir.) e confirme com Enter."
                    : "Escolha A (Esquerda) ou D (Direita) e confirme com Enter.");

            if (attack != null && (playing || !ConfirmActionEnabled))
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
                new Rectangle(tx, r.Y + 2, Math.Max(40, textRightLimit - tx), r.Height - 4),
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
