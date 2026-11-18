using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FishermansMemory.Core;

namespace FishermansMemory.UI
{
    // Compact Nautical Expedition Top Bar HUD: HP, Score, Combo, Stage/Attack Progress, and On-Demand Menu Button.
    public sealed class GameHud : Control
    {
        private int hp = 100, score, combo;
        private string stageTitle = "FASE 1 • PRAIA DAS CONCHAS";
        private string enemySubtitle = "Caranguejo sentinela";
        private int resolvedAttacks, totalAttacks = 4;
        private readonly List<bool> attackOutcomes = new List<bool>();
        private Rectangle menuButtonRect;
        private bool menuHovered, menuPressed;

        public event EventHandler MenuRequested;

        public GameHud()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = CoastalTheme.WoodBase;
            AccessibleRole = AccessibleRole.StatusBar;
        }

        public void UpdateValues(int health, int points, int streak)
        {
            hp = health;
            score = points;
            combo = streak;
            AccessibleName = "Vida " + hp + " de 100. Pontos " + score + ". Combo " + combo + " de 3.";
            Invalidate();
        }

        public void UpdateSession(GameSession session)
        {
            if (session == null) return;
            hp = session.Hp;
            score = session.Score;
            combo = session.Combo;
            stageTitle = (session.Level.Boss ? "BOSS • " : "FASE " + (session.LevelIndex + 1) + " • ") + session.Level.Name.ToUpperInvariant();
            enemySubtitle = session.Level.Enemy;
            resolvedAttacks = session.Resolved;
            totalAttacks = session.Level.Attacks;

            attackOutcomes.Clear();
            int count = Math.Min(session.Input.Count, session.Pattern.Count);
            for (int i = 0; i < count; i++)
                attackOutcomes.Add(session.Input[i] == session.Pattern[i]);

            AccessibleName = stageTitle + ". Vida " + hp + " de 100. Pontos " + score +
                             ". Combo " + combo + " de 3. Ataques " + resolvedAttacks + " de " + totalAttacks + ".";
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width > 12 && Height > 12)
            {
                var old = Region;
                Region = CoastalTheme.CreatePixelRegion(new Rectangle(0, 0, Width, Height), 3);
                if (old != null) old.Dispose();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool over = menuButtonRect.Contains(e.Location);
            if (over != menuHovered)
            {
                menuHovered = over;
                if (menuHovered) CoastalTheme.NotifyButtonHover();
                Cursor = menuHovered ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (menuHovered || menuPressed)
            {
                menuHovered = false;
                menuPressed = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && menuButtonRect.Contains(e.Location))
            {
                menuPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (menuPressed)
            {
                bool trigger = menuButtonRect.Contains(e.Location);
                menuPressed = false;
                Invalidate();
                if (trigger)
                {
                    CoastalTheme.NotifyButtonSelect();
                    var handler = MenuRequested;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;

            // Outer wooden HUD bar frame
            CoastalTheme.DrawPixelBox(g, new Rectangle(0, 0, Width, Height),
                CoastalTheme.WoodBase, CoastalTheme.WoodOutline, CoastalTheme.WoodHighlight, 3);

            int pad = 6;
            int innerH = Math.Max(24, Height - pad * 2);
            bool compact = Width < 780 || Height < 54;

            // 1. Rightmost On-Demand Menu Button
            int menuW = compact ? 78 : 112;
            menuButtonRect = new Rectangle(Width - pad - menuW, pad, menuW, innerH);
            DrawMenuButton(g, menuButtonRect, compact);

            int availW = Math.Max(100, menuButtonRect.X - pad - 6);

            // Layout sections proportionally across availW:
            // [Stage & Attack Progress ~30%] [HP Bar ~34%] [Score ~18%] [Combo ~18%]
            int stageW = availW * 31 / 100;
            int hpW = availW * 33 / 100;
            int scoreW = availW * 18 / 100;
            int comboW = Math.Max(60, availW - stageW - hpW - scoreW - 18);

            Rectangle stageBox = new Rectangle(pad, pad, stageW, innerH);
            Rectangle hpBox = new Rectangle(stageBox.Right + 6, pad, hpW, innerH);
            Rectangle scoreBox = new Rectangle(hpBox.Right + 6, pad, scoreW, innerH);
            Rectangle comboBox = new Rectangle(scoreBox.Right + 6, pad, comboW, innerH);

            DrawStageAndAttacksBox(g, stageBox, compact);
            DrawHpBox(g, hpBox, compact);
            DrawScoreBox(g, scoreBox, compact);
            DrawComboBox(g, comboBox, compact);
        }

        private void DrawStageAndAttacksBox(Graphics g, Rectangle r, bool compact)
        {
            CoastalTheme.DrawSubCard(g, r, CoastalTheme.OceanDeep, CoastalTheme.WoodOutline, CoastalTheme.BrassDark);

            var titleFont = CoastalTheme.TitleFont(compact ? 8f : 9.5f, FontStyle.Bold);
            var subFont = CoastalTheme.BodyFont(compact ? 7.5f : 8.5f, FontStyle.Bold);

            Rectangle topRow = new Rectangle(r.X + 8, r.Y + 3, r.Width - 16, r.Height / 2 - 2);
            TextRenderer.DrawText(g, stageTitle, titleFont, topRow, CoastalTheme.BrassBright,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            // Bottom row: Attack counter + visual beads
            Rectangle botRow = new Rectangle(r.X + 8, r.Y + r.Height / 2, r.Width - 16, r.Height / 2 - 3);
            string atkText = compact
                ? ("ATK " + resolvedAttacks + "/" + totalAttacks)
                : ("ATAQUES " + resolvedAttacks + "/" + totalAttacks);

            Size txtSz = TextRenderer.MeasureText(g, atkText, subFont);
            TextRenderer.DrawText(g, atkText, subFont,
                new Rectangle(botRow.X, botRow.Y, Math.Min(botRow.Width, txtSz.Width + 4), botRow.Height),
                CoastalTheme.SeafoamLight,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            // Draw individual attack beads (✓ hit, ✕ miss, • pending)
            int beadsStartX = botRow.X + txtSz.Width + 6;
            int availBeadsW = botRow.Right - beadsStartX;
            if (totalAttacks > 0 && availBeadsW >= totalAttacks * 10)
            {
                int beadSz = Math.Min(14, Math.Max(9, (availBeadsW - (totalAttacks - 1) * 3) / totalAttacks));
                int by = botRow.Y + (botRow.Height - beadSz) / 2;
                for (int i = 0; i < totalAttacks; i++)
                {
                    int bx = beadsStartX + i * (beadSz + 3);
                    Rectangle bead = new Rectangle(bx, by, beadSz, beadSz);
                    bool resolved = i < attackOutcomes.Count;
                    bool hit = resolved && attackOutcomes[i];

                    Color fill = !resolved ? CoastalTheme.OceanMid :
                                 hit ? CoastalTheme.SafeGreenDark : CoastalTheme.DangerDark;
                    Color border = !resolved ? CoastalTheme.OceanBorder :
                                   hit ? CoastalTheme.SafeGreen : CoastalTheme.DangerCoral;

                    using (var bFill = new SolidBrush(fill))
                    using (var bPen = new Pen(border, 1))
                    {
                        g.FillRectangle(bFill, bead);
                        g.DrawRectangle(bPen, bead.X, bead.Y, bead.Width - 1, bead.Height - 1);
                    }

                    if (resolved && beadSz >= 10)
                    {
                        using (var symPen = new Pen(Color.White, 1.5f))
                        {
                            if (hit)
                            {
                                g.DrawLine(symPen, bead.X + 2, bead.Y + bead.Height / 2, bead.X + bead.Width / 2, bead.Bottom - 3);
                                g.DrawLine(symPen, bead.X + bead.Width / 2, bead.Bottom - 3, bead.Right - 2, bead.Y + 2);
                            }
                            else
                            {
                                g.DrawLine(symPen, bead.X + 2, bead.Y + 2, bead.Right - 3, bead.Bottom - 3);
                                g.DrawLine(symPen, bead.Right - 3, bead.Y + 2, bead.X + 2, bead.Bottom - 3);
                            }
                        }
                    }
                }
            }
        }

        private void DrawHpBox(Graphics g, Rectangle r, bool compact)
        {
            CoastalTheme.DrawSubCard(g, r, CoastalTheme.OceanDeep, CoastalTheme.WoodOutline, CoastalTheme.BrassDark);

            int iconSz = compact ? 18 : 26;
            RectangleF heartRect = new RectangleF(r.X + 7, r.Y + (r.Height - iconSz) / 2f, iconSz, iconSz);
            bool lowHp = hp <= 30;
            CoastalTheme.DrawNauticalIcon(g, NauticalIcon.Heart, heartRect,
                lowHp ? CoastalTheme.DangerBright : Color.FromArgb(236, 56, 68),
                Color.MistyRose);

            int barX = (int)heartRect.Right + 6;
            int barW = Math.Max(40, r.Right - barX - 8);

            var labelFont = CoastalTheme.TitleFont(compact ? 8f : 9.5f, FontStyle.Bold);
            string hpText = "VIDA " + hp + "/100" + (lowHp ? " [!]" : "");
            Rectangle topText = new Rectangle(barX, r.Y + 3, barW, r.Height / 2 - 2);
            TextRenderer.DrawText(g, hpText, labelFont, topText,
                lowHp ? CoastalTheme.DangerBright : Color.White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            // 20-segment health bar
            int segY = r.Y + r.Height / 2 + 1;
            int segH = Math.Max(8, r.Bottom - segY - 5);
            int step = Math.Max(3, barW / 20);
            int cellW = Math.Max(2, step - 2);

            for (int i = 0; i < 20; i++)
            {
                Rectangle cell = new Rectangle(barX + i * step, segY, cellW, segH);
                using (var emptyBrush = new SolidBrush(Color.FromArgb(42, 54, 68)))
                    g.FillRectangle(emptyBrush, cell);

                int fill = Math.Max(0, Math.Min(5, hp - i * 5));
                if (fill > 0)
                {
                    Color hpCol = lowHp ? Color.OrangeRed : Color.FromArgb(235, 58, 70);
                    int fw = Math.Max(1, cell.Width * fill / 5);
                    using (var fillBrush = new SolidBrush(hpCol))
                        g.FillRectangle(fillBrush, cell.X, cell.Y, fw, cell.Height);
                }
            }
        }

        private void DrawScoreBox(Graphics g, Rectangle r, bool compact)
        {
            CoastalTheme.DrawSubCard(g, r, CoastalTheme.OceanDeep, CoastalTheme.WoodOutline, CoastalTheme.BrassDark);

            int iconSz = compact ? 16 : 24;
            RectangleF starRect = new RectangleF(r.X + 6, r.Y + (r.Height - iconSz) / 2f, iconSz, iconSz);
            CoastalTheme.DrawNauticalIcon(g, NauticalIcon.Compass, starRect, CoastalTheme.BrassBright, CoastalTheme.BrassGold);

            int tx = (int)starRect.Right + 4;
            int tw = Math.Max(20, r.Right - tx - 4);
            var lblFont = CoastalTheme.TitleFont(compact ? 7.5f : 8.5f, FontStyle.Bold);
            var valFont = CoastalTheme.TitleFont(compact ? 10.5f : 13.5f, FontStyle.Bold);

            TextRenderer.DrawText(g, "PONTOS", lblFont, new Rectangle(tx, r.Y + 3, tw, r.Height * 42 / 100),
                CoastalTheme.BrassGold, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, score.ToString(), valFont, new Rectangle(tx, r.Y + r.Height * 42 / 100, tw, r.Height * 55 / 100),
                Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        private void DrawComboBox(Graphics g, Rectangle r, bool compact)
        {
            CoastalTheme.DrawSubCard(g, r, CoastalTheme.OceanDeep, CoastalTheme.WoodOutline, CoastalTheme.BrassDark);

            var lblFont = CoastalTheme.TitleFont(compact ? 7.5f : 8.5f, FontStyle.Bold);
            var valFont = CoastalTheme.TitleFont(compact ? 10f : 12.5f, FontStyle.Bold);

            Rectangle top = new Rectangle(r.X + 6, r.Y + 3, r.Width - 12, r.Height * 42 / 100);
            TextRenderer.DrawText(g, "COMBO", lblFont, top, CoastalTheme.ComboCyan,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            Rectangle bot = new Rectangle(r.X + 6, r.Y + r.Height * 42 / 100, r.Width - 12, r.Height * 55 / 100);
            TextRenderer.DrawText(g, combo + "/3", valFont, bot, Color.White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            // 3 Combo Pearls on the right side of the Combo box
            int pearlSz = compact ? 9 : 12;
            int totalPearlsW = pearlSz * 3 + 6;
            if (r.Width >= totalPearlsW + 48)
            {
                int startX = r.Right - totalPearlsW - 6;
                int py = bot.Y + (bot.Height - pearlSz) / 2;
                var oldSmooth = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int i = 0; i < 3; i++)
                {
                    bool lit = i < combo;
                    Rectangle pr = new Rectangle(startX + i * (pearlSz + 3), py, pearlSz, pearlSz);
                    using (var fill = new SolidBrush(lit ? CoastalTheme.ComboCyan : Color.FromArgb(35, 55, 72)))
                    using (var pen = new Pen(lit ? Color.White : CoastalTheme.OceanBorder, 1.2f))
                    {
                        g.FillEllipse(fill, pr);
                        g.DrawEllipse(pen, pr);
                    }
                }
                g.SmoothingMode = oldSmooth;
            }
        }

        private void DrawMenuButton(Graphics g, Rectangle r, bool compact)
        {
            int press = menuPressed ? 1 : 0;
            Color bg = menuHovered ? CoastalTheme.WoodLight : CoastalTheme.WoodShadow;
            Color border = menuHovered ? CoastalTheme.BrassBright : CoastalTheme.BrassGold;

            Rectangle btn = new Rectangle(r.X, r.Y + press, r.Width, r.Height - press);
            CoastalTheme.DrawSubCard(g, btn, bg, CoastalTheme.WoodOutline, border);

            int iconSz = compact ? 14 : 18;
            RectangleF iconRect = new RectangleF(btn.X + 8, btn.Y + (btn.Height - iconSz) / 2f, iconSz, iconSz);
            CoastalTheme.DrawNauticalIcon(g, NauticalIcon.Helm, iconRect, CoastalTheme.BrassBright, CoastalTheme.SandLight);

            var font = CoastalTheme.TitleFont(compact ? 8.5f : 9.5f, FontStyle.Bold);
            Rectangle txtRect = new Rectangle((int)iconRect.Right + 2, btn.Y, btn.Right - (int)iconRect.Right - 4, btn.Height);
            TextRenderer.DrawText(g, "MENU", font, txtRect, CoastalTheme.SandLight,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}
