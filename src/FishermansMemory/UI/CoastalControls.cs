using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FishermansMemory.Core;

namespace FishermansMemory.UI
{
    public enum ButtonStyle
    {
        Primary,
        SecondaryWood,
        OceanToggle,
        Danger
    }

    public sealed class NauticalButton : Control
    {
        private bool hovered;
        private bool pressed;
        public ButtonStyle StyleVariant { get; set; } = ButtonStyle.SecondaryWood;
        public NauticalIcon Icon { get; set; } = NauticalIcon.None;
        public string SubText { get; set; } = "";

        public NauticalButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            TabStop = true;
            Size = new Size(220, 44);
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PushButton;
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            if (!Enabled) { hovered = false; pressed = false; }
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width > 8 && Height > 8)
            {
                var old = Region;
                Region = CoastalTheme.CreatePixelRegion(new Rectangle(0, 0, Width, Height), 3);
                if (old != null) old.Dispose();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (Enabled)
            {
                if (!hovered) CoastalTheme.NotifyButtonHover();
                hovered = true;
                Invalidate();
            }
        }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; pressed = false; Invalidate(); }

        protected override void OnClick(EventArgs e)
        {
            if (Enabled) CoastalTheme.NotifyButtonSelect();
            base.OnClick(e);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Enabled && e.Button == MouseButtons.Left) { Focus(); pressed = true; Invalidate(); }
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (pressed) { pressed = false; Invalidate(); }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); pressed = false; Invalidate(); }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Enter || keyData == Keys.Space) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (Enabled && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space))
            {
                pressed = true;
                Invalidate();
                e.Handled = true;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (pressed && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space))
            {
                pressed = false;
                Invalidate();
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
        }

        public void PerformClick()
        {
            if (Enabled && Visible) OnClick(EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;

            Color faceColor, topHighlight, bottomShadow, borderColor, textColor, subTextColor, iconColor;

            if (!Enabled)
            {
                faceColor = Color.FromArgb(62, 54, 48);
                topHighlight = Color.FromArgb(82, 73, 66);
                bottomShadow = Color.FromArgb(38, 33, 29);
                borderColor = Color.FromArgb(28, 22, 18);
                textColor = Color.FromArgb(196, 186, 172);
                subTextColor = Color.FromArgb(224, 176, 118);
                iconColor = Color.FromArgb(150, 140, 128);
            }
            else
            {
                switch (StyleVariant)
                {
                    case ButtonStyle.Primary:
                        faceColor = hovered ? Color.FromArgb(214, 142, 42) : Color.FromArgb(186, 118, 28);
                        topHighlight = hovered ? Color.FromArgb(255, 224, 132) : Color.FromArgb(242, 194, 92);
                        bottomShadow = Color.FromArgb(108, 62, 12);
                        borderColor = Focused || hovered ? CoastalTheme.BrassBright : CoastalTheme.WoodOutline;
                        textColor = Color.FromArgb(255, 250, 235);
                        subTextColor = Color.FromArgb(255, 232, 175);
                        iconColor = CoastalTheme.BrassBright;
                        break;
                    case ButtonStyle.OceanToggle:
                        faceColor = hovered ? CoastalTheme.OceanLight : CoastalTheme.OceanMid;
                        topHighlight = hovered ? CoastalTheme.Seafoam : CoastalTheme.OceanBorder;
                        bottomShadow = CoastalTheme.OceanDeep;
                        borderColor = Focused || hovered ? CoastalTheme.SeafoamLight : CoastalTheme.WoodOutline;
                        textColor = CoastalTheme.SandLight;
                        subTextColor = CoastalTheme.SeafoamLight;
                        iconColor = CoastalTheme.Seafoam;
                        break;
                    case ButtonStyle.Danger:
                        faceColor = hovered ? Color.FromArgb(184, 58, 52) : Color.FromArgb(148, 42, 38);
                        topHighlight = Color.FromArgb(235, 112, 102);
                        bottomShadow = Color.FromArgb(82, 18, 16);
                        borderColor = Focused || hovered ? CoastalTheme.BrassBright : CoastalTheme.WoodOutline;
                        textColor = Color.FromArgb(255, 244, 240);
                        subTextColor = Color.FromArgb(255, 205, 198);
                        iconColor = Color.FromArgb(255, 210, 190);
                        break;
                    default: // SecondaryWood
                        faceColor = hovered ? CoastalTheme.WoodLight : CoastalTheme.WoodBase;
                        topHighlight = hovered ? CoastalTheme.BrassGold : CoastalTheme.WoodHighlight;
                        bottomShadow = CoastalTheme.WoodShadow;
                        borderColor = Focused || hovered ? CoastalTheme.BrassBright : CoastalTheme.WoodOutline;
                        textColor = CoastalTheme.SandLight;
                        subTextColor = CoastalTheme.BrassBright;
                        iconColor = CoastalTheme.BrassGold;
                        break;
                }
            }

            int pressOffset = (pressed && Enabled) ? 2 : 0;
            int shadowDepth = (pressed && Enabled) ? 1 : 3;

            // Outer border / base shadow
            g.Clear(CoastalTheme.WoodOutline);

            // Face rectangle
            Rectangle face = new Rectangle(2, 2 + pressOffset, Width - 4, Height - 4 - shadowDepth);
            if (face.Width > 2 && face.Height > 2)
            {
                using (var faceBrush = new SolidBrush(faceColor))
                    g.FillRectangle(faceBrush, face);

                using (var hiPen = new Pen(topHighlight, 2))
                {
                    g.DrawLine(hiPen, face.X + 1, face.Y + 1, face.Right - 2, face.Y + 1);
                    g.DrawLine(hiPen, face.X + 1, face.Y + 1, face.X + 1, face.Bottom - 2);
                }

                using (var shPen = new Pen(bottomShadow, 2))
                {
                    g.DrawLine(shPen, face.X + 1, face.Bottom - 1, face.Right - 1, face.Bottom - 1);
                    g.DrawLine(shPen, face.Right - 1, face.Y + 2, face.Right - 1, face.Bottom - 1);
                }
            }

            // Outer pixel trim
            using (var borderPen = new Pen(borderColor, Focused ? 2f : 1f))
                g.DrawRectangle(borderPen, 1, 1, Width - 3, Height - 3);

            // Distinct keyboard focus ring (accessible without relying only on color)
            if (Focused && Enabled)
            {
                Rectangle focusRect = new Rectangle(4, 4 + pressOffset, Width - 8, Height - 8 - shadowDepth);
                using (var dashPen = new Pen(CoastalTheme.BrassBright, 1f) { DashStyle = DashStyle.Dot })
                    g.DrawRectangle(dashPen, focusRect);
            }

            // Content layout
            int leftPad = Width < 190 ? 8 : 12;
            int rightPad = Width < 190 ? 8 : 12;
            if (Icon != NauticalIcon.None)
            {
                int iconSz = Math.Min(22, Math.Max(14, Height - 16));
                RectangleF iconRect = new RectangleF(leftPad, (Height - shadowDepth - iconSz) / 2f + pressOffset, iconSz, iconSz);
                CoastalTheme.DrawNauticalIcon(g, Icon, iconRect, iconColor, textColor);
                leftPad += iconSz + (Width < 190 ? 5 : 8);
            }

            float fontSize = (Width < 190 || Height < 36) ? 9f : Height < 44 ? 10.5f : 11.5f;
            var mainFont = CoastalTheme.TitleFont(fontSize, FontStyle.Bold);
            var subFont = CoastalTheme.BodyFont(Math.Max(8f, fontSize - 2f), FontStyle.Bold);

            string displayMain = Text ?? "";
            if (Focused && Enabled)
                displayMain = "▸ " + displayMain;
            else if (hovered && Enabled)
                displayMain = "› " + displayMain;

            Rectangle textArea = new Rectangle(leftPad, pressOffset + 1, Math.Max(10, Width - leftPad - rightPad), Height - shadowDepth - 2);

            if (!string.IsNullOrEmpty(SubText))
            {
                Size mainSize = TextRenderer.MeasureText(g, displayMain, mainFont, new Size(2000, textArea.Height), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                Size subSize = TextRenderer.MeasureText(g, SubText, subFont, new Size(2000, textArea.Height), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                int pillW = subSize.Width + 12;

                // Draw SubText as a right-aligned pill badge ONLY when both main text and pill fit completely without truncating main text!
                if (mainSize.Width + pillW + 14 <= textArea.Width)
                {
                    int pillH = Math.Min(textArea.Height - 6, subSize.Height + 4);
                    Rectangle pill = new Rectangle(textArea.Right - pillW, textArea.Y + (textArea.Height - pillH) / 2, pillW, pillH);
                    using (var pillBg = new SolidBrush(Color.FromArgb(145, 16, 12, 8)))
                    using (var pillBorder = new Pen(subTextColor, 1))
                    {
                        g.FillRectangle(pillBg, pill);
                        g.DrawRectangle(pillBorder, pill.X, pill.Y, pill.Width - 1, pill.Height - 1);
                    }
                    TextRenderer.DrawText(g, SubText, subFont, pill, subTextColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

                    Rectangle mainRect = new Rectangle(textArea.X, textArea.Y, Math.Max(20, pill.X - textArea.X - 6), textArea.Height);
                    TextRenderer.DrawText(g, displayMain, mainFont, mainRect, textColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                }
                else if (Height >= 40)
                {
                    var topFont = mainSize.Width > textArea.Width ? CoastalTheme.TitleFont(Math.Max(8.5f, fontSize - 1.5f), FontStyle.Bold) : mainFont;
                    Rectangle topHalf = new Rectangle(textArea.X, textArea.Y, textArea.Width, textArea.Height * 56 / 100);
                    Rectangle botHalf = new Rectangle(textArea.X, textArea.Y + textArea.Height * 52 / 100, textArea.Width, textArea.Height * 48 / 100);
                    TextRenderer.DrawText(g, displayMain, topFont, topHalf, textColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    TextRenderer.DrawText(g, SubText, subFont, botHalf, subTextColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
                else
                {
                    var fitFont = mainSize.Width > textArea.Width ? CoastalTheme.TitleFont(Math.Max(8.5f, fontSize - 1.5f), FontStyle.Bold) : mainFont;
                    TextRenderer.DrawText(g, displayMain, fitFont, textArea, textColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
            }
            else
            {
                TextFormatFlags align = Icon != NauticalIcon.None && Width > 230
                    ? (TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix)
                    : (TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                // Drop shadow for crisp pixel readability
                if (Enabled)
                {
                    Rectangle shadowRect = new Rectangle(textArea.X + 1, textArea.Y + 1, textArea.Width, textArea.Height);
                    TextRenderer.DrawText(g, displayMain, mainFont, shadowRect, CoastalTheme.WoodOutline, align);
                }
                TextRenderer.DrawText(g, displayMain, mainFont, textArea, textColor, align);
            }
        }
    }

    // A themed wooden/parchment or wooden/ocean card with header banner, scrollable/fitted body, and pinned footer
    public sealed class CoastalCardPanel : Panel
    {
        public string HeaderTitle { get; set; } = "";
        public string HeaderSubtitle { get; set; } = "";
        public string HeaderBadge { get; set; } = "";
        public NauticalIcon HeaderIcon { get; set; } = NauticalIcon.None;
        public bool ParchmentInterior { get; set; } = true;
        public bool RuledJournalLines { get; set; } = false;
        public int HeaderHeight { get; set; } = 58;
        public int FooterHeight { get; set; } = 56;

        public CoastalCardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.ContainerControl, true);
            BackColor = CoastalTheme.WoodBase;
        }

        public Rectangle ContentBounds
        {
            get
            {
                int rim = Math.Max(14, Math.Min(18, Math.Min(Width, Height) / 22));
                int top = rim + (string.IsNullOrEmpty(HeaderTitle) ? 4 : HeaderHeight);
                int bottom = rim + FooterHeight;
                return new Rectangle(rim + 6, top, Math.Max(20, Width - (rim + 6) * 2), Math.Max(20, Height - top - bottom));
            }
        }

        public Rectangle FooterBounds
        {
            get
            {
                int rim = Math.Max(14, Math.Min(18, Math.Min(Width, Height) / 22));
                return new Rectangle(rim + 6, Math.Max(rim, Height - rim - FooterHeight + 2),
                    Math.Max(20, Width - (rim + 6) * 2), Math.Max(24, FooterHeight - 4));
            }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (Width > 12 && Height > 12)
            {
                var old = Region;
                Region = CoastalTheme.CreatePixelRegion(new Rectangle(0, 0, Width, Height), 4);
                if (old != null) old.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            CoastalTheme.DrawWoodFrame(g, new Rectangle(0, 0, Width, Height), ParchmentInterior, RuledJournalLines, true);

            int rim = Math.Max(14, Math.Min(18, Math.Min(Width, Height) / 22));

            if (!string.IsNullOrEmpty(HeaderTitle) && Height > HeaderHeight + 20)
            {
                Rectangle banner = new Rectangle(rim + 2, rim + 2, Width - (rim + 2) * 2, HeaderHeight - 6);
                CoastalTheme.DrawSubCard(g, banner, CoastalTheme.WoodShadow, CoastalTheme.WoodOutline, CoastalTheme.BrassDark);

                int leftX = banner.X + 12;
                if (HeaderIcon != NauticalIcon.None)
                {
                    int iconBox = Math.Min(34, banner.Height - 10);
                    Rectangle iconFrame = new Rectangle(leftX, banner.Y + (banner.Height - iconBox) / 2, iconBox, iconBox);
                    CoastalTheme.DrawSubCard(g, iconFrame, CoastalTheme.OceanDeep, CoastalTheme.BrassGold);
                    CoastalTheme.DrawNauticalIcon(g, HeaderIcon, Rectangle.Inflate(iconFrame, -4, -4), CoastalTheme.BrassBright, CoastalTheme.Seafoam);
                    leftX += iconBox + 10;
                }

                int rightLimit = banner.Right - 10;
                if (!string.IsNullOrEmpty(HeaderBadge))
                {
                    var badgeFont = CoastalTheme.TitleFont(banner.Height < 44 ? 8.5f : 9.5f, FontStyle.Bold);
                    Size bSz = TextRenderer.MeasureText(g, HeaderBadge, badgeFont);
                    int bw = Math.Min(banner.Width * 42 / 100, bSz.Width + 16);
                    int bh = Math.Min(banner.Height - 10, bSz.Height + 8);
                    Rectangle badgeRect = new Rectangle(banner.Right - bw - 10, banner.Y + (banner.Height - bh) / 2, bw, bh);
                    CoastalTheme.DrawSubCard(g, badgeRect, CoastalTheme.OceanDeep, CoastalTheme.BrassGold);
                    TextRenderer.DrawText(g, HeaderBadge, badgeFont, badgeRect, CoastalTheme.BrassBright,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    rightLimit = badgeRect.X - 8;
                }

                int availW = Math.Max(40, rightLimit - leftX);
                bool compact = banner.Height < 46;
                var titleFont = CoastalTheme.TitleFont(compact ? 11.5f : 14f, FontStyle.Bold);

                if (string.IsNullOrEmpty(HeaderSubtitle))
                {
                    Rectangle tRect = new Rectangle(leftX, banner.Y + 2, availW, banner.Height - 4);
                    TextRenderer.DrawText(g, HeaderTitle, titleFont, tRect, CoastalTheme.BrassBright,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
                else
                {
                    var subFont = CoastalTheme.BodyFont(compact ? 8.5f : 9.5f, FontStyle.Regular);
                    Rectangle tRect = new Rectangle(leftX, banner.Y + 3, availW, banner.Height * 55 / 100);
                    Rectangle sRect = new Rectangle(leftX, banner.Y + banner.Height * 52 / 100, availW, banner.Height * 45 / 100);
                    TextRenderer.DrawText(g, HeaderTitle, titleFont, tRect, CoastalTheme.BrassBright,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    TextRenderer.DrawText(g, HeaderSubtitle, subFont, sRect, CoastalTheme.SandMuted,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
            }

            if (FooterHeight > 0 && Height > HeaderHeight + FooterHeight + 20)
            {
                Rectangle fBounds = FooterBounds;
                using (var sepPen = new Pen(ParchmentInterior ? CoastalTheme.ParchmentBorder : CoastalTheme.OceanBorder, 1))
                    g.DrawLine(sepPen, fBounds.X, fBounds.Y - 3, fBounds.Right, fBounds.Y - 3);
            }
        }
    }

    // A themed question + text input row for Phase.Recall
    public sealed class NauticalQuestionInput : Panel
    {
        public int QuestionIndex { get; }
        public string QuestionText { get; }
        public TextBox InnerTextBox { get; }
        public bool? ValidationState { get; private set; }
        public event EventHandler EnterPressed;

        public string AnswerText
        {
            get { return InnerTextBox.Text; }
            set { InnerTextBox.Text = value; }
        }

        public NauticalQuestionInput(int index, string question)
        {
            QuestionIndex = index;
            QuestionText = question;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = CoastalTheme.ParchmentMid;
            Height = 76;

            InnerTextBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                MaxLength = 120,
                BackColor = Color.FromArgb(255, 250, 240),
                ForeColor = CoastalTheme.InkDark,
                Font = CoastalTheme.BodyFont(12f, FontStyle.Bold),
                TabStop = true,
                AccessibleName = "Resposta " + (index + 1) + ": " + question
            };

            InnerTextBox.GotFocus += (s, e) => Invalidate();
            InnerTextBox.LostFocus += (s, e) => Invalidate();
            InnerTextBox.TextChanged += (s, e) =>
            {
                if (ValidationState == false) { ValidationState = null; Invalidate(); }
            };
            InnerTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    var handler = EnterPressed;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
            };

            Controls.Add(InnerTextBox);
            LayoutInnerBox();
        }

        public void SetValidation(bool? valid)
        {
            ValidationState = valid;
            Invalidate();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            LayoutInnerBox();
        }

        private void LayoutInnerBox()
        {
            if (InnerTextBox == null) return;
            bool compact = Height < 66;
            InnerTextBox.Font = CoastalTheme.BodyFont(compact ? 10.5f : 12f, FontStyle.Bold);
            int topY = compact ? 28 : 35;
            int boxH = InnerTextBox.PreferredHeight;
            int slotY = topY + Math.Max(2, (Height - topY - 6 - boxH) / 2);
            InnerTextBox.SetBounds(16, slotY, Math.Max(60, Width - 125), boxH);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            bool focused = InnerTextBox.Focused;
            bool compact = Height < 66;

            Color cardBorder = focused ? CoastalTheme.BrassGold :
                ValidationState == false ? CoastalTheme.DangerCoral :
                ValidationState == true ? CoastalTheme.SafeGreenDark : CoastalTheme.ParchmentBorder;

            Color cardBg = focused ? Color.FromArgb(250, 240, 216) : CoastalTheme.ParchmentMid;
            CoastalTheme.DrawSubCard(g, new Rectangle(0, 0, Width, Height), cardBg, cardBorder,
                focused ? (Color?)CoastalTheme.BrassBright : null);

            // Badge "PISTA #1"
            Rectangle numBadge = new Rectangle(8, 6, 74, compact ? 18 : 22);
            CoastalTheme.DrawSubCard(g, numBadge, CoastalTheme.WoodBase, CoastalTheme.WoodOutline);
            var badgeFont = CoastalTheme.TitleFont(compact ? 8f : 9f, FontStyle.Bold);
            TextRenderer.DrawText(g, "PISTA #" + (QuestionIndex + 1), badgeFont, numBadge, CoastalTheme.BrassBright,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            // Question text
            var qFont = CoastalTheme.BodyFont(compact ? 9.5f : 11f, FontStyle.Bold);
            Rectangle qRect = new Rectangle(88, 4, Math.Max(40, Width - 96), compact ? 22 : 26);
            TextRenderer.DrawText(g, QuestionText, qFont, qRect, CoastalTheme.InkDark,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            // Input slot frame around InnerTextBox
            int slotTop = compact ? 26 : 32;
            Rectangle inputSlot = new Rectangle(8, slotTop, Math.Max(80, Width - 16), Height - slotTop - 6);
            Color slotBorder = focused ? CoastalTheme.WoodBase :
                ValidationState == false ? CoastalTheme.DangerCoral :
                ValidationState == true ? CoastalTheme.SafeGreenDark : CoastalTheme.ParchmentBorder;

            using (var slotBg = new SolidBrush(InnerTextBox.BackColor))
            using (var slotPen = new Pen(slotBorder, focused ? 2f : 1f))
            {
                g.FillRectangle(slotBg, inputSlot);
                g.DrawRectangle(slotPen, inputSlot.X, inputSlot.Y, inputSlot.Width - 1, inputSlot.Height - 1);
            }

            // Status label only when validated (keeps idle input clean and uncluttered)
            if (ValidationState.HasValue)
            {
                string statusLabel = ValidationState == false ? "✕ REVISAR" : "✓ OK";
                Color statusColor = ValidationState == false ? CoastalTheme.DangerDark : CoastalTheme.SafeGreenDark;
                Rectangle statusRect = new Rectangle(inputSlot.Right - 92, inputSlot.Y + 2, 88, inputSlot.Height - 4);
                var stFont = CoastalTheme.TitleFont(7.5f, FontStyle.Bold);
                TextRenderer.DrawText(g, statusLabel, stFont, statusRect, statusColor,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }
    }

    // Visual sequence board for Phase.Reveal showing each attack's safe position, key, arrow, and spatial mini-compass
    public sealed class SequenceBoardControl : Control
    {
        private IReadOnlyList<Lane> pattern = new Lane[0];

        public SequenceBoardControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = CoastalTheme.OceanDeep;
        }

        public void SetPattern(IReadOnlyList<Lane> sequence)
        {
            pattern = sequence ?? new Lane[0];
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(CoastalTheme.ParchmentMid);

            if (pattern == null || pattern.Count == 0) return;

            int count = pattern.Count;
            int cols = (count <= 4 && Width >= 420) ? count :
                       (count <= 6 && Width >= 560) ? count :
                       (count + 1) / 2;
            int rows = (count + cols - 1) / cols;

            int pad = 6;
            int gap = 8;
            int availW = Math.Max(40, Width - pad * 2 - gap * (cols - 1));
            int availH = Math.Max(40, Height - pad * 2 - gap * (rows - 1));
            int cardW = availW / cols;
            int cardH = availH / rows;

            bool compact = cardH < 78 || cardW < 82;

            for (int i = 0; i < count; i++)
            {
                int r = i / cols;
                int c = i % cols;
                int x = pad + c * (cardW + gap);
                int y = pad + r * (cardH + gap);
                Rectangle card = new Rectangle(x, y, cardW, cardH);

                Lane lane = pattern[i];
                CoastalTheme.DrawSubCard(g, card, CoastalTheme.OceanDeep, CoastalTheme.WoodBase, CoastalTheme.BrassGold);

                // Top order ribbon: "1º ATAQUE"
                int headerH = compact ? 18 : 22;
                Rectangle topRibbon = new Rectangle(card.X + 2, card.Y + 2, card.Width - 4, headerH);
                using (var ribBrush = new SolidBrush(CoastalTheme.WoodBase))
                    g.FillRectangle(ribBrush, topRibbon);

                var ordFont = CoastalTheme.TitleFont(compact ? 8f : 9f, FontStyle.Bold);
                TextRenderer.DrawText(g, (i + 1) + "º GOLPE", ordFont, topRibbon, CoastalTheme.BrassBright,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                // Bottom direction label: "CIMA", "ESQUERDA", etc. in calm ocean tone
                int footH = compact ? 16 : 20;
                Rectangle botBand = new Rectangle(card.X + 2, card.Bottom - footH - 2, card.Width - 4, footH);
                using (var botBrush = new SolidBrush(CoastalTheme.OceanMid))
                    g.FillRectangle(botBrush, botBand);

                var dirFont = CoastalTheme.TitleFont(compact ? 7.5f : 8.5f, FontStyle.Bold);
                TextRenderer.DrawText(g, CoastalTheme.LaneName(lane), dirFont, botBand, CoastalTheme.SandLight,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                // Middle area: Key symbol + mini spatial cross
                Rectangle mid = new Rectangle(card.X + 4, topRibbon.Bottom + 2, card.Width - 8, Math.Max(16, botBand.Top - topRibbon.Bottom - 4));
                if (mid.Width >= 90 && mid.Height >= 28)
                {
                    int crossW = Math.Min(mid.Height + 6, mid.Width * 42 / 100);
                    Rectangle crossRect = new Rectangle(mid.X + 2, mid.Y + 2, crossW, mid.Height - 4);
                    CoastalTheme.DrawLaneCrossMini(g, crossRect, lane, CoastalTheme.SafeGreen, Color.FromArgb(90, 120, 145));

                    Rectangle keyRect = new Rectangle(crossRect.Right + 2, mid.Y, mid.Right - crossRect.Right - 2, mid.Height);
                    var keyFont = CoastalTheme.TitleFont(compact ? 12f : 15f, FontStyle.Bold);
                    TextRenderer.DrawText(g, Levels.Symbol(lane), keyFont, keyRect, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
                else
                {
                    var keyFont = CoastalTheme.TitleFont(compact ? 11f : 14f, FontStyle.Bold);
                    TextRenderer.DrawText(g, Levels.Symbol(lane), keyFont, mid, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }
        }
    }

    // Visual route chart for Phase.Map showing Completed, Next, and Locked regions
    public sealed class RouteMapCanvas : Control
    {
        private int completedLevelIndex;

        public RouteMapCanvas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = CoastalTheme.ParchmentMid;
        }

        public void SetCompletedLevel(int levelIndex)
        {
            completedLevelIndex = levelIndex;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            CoastalTheme.DrawParchmentSurface(g, new Rectangle(0, 0, Width, Height), false);

            var oldSmooth = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int count = Levels.All.Count;
            int pad = 12;
            int gap = 14;
            int cardW = Math.Max(80, (Width - pad * 2 - gap * (count - 1)) / count);
            int cardH = Math.Max(80, Height - pad * 2);

            // Draw connecting sea route line across the middle of the cards
            int routeY = Height / 2;
            using (var routePen = new Pen(CoastalTheme.WoodBase, 3f) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(routePen, pad + cardW / 2, routeY, Width - pad - cardW / 2, routeY);
            }

            bool compact = cardH < 140 || cardW < 155;

            for (int i = 0; i < count; i++)
            {
                var lvl = Levels.All[i];
                bool isCompleted = i <= completedLevelIndex;
                bool isNext = i == completedLevelIndex + 1;
                bool isLocked = i > completedLevelIndex + 1;

                Rectangle card = new Rectangle(pad + i * (cardW + gap), pad, cardW, cardH);

                Color bg = isCompleted ? Color.FromArgb(228, 242, 228) :
                           isNext ? Color.FromArgb(255, 244, 212) :
                           Color.FromArgb(218, 212, 198);
                Color border = isCompleted ? CoastalTheme.SafeGreenDark :
                               isNext ? CoastalTheme.WoodBase :
                               CoastalTheme.ParchmentBorder;
                Color? hi = isNext ? (Color?)CoastalTheme.BrassGold : (isCompleted ? (Color?)CoastalTheme.SafeGreen : null);

                CoastalTheme.DrawSubCard(g, card, bg, border, hi);

                // Top status banner
                int statusH = compact ? 22 : 28;
                Rectangle statusBand = new Rectangle(card.X + 2, card.Y + 2, card.Width - 4, statusH);
                Color statusBg = isCompleted ? CoastalTheme.SafeGreenDark :
                                 isNext ? CoastalTheme.WoodBase :
                                 Color.FromArgb(98, 92, 84);
                Color statusFg = isCompleted ? CoastalTheme.SafeGreenBright :
                                 isNext ? CoastalTheme.BrassBright :
                                 Color.FromArgb(220, 214, 204);
                string statusText = isCompleted ? "✓ CONCLUÍDA" :
                                    isNext ? "→ PRÓXIMA REGIÃO" :
                                    "🔒 BLOQUEADA";

                using (var sbBrush = new SolidBrush(statusBg))
                    g.FillRectangle(sbBrush, statusBand);

                var stFont = CoastalTheme.TitleFont(compact ? 8f : 9.5f, FontStyle.Bold);
                TextRenderer.DrawText(g, statusText, stFont, statusBand, statusFg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                // Center emblem icon
                int iconSz = compact ? 28 : 42;
                RectangleF iconRect = new RectangleF(card.X + (card.Width - iconSz) / 2f, statusBand.Bottom + (compact ? 6 : 12), iconSz, iconSz);
                NauticalIcon nodeIcon = isCompleted ? NauticalIcon.ShieldCheck :
                                        isNext ? (lvl.Boss ? NauticalIcon.WaveSkull : NauticalIcon.Compass) :
                                        NauticalIcon.Lock;
                CoastalTheme.DrawNauticalIcon(g, nodeIcon, iconRect, statusBg, isNext ? CoastalTheme.BrassGold : border);

                // Region Number & Title
                int textTop = (int)iconRect.Bottom + (compact ? 4 : 8);
                var titleFont = CoastalTheme.TitleFont(compact ? 9.5f : 11.5f, FontStyle.Bold);
                string regTitle = (lvl.Boss ? "BOSS: " : (i + 1) + ". ") + lvl.Name;
                Rectangle titleRect = new Rectangle(card.X + 6, textTop, card.Width - 12, compact ? 32 : 38);
                TextRenderer.DrawText(g, regTitle, titleFont, titleRect,
                    isLocked ? CoastalTheme.InkMuted : CoastalTheme.InkDark,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

                // Region details (Enemy & attacks)
                var detailFont = CoastalTheme.BodyFont(compact ? 8f : 9.5f, FontStyle.Regular);
                string details = lvl.Enemy + "\n" + lvl.Attacks + " ataques • " + lvl.Facts.Length + (lvl.Facts.Length == 1 ? " pista" : " pistas");
                Rectangle detRect = new Rectangle(card.X + 6, titleRect.Bottom, card.Width - 12, Math.Max(20, card.Bottom - titleRect.Bottom - 4));
                TextRenderer.DrawText(g, details, detailFont, detRect,
                    isLocked ? CoastalTheme.InkMuted : CoastalTheme.InkMedium,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            }

            g.SmoothingMode = oldSmooth;
        }
    }
}
