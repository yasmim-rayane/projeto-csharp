using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Text.Json;
using FishermansMemory.Core;
using FishermansMemory.Infrastructure;

namespace FishermansMemory.UI
{
public sealed class MainForm : Form
{
    private readonly SaveService saves = new SaveService();
    private readonly AudioService audio = new AudioService();
    private readonly ArtAssets art = new ArtAssets();
    private readonly Arena arena;
    private readonly GameHud hud = new GameHud { Visible = false };
    private readonly FlowLayoutPanel body = new FlowLayoutPanel()
    { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(20) };
    private bool fullScreen, arranging;
    private Rectangle windowBounds;
    private FormWindowState windowState;
    private GameSession game;
    private bool menu = true;
    private string feedback = "";
    private AttackResult last;
    private bool attackPresentation;

    public MainForm()
    {
        arena = new Arena(art);
        arena.AttackFinished += (sender, args) =>
        {
            if (!attackPresentation) return;
            attackPresentation = false;
            if (!menu) Render();
        };
        Text = "Fisherman's Memory"; ClientSize = new Size(1280, 800); MinimumSize = new Size(640, 480);
        AutoScaleMode = AutoScaleMode.Dpi; DoubleBuffered = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(16, 29, 47); ForeColor = Color.FromArgb(232, 240, 237);
        Font = new Font("Segoe UI", 11);
        arena.Dock = DockStyle.Fill; Controls.Add(arena);
        body.BackColor = Color.FromArgb(16, 29, 47);
        arena.Controls.Add(body);
        arena.Controls.Add(hud);
        Resize += (sender, args) => { arena.PausePresentation(WindowState == FormWindowState.Minimized); Arrange(); };
        body.ControlAdded += (sender, args) => Arrange();
        body.Layout += (sender, args) => Arrange();
        FormClosing += (sender, args) => { if (game != null) Persist(); };
        FormClosed += (sender, args) => { arena.Dispose(); art.Dispose(); audio.Dispose(); };
        ShowMenu();
    }
    private void Clear(string title)
    {
        body.SuspendLayout();
        while (body.Controls.Count > 0) body.Controls[0].Dispose();
        Label(title, 25, Color.FromArgb(102, 221, 198));
        body.ResumeLayout();
    }
    private void Label(string text, float size = 11, Color? color = null)
    {
        body.Controls.Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(850, 0),
            ForeColor = color ?? ForeColor, Font = new Font(Font.FontFamily, size), Margin = new Padding(0, 0, 0, 14) });
    }
    private void Info(string title, string text, bool note)
    {
        // Notes use a quieter slate card; actionable tips use teal.
        var label = new Label { AutoSize = true, MaximumSize = new Size(850, 0),
            Text = title + "\n" + text, Padding = new Padding(12),
            Margin = new Padding(0, note ? 12 : 0, 0, 14), BorderStyle = BorderStyle.FixedSingle,
            BackColor = note ? Color.FromArgb(37, 42, 54) : Color.FromArgb(20, 62, 72),
            ForeColor = note ? Color.FromArgb(200, 207, 220) : Color.FromArgb(218, 255, 245),
            Font = new Font(Font.FontFamily, note ? 10 : 11) };
        body.Controls.Add(label);
    }
    private Button Button(string text, Action click, bool enabled = true)
    {
        var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(190, 38),
            BackColor = Color.FromArgb(35, 72, 91), ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Padding = new Padding(10, 3, 10, 3), Margin = new Padding(0, 0, 8, 8), Enabled = enabled };
        b.Click += (sender, args) => click(); body.Controls.Add(b); return b;
    }
    private void ShowMenu()
    {
        hud.Visible = false;
        menu = true; attackPresentation = false; arena.ClearAttack(); audio.Stop(); Clear("FISHERMAN'S MEMORY");
        Label("Uma ilha guarda suas histórias. O mar tenta apagá-las.", 15);
        ShowArt(0, ArtView.Menu, false);
        Button("Nova aventura", () =>
        {
            if (saves.Exists && MessageBox.Show(this, "Substituir o progresso salvo?", "Nova aventura",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            game = new GameSession(); arena.PlayerLane = Lane.Center; arena.ClearAttack(); last = null; feedback = ""; Persist(); Introduction();
        });
        Button("Continuar", () =>
        {
            try { game = saves.Load(); arena.PlayerLane = game.Input.Count > 0 ? game.Input[game.Input.Count - 1] : Lane.Center; arena.ClearAttack(); last = null; feedback = ""; menu = false; audio.Music(); Render(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException || e is NotSupportedException)
            { MessageBox.Show(this, "Não foi possível carregar o progresso.\n" + e.Message); }
        }, saves.Exists);
        Button("Tutorial", () => Tutorial(false));
        Button(audio.Enabled ? "Som: ligado" : "Som: desligado", () => { audio.Enabled = !audio.Enabled; ShowMenu(); });
        Button(arena.MotionEnabled ? "Animações: ligadas" : "Animações: desligadas", () => { arena.MotionEnabled = !arena.MotionEnabled; ShowMenu(); });
        Button("Tela cheia / janela • F11", ToggleFullScreen);
        Button("Sair", Close);
    }
    private void Introduction()
    {
        hud.Visible = false; menu = true; Clear("A ilha que esqueceu seu nome");
        ShowArt(0, ArtView.Exploration, false);
        Label("Você é um desbravador que desembarca numa ilha litorânea. O farol se apagou, os caminhos mudaram e os habitantes perderam suas histórias.\n\nSiga as pistas da praia até o coração da ilha. Observe os guardiões, memorize seus movimentos e recupere as informações que revelam a passagem. O Leviatã do Esquecimento espera no fim da jornada.", 15);
        Button("Aprender a jogar", () => Tutorial(true));
    }
    private void Tutorial(bool start)
    {
        hud.Visible = false; menu = true; Clear("Tutorial • observe, guarde, recupere");
        ShowArt(0, ArtView.Exploration, false);
        Label("1. Memorize as informações da fase.\n2. Observe a sequência de posições seguras: W = cima, A = esquerda, S = baixo e D = direita.\n3. Clique em Iniciar combate para ocultar o padrão.\n4. Pressione uma tecla por ataque: a posição muda imediatamente e o ataque é validado na hora. Não há confirmação ou desfazer.\n5. Ao terminar, recupere as informações iniciais.", 13);
        Label("São três fases, com 4, 6 e 8 ataques e 1, 2 e 3 informações. Não há cronômetro: pense antes de cada tecla. Solte a tecla para registrar um novo pressionamento.\n\nHP: começa em 100; erro causa 20 de dano ou 30 no Boss. Três acertos consecutivos curam 50 HP (máximo 100). Pontos: +100/acerto, +150/combo, −50/erro, +500/memória e +5 por HP na conclusão da fase.");
        Button(start ? "Desembarcar na ilha" : "Voltar ao menu", () =>
        { if (start) { menu = false; audio.Music(); Render(); } else ShowMenu(); });
    }
    private void Act(Action action)
    {
        feedback = ""; action(); Persist(); Render();
    }
    private void Persist()
    {
        if (game == null) return;
        try { saves.Save(game); }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        { MessageBox.Show(this, "A partida continua, mas o progresso não pôde ser salvo.\n" + e.Message); }
    }
    private void Render()
    {
        if (game == null) return;
        menu = false; var g = game;
        Clear($"{(g.Level.Boss ? "BOSS" : $"FASE {g.LevelIndex + 1}")} • {g.Level.Name}");
        hud.Visible = true; hud.UpdateValues(g.Hp, g.Score, g.Combo); Arrange();
        if (g.Phase == Phase.Input || attackPresentation)
        {
            Label($"Ataques: {g.Resolved}/{g.Level.Attacks} • {g.Level.Enemy}", 16);
            ShowArenaAndFeedback(g);
            Info("CONTROLES", "W: cima • A: esquerda • S: baixo • D: direita. Uma tecla por ataque.", false);
            Info("NOTA", "Cada movimento é validado imediatamente. Não há limite de tempo para pensar.", true);
            Button("Salvar e voltar ao menu", () => { Persist(); ShowMenu(); });
            return;
        }
        switch (g.Phase)
        {
            case Phase.Information:
                last = null; arena.ClearAttack();
                ShowArt(g.LevelIndex, ArtView.Exploration, g.Level.Center);
                Label("Diário da expedição", 18);
                Label(g.Level.Passage, 15);
                Info("NOTA", "Leia o texto inteiro: os detalhes serão perguntados após o combate. O diário ficará oculto até a próxima fase.", true);
                Button("Memorizei • observar o inimigo", () => Act(g.Reveal)); break;
            case Phase.Reveal:
                Label($"{g.Level.Enemy} revela {g.Level.Attacks} ataques.", 17);
                Info("DICA", "Cada símbolo indica a única posição SEGURA daquele ataque. Memorize a ordem antes de começar.", false);
                Label(string.Join("    ", g.Pattern.Select(Levels.Symbol)), 22);
                arena.ClearAttack(); ShowArt(g.LevelIndex, ArtView.Combat, g.Level.Center);
                Button("Iniciar combate", () => Act(g.HidePattern)); break;
            case Phase.Recall:
                ShowArenaAndFeedback(g);
                Label("Recupere a informação inicial", 18);
                var answers = new List<TextBox>();
                foreach (var f in g.Level.Facts)
                {
                    Label(f.Question); var box = new TextBox { Width = 600, MaxLength = 120 };
                    answers.Add(box); body.Controls.Add(box);
                }
                var error = new Label { AutoSize = true, ForeColor = Color.Salmon, MaximumSize = new Size(850, 0) };
                Button("Validar memória", () =>
                {
                    if (g.Recall(answers.Select(x => x.Text).ToArray())) { Persist(); Render(); }
                    else error.Text = "Ainda não. Responda apenas com a palavra-chave de cada informação e tente novamente.";
                });
                body.Controls.Add(error); break;
            case Phase.Results:
                ShowArt(g.LevelIndex, ArtView.Exploration, g.Level.Center);
                Label("Região libertada!", 22);
                Label($"Esquivas corretas: {g.Correct} • Erros: {g.Errors}\nMemória recuperada: +500\nHP restante: {g.Hp} × 5 = +{g.Hp * 5}\nSaldo desta fase: {g.StageScore}\nPontuação acumulada: {g.Score}", 16);
                Button(g.Level.Boss ? "Revelar o destino da ilha" : "Abrir mapa", () => Act(g.LeaveResults)); break;
            case Phase.Map:
                ShowArt(g.LevelIndex + 1, ArtView.Map, g.Level.Center);
                Label("Rota da expedição", 20);
                for (int i = 0; i < Levels.All.Count; i++)
                    Label($"{(i <= g.LevelIndex ? "✓" : i == g.LevelIndex + 1 ? "→" : "·")} {i + 1}. {Levels.All[i].Name}", 15);
                Button("Seguir para a próxima região", () => Act(g.NextLevel)); break;
            case Phase.GameOver:
                ShowArenaAndFeedback(g);
                Label("O mar levou suas forças, mas a jornada pode recomeçar.", 18);
                Label($"Fim da expedição • {g.Score} pontos. Uma nova aventura começa com 100 HP."); break;
            case Phase.Victory:
                ShowArt(0, ArtView.Victory, false);
                Label("Aurora voltou a lembrar", 25);
                Label("O Leviatã recua. Você devolve a pérola ao farol e a ilha recupera seu nome. As histórias voltam a atravessar o mar.", 17);
                Label($"Expedição concluída • {g.Score} pontos • {g.Hp} HP", 19); break;
        }
        Button("Salvar e voltar ao menu", () => { Persist(); ShowMenu(); });
    }
    private void ShowArt(int stage, ArtView view, bool center)
    {
        arena.Configure(stage, view, center);
        arena.Invalidate();
    }
    private void ShowArenaAndFeedback(GameSession g)
    {
        ShowArt(g.LevelIndex, ArtView.Combat, g.Level.Center);
        arena.SetDefeated(g.Hp == 0);
        if (feedback.Length > 0) Info("ÚLTIMA AÇÃO", feedback, false);
    }
    private void MoveAndAttack(Lane lane)
    {
        if (game == null || game.Phase != Phase.Input) return;
        audio.Music(); last = game.SubmitMove(lane);
        attackPresentation = true;
        arena.Play(last, game.Hp == 0);
        feedback = last.Correct ? $"Esquiva correta! +100 pontos.{(last.Combo ? $" Combo! +150 pontos e +{last.Healing} HP." : "")}" :
            $"Posição incorreta: −{last.Damage} HP e −50 pontos. Posição segura: {Levels.Symbol(last.Safe)}.";
        if (!last.Correct) audio.Damage();
        Persist(); Render();
    }
    private void Arrange()
    {
        if (arranging || ClientSize.Width < 1 || ClientSize.Height < 1) return;
        arranging = true;
        try
        {
            float dpi = DeviceDpi / 96f;
            bool wide = ClientSize.Width >= 1000 * dpi;
            int panelWidth = wide ? Math.Min((int)(440 * dpi), ClientSize.Width * 38 / 100) : ClientSize.Width;
            int panelHeight = wide ? ClientSize.Height : ClientSize.Height * 42 / 100;
            body.SetBounds(0, wide ? 0 : ClientSize.Height - panelHeight, panelWidth, panelHeight);
            int available = Math.Max(100, panelWidth - body.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 12);
            foreach (Control c in body.Controls)
            {
                c.MaximumSize = new Size(available, 0);
                if (c is TextBox || c is Button) c.Width = available;
                if (c is Button) { c.AutoSize = true; c.MinimumSize = new Size(available, (int)(40 * dpi)); }
            }
            Rectangle free = wide ? new Rectangle(panelWidth, 0, ClientSize.Width - panelWidth, ClientSize.Height)
                : new Rectangle(0, 0, ClientSize.Width, ClientSize.Height - panelHeight);
            int hudHeight = hud.Visible ? Math.Min((int)(90 * dpi), (int)(free.Width * 84f / 850) + 8) : 0;
            hud.SetBounds(free.X + 6, free.Y + 4, Math.Max(1, free.Width - 12), Math.Max(1, hudHeight - 8));
            arena.PlayArea = new Rectangle(free.X, free.Y + hudHeight, free.Width, Math.Max(1, free.Height - hudHeight));
            arena.Invalidate();
        }
        finally { arranging = false; }
    }
    private void ToggleFullScreen()
    {
        SuspendLayout();
        if (!fullScreen)
        {
            windowState = WindowState;
            windowBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var monitor = Screen.FromControl(this);
            WindowState = FormWindowState.Normal; FormBorderStyle = FormBorderStyle.None;
            Bounds = monitor.Bounds; fullScreen = true;
        }
        else
        {
            fullScreen = false; FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Normal; Bounds = windowBounds; WindowState = windowState;
        }
        ResumeLayout(); Arrange();
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F11 || keyData == (Keys.Alt | Keys.Enter))
        {
            if ((msg.LParam.ToInt64() & (1L << 30)) == 0) ToggleFullScreen();
            return true;
        }
        if (keyData == Keys.Escape && fullScreen) { ToggleFullScreen(); return true; }
        if (!menu && (game?.Phase == Phase.Input || attackPresentation))
        {
            Lane? lane = null;
            switch (keyData)
            {
                case Keys.A: case Keys.Left: lane = Lane.Left; break;
                case Keys.D: case Keys.Right: lane = Lane.Right; break;
                case Keys.S: case Keys.Down: lane = Lane.Center; break;
                case Keys.W: case Keys.Up: lane = Lane.Up; break;
            }
            if (lane.HasValue)
            {
                // Bit 30 = tecla já estava pressionada: ignora autorrepetição do Windows.
                bool repeated = (msg.LParam.ToInt64() & (1L << 30)) != 0;
                if (!repeated) MoveAndAttack(lane.Value);
                return true;
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}

}
