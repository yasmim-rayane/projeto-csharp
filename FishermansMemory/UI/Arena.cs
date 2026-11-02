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
        private bool dead, playing;
        public Rectangle PlayArea { get; set; }
        public event EventHandler AttackFinished;
        private bool motionEnabled = true, presentationPaused;
        public void PausePresentation(bool paused)
        {
            presentationPaused = paused; RefreshTimer();
        }
        public bool MotionEnabled
        {
            get { return motionEnabled; }
            set { motionEnabled = value; RefreshTimer(); Invalidate(); }
        }
        private const double AttackDuration = .65;
        private void RefreshTimer()
        {
            if (!presentationPaused && Parent != null && Visible && (playing || clock.Elapsed.TotalSeconds - transition < .35 ||
                (view == ArtView.Combat && MotionEnabled && !dead))) timer.Start();
            else timer.Stop();
        }
        public Lane PlayerLane { get; set; } = Lane.Center;
        public Arena(ArtAssets assets)
        {
            art = assets; DoubleBuffered = true; ResizeRedraw = true; Size = new Size(850, 390); Margin = new Padding(0, 0, 0, 14);
            timer.Tick += (sender, args) =>
            {
                if (Visible && Parent != null) Invalidate();
                if (playing && clock.Elapsed.TotalSeconds - action >= (MotionEnabled ? AttackDuration : .12))
                {
                    playing = false;
                    var handler = AttackFinished; if (handler != null) handler(this, EventArgs.Empty);
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
                previousStage = stage; previousView = view; transition = clock.Elapsed.TotalSeconds;
                stage = index; view = mode;
                if (mode != ArtView.Combat) { attack = null; dead = false; playing = false; }
            }
            timer.Start(); Invalidate();
        }
        public void Play(AttackResult result, bool isDead)
        {
            attack = result; dead = isDead; PlayerLane = result.Chosen;
            action = clock.Elapsed.TotalSeconds; playing = true; timer.Start(); Invalidate();
        }
        public void SetDefeated(bool value) { dead = value; RefreshTimer(); Invalidate(); }
        public void ClearAttack() { attack = null; dead = false; playing = false; Invalidate(); }
        // Posições absolutas; não há ciclo de caminhada nem interpolação de deslocamento.
        private static PointF Position(Lane lane)
        {
            switch (lane)
            {
                case Lane.Left: return new PointF(190, 300);
                case Lane.Up: return new PointF(425, 245);
                case Lane.Right: return new PointF(660, 300);
                default: return new PointF(425, 350);
            }
        }
        private void Draw(Graphics g, string clip, RectangleF bounds, float opacity = 1, int frame = 0)
        {
            var bitmap = art.Frame(clip, frame); if (bitmap == null) return;
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
            string key = mode == ArtView.Menu ? "menu" : mode == ArtView.Victory ? "victory" : "scene." + level;
            if (Width <= 0 || Height <= 0) return;
            if (backgroundSize != ClientSize) { ClearBackgrounds(); backgroundSize = ClientSize; }
            Bitmap cached;
            if (!backgrounds.TryGetValue(key, out cached))
            {
                var bitmap = art.Frame(key, 0); if (bitmap == null) return;
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
            base.OnPaint(e); var g = e.Graphics; g.Clear(Color.FromArgb(9, 20, 31));
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var saved = g.Save();
            if (!art.Available)
            {
                g.DrawString("Copie Assets/Art junto ao executável.\n" + art.Error, Font, Brushes.White, 20, 20);
                g.Restore(saved); return;
            }
            float fade = MotionEnabled ? (float)Math.Min(1, (clock.Elapsed.TotalSeconds - transition) / .35) : 1;
            if (fade < 1) Background(g, previousStage, previousView, 1);
            Background(g, stage, view, fade);
            Rectangle area = PlayArea.Width > 0 ? PlayArea : ClientRectangle;
            float scale = Math.Min(area.Width / 850f, area.Height / 390f);
            g.TranslateTransform(area.X + (area.Width - 850 * scale) / 2, area.Y + (area.Height - 390 * scale) / 2);
            g.ScaleTransform(scale, scale);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            if (view == ArtView.Combat) Combat(g);
            else if (view == ArtView.Map)
            {
                using (var shade = new SolidBrush(Color.FromArgb(200, 9, 20, 31))) g.FillRectangle(shade, 20, 348, 810, 42);
                for (int i = 0; i < Levels.All.Count; i++)
                {
                    using (var brush = new SolidBrush(i <= stage ? Color.Turquoise : Color.SlateGray)) g.FillEllipse(brush, 125 + i * 260, 364, 12, 12);
                    g.DrawString(i == Levels.All.Count - 1 ? "BOSS" : "FASE " + (i + 1), Font, Brushes.White, 145 + i * 260, 357);
                }
            }
            g.Restore(saved);
        }
        private void Enemy(Graphics g, float progress)
        {
            bool strike = playing && MotionEnabled;
            double t = MotionEnabled && !dead ? clock.Elapsed.TotalSeconds : 0;
            float breath = (float)Math.Sin(t * 2.2);
            // Wind-up, impact and recovery occupy separate parts of one visual action.
            float thrust = strike ? (float)Math.Sin(Math.PI * Math.Max(0, (progress - .2f) / .8f)) : 0;
            float windup = strike && progress < .2f ? progress / .2f : 0;
            float width = stage == 2 ? 300 : 140;
            float height = stage == 2 ? 165 : 105;
            float x = 425 + (stage == 1 && MotionEnabled ? (float)Math.Sin(t * 1.6) * 6 : 0);
            float y = stage == 2 ? 95 : 105;
            y += breath * 2 + thrust * (stage == 1 ? 38 : 18) - windup * 7;
            float rotation = strike ? (stage == 1 ? -18 : stage == 0 ? 7 : 3) * thrust : breath * (stage == 0 ? 1.8f : .8f);
            var state = g.Save(); g.TranslateTransform(x, y); g.RotateTransform(rotation);
            g.ScaleTransform(1 + thrust * .10f, 1 + breath * .018f + thrust * .08f);
            int frame = MotionEnabled ? (strike ? (progress < .25f ? 0 : progress < .7f ? 1 : 0) : ((int)(t * 3) % 4 == 1 ? 1 : 0)) : 0;
            Draw(g, "enemy." + stage + (strike ? ".attack" : ".idle"),
                new RectangleF(-width / 2, -height / 2, width, height), 1, frame);
            g.Restore(state);
        }
        private void StrikeEffect(Graphics g, PointF destination, float progress)
        {
            if (!playing || progress < .20f || progress > .95f) return;
            float hit = Math.Min(1, (progress - .2f) / .45f);
            float x = destination.X, y = destination.Y - 14;
            if (stage == 0)
            {
                // Paired claw cuts converge at impact.
                using (var shadow = new Pen(Color.FromArgb(230, 35, 15, 15), 10))
                using (var cut = new Pen(Color.Orange, 5))
                {
                    float gap = (1 - hit) * 35;
                    var left = new RectangleF(x - 45 - gap, y - 45, 70, 65);
                    var right = new RectangleF(x - 25 + gap, y - 45, 70, 65);
                    g.DrawArc(shadow, left, -65, 125); g.DrawArc(shadow, right, 120, 125);
                    g.DrawArc(cut, left, -65, 125); g.DrawArc(cut, right, 120, 125);
                }
            }
            else if (stage == 1)
            {
                // The shark leaves three slanting water cuts.
                using (var trail = new Pen(Color.FromArgb(220, 47, 202, 240), 9))
                using (var edge = new Pen(Color.White, 3))
                    for (int i = -1; i <= 1; i++)
                    {
                        float offset = i * 17;
                        g.DrawLine(trail, x - 35 + offset, y - 45, x + 25 + offset, y + 12);
                        g.DrawLine(edge, x - 35 + offset, y - 45, x + 25 + offset, y + 12);
                    }
            }
            else
            {
                // Tentacle whip extends from the boss to the attacked platform.
                var tip = new PointF(425 + (x - 425) * hit, 155 + (y - 155) * hit);
                using (var outline = new Pen(Color.FromArgb(8, 12, 28), 19))
                using (var tentacle = new Pen(Color.MediumSlateBlue, 11))
                {
                    g.DrawBezier(outline, new PointF(425, 140), new PointF(x - 110, 165), new PointF(x + 65, y - 65), tip);
                    g.DrawBezier(tentacle, new PointF(425, 140), new PointF(x - 110, 165), new PointF(x + 65, y - 65), tip);
                }
            }
            if (hit > .65f) Draw(g, "splash", new RectangleF(x - 32, y - 24, 64, 54));
        }
        private void Combat(Graphics g)
        {
            double elapsed = clock.Elapsed.TotalSeconds - action;
            float progress = MotionEnabled ? (float)Math.Min(1, elapsed / AttackDuration) : .65f;
            Enemy(g, progress);
            using (var floor = new SolidBrush(Color.FromArgb(105, 6, 14, 26))) g.FillRectangle(floor, 0, 205, 850, 185);
            using (var keyFont = new Font("Consolas", 19, FontStyle.Bold))
            using (var stateFont = new Font("Segoe UI", 9, FontStyle.Bold))
            foreach (Lane lane in Levels.Lanes(Levels.All[stage]))
            {
                PointF p = Position(lane);
                bool selected = PlayerLane == lane;
                bool showingResult = attack != null && playing;
                Color c = showingResult ? (attack.Safe == lane ? Color.FromArgb(86, 255, 188) : Color.FromArgb(255, 113, 102)) : Color.WhiteSmoke;
                var platform = new RectangleF(p.X - 58, p.Y - 12, 116, 33);
                using (var fill = new SolidBrush(Color.FromArgb(245, 12, 24, 39))) g.FillEllipse(fill, platform);
                using (var outline = new Pen(Color.FromArgb(5, 10, 18), 9)) g.DrawEllipse(outline, platform);
                using (var pen = new Pen(selected ? Color.Gold : c, selected ? 5 : 3)) g.DrawEllipse(pen, platform);
                var badge = new RectangleF(p.X + 66, p.Y - 14, 77, 35);
                g.FillRectangle(Brushes.Black, badge);
                using (var pen = new Pen(c, 2)) g.DrawRectangle(pen, badge.X, badge.Y, badge.Width, badge.Height);
                g.DrawString(Levels.Symbol(lane), keyFont, Brushes.White, badge.X + 4, badge.Y + 2);
                if (showingResult)
                {
                    string label = attack.Safe == lane ? "SEGURA" : "ATAQUE";
                    var tag = new RectangleF(p.X - 38, p.Y + 23, 76, 15);
                    g.FillRectangle(Brushes.Black, tag);
                    using (var brush = new SolidBrush(c)) g.DrawString(label, stateFont, brush, tag.X + 6, tag.Y);
                }
                if (showingResult && attack.Safe != lane) StrikeEffect(g, p, progress);
            }
            var pos = Position(PlayerLane);
            string pose = dead ? "hero.dead" : attack != null && !attack.Correct && playing ? "hero.hurt" : "hero.idle";
            Draw(g, pose, new RectangleF(pos.X - 25, pos.Y - 67, 50, 68));
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Stop(); timer.Dispose(); clock.Stop(); ClearBackgrounds(); }
            base.Dispose(disposing);
        }
    }
}
