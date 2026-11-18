using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;
using FishermansMemory.Core;
using FishermansMemory.Infrastructure;

namespace FishermansMemory.UI
{
    public sealed class MainForm : Form
    {
        private enum ScreenMode
        {
            MainMenu,
            Introduction,
            Tutorial,
            Campaign
        }

        private readonly SaveService saves = new SaveService();
        private readonly AudioService audio = new AudioService();
        private readonly ArtAssets art = new ArtAssets();
        private readonly Arena arena;
        private readonly GameHud hud = new GameHud { Visible = false };

        // Modular overlay containers (replacing the old permanent left sidebar)
        private readonly TitleBannerControl menuBanner = new TitleBannerControl { Visible = false };
        private readonly CoastalCardPanel mainCard = new CoastalCardPanel { Visible = false };
        private readonly CoastalCardPanel modalCard = new CoastalCardPanel { Visible = false };

        private bool fullScreen, arranging, mainCardWasVisibleBeforeModal;
        private Rectangle windowBounds;
        private FormWindowState windowState;
        private GameSession game;
        private ScreenMode screenMode = ScreenMode.MainMenu;
        private bool returnToCampaignFromTutorial;
        private int introPage;
        private int tutorialStep;
        private static readonly Lane[][] tutorialSequences =
        {
            new[] { Lane.Left, Lane.Center, Lane.Right, Lane.Center },
            new[] { Lane.Right, Lane.Center, Lane.Left, Lane.Right },
            new[] { Lane.Center, Lane.Right, Lane.Center, Lane.Left }
        };
        private int tutorialSequenceIndex;
        private readonly List<Lane> tutorialPattern = new List<Lane> { Lane.Left, Lane.Center, Lane.Right, Lane.Center };
        private int tutorialProgress;
        private int tutorialHp = 100;
        private int tutorialCombo;
        private string tutorialFeedback = "Escolha A (Esq.), S (Centro) ou D (Dir.) e pressione Enter para confirmar.";
        private GameSettings settings = new GameSettings();

        private string feedback = "";
        private AttackResult last;
        private bool attackPresentation;
        private Action modalCancelAction;

        // Cached references for dynamic layout inside mainCard and modalCard
        private Action currentCardLayout;
        private Action currentModalLayout;

        public MainForm()
        {
            arena = new Arena(art);
            arena.AttackFinished += (sender, args) =>
            {
                if (!attackPresentation) return;
                attackPresentation = false;
                if (screenMode == ScreenMode.Campaign && game != null)
                {
                    if (game.Phase != Phase.Input)
                    {
                        Render();
                    }
                    else
                    {
                        arena.ConfirmActionEnabled = true;
                        UpdateCombatFooterPrompt();
                        arena.Invalidate();
                    }
                }
                else
                {
                    arena.ConfirmActionEnabled = (screenMode == ScreenMode.Tutorial && tutorialStep == 2);
                    arena.Invalidate();
                }
            };
            arena.LaneSelectedByMouse += lane =>
            {
                if (modalCard.Visible || attackPresentation) return;
                if (screenMode == ScreenMode.Tutorial && tutorialStep == 2)
                    SelectTutorialLane(lane);
                else if (screenMode == ScreenMode.Campaign && game != null && game.Phase == Phase.Input)
                    SelectCampaignLane(lane);
            };
            arena.ConfirmRequested += (sender, args) =>
            {
                if (modalCard.Visible || attackPresentation) return;
                if (screenMode == ScreenMode.Tutorial && tutorialStep == 2)
                    ConfirmTutorialMove();
                else if (screenMode == ScreenMode.Campaign && game != null && game.Phase == Phase.Input)
                    MoveAndAttack(arena.PlayerLane);
            };

            hud.MenuRequested += (sender, args) =>
            {
                if (!modalCard.Visible)
                    ShowOptionsModal(screenMode == ScreenMode.Campaign && game != null);
            };

            Text = "Fisherman's Memory";
            Icon appIcon = CoastalTheme.LoadAppIcon();
            if (appIcon != null) Icon = appIcon;
            ClientSize = new Size(1280, 800);
            MinimumSize = new Size(640, 480);
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;
            KeyPreview = true;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = CoastalTheme.OceanDeep;
            ForeColor = CoastalTheme.SandLight;
            Font = CoastalTheme.BodyFont(11f);

            arena.Dock = DockStyle.Fill;
            Controls.Add(arena);

            arena.Controls.Add(modalCard);
            arena.Controls.Add(mainCard);
            arena.Controls.Add(menuBanner);
            arena.Controls.Add(hud);

            Resize += (sender, args) =>
            {
                arena.PausePresentation(WindowState == FormWindowState.Minimized);
                Arrange();
            };
            Load += (sender, args) => Arrange();
            Shown += (sender, args) => Arrange();
            FormClosing += (sender, args) => { if (game != null) Persist(); };
            FormClosed += (sender, args) => { arena.Dispose(); art.Dispose(); audio.Dispose(); };

            CoastalTheme.OnButtonHoverSound = () => audio.PlayUiHover();
            CoastalTheme.OnButtonSelectSound = () => audio.PlayUiSelect();

            settings = saves.LoadSettings();
            audio.Enabled = settings.AudioEnabled;
            arena.MotionEnabled = settings.MotionEnabled;

            ShowMenu();
        }

        private void ClearCardControls(Control container)
        {
            container.SuspendLayout();
            while (container.Controls.Count > 0)
            {
                var c = container.Controls[0];
                container.Controls.RemoveAt(0);
                c.Dispose();
            }
            container.ResumeLayout(false);
        }

        private int DefaultBackdropDimForScreen()
        {
            if (screenMode == ScreenMode.MainMenu) return 55;
            if (screenMode == ScreenMode.Tutorial) return tutorialStep == 2 ? 0 : 120;
            if (screenMode == ScreenMode.Campaign && game != null)
            {
                if (game.Phase == Phase.Input || attackPresentation) return 0;
                if (game.Phase == Phase.Reveal) return 45;
                return 130;
            }
            return 120;
        }

        private void PrepareModalOpen()
        {
            if (!modalCard.Visible)
            {
                mainCardWasVisibleBeforeModal = mainCard.Visible;
                mainCard.Visible = false;
                menuBanner.Visible = false;
            }
            arena.BackdropDimAlpha = 185;
            arena.Invalidate();
        }

        private void CloseModal()
        {
            if (!modalCard.Visible) return;
            modalCard.Visible = false;
            modalCancelAction = null;
            currentModalLayout = null;
            ClearCardControls(modalCard);
            mainCard.Visible = mainCardWasVisibleBeforeModal;
            menuBanner.Visible = screenMode == ScreenMode.MainMenu;
            arena.BackdropDimAlpha = DefaultBackdropDimForScreen();
            arena.Invalidate();
            Arrange();
        }

        // =====================================================================
        // 1. MENU PRINCIPAL E CONFIGURAÇÕES
        // =====================================================================
        public void ShowMenu()
        {
            CloseModal();
            screenMode = ScreenMode.MainMenu;
            attackPresentation = false;
            hud.Visible = false;
            menuBanner.Visible = true;
            mainCard.Visible = true;
            arena.ConfirmActionEnabled = false;
            arena.ShowCombatFooter = false;
            arena.ShowPlatforms = true;
            arena.BackdropDimAlpha = 55;
            arena.ClearAttack();
            audio.PlayTrack(MusicTrack.MenuCalm);
            ShowArt(0, ArtView.Menu, false);

            ClearCardControls(mainCard);
            mainCard.ParchmentInterior = false;
            mainCard.RuledJournalLines = false;
            mainCard.HeaderIcon = NauticalIcon.Anchor;
            mainCard.HeaderTitle = "DIÁRIO DE NAVEGAÇÃO";
            mainCard.HeaderSubtitle = "";
            mainCard.HeaderBadge = "";
            mainCard.HeaderHeight = 52;
            mainCard.FooterHeight = 52;

            bool hasSave = saves.Exists;
            string continueSub = hasSave ? "" : "Sem progresso salvo";

            var btnNew = new NauticalButton
            {
                Text = "Nova aventura",
                Icon = NauticalIcon.PlayArrow,
                StyleVariant = ButtonStyle.Primary,
                TabIndex = 0
            };
            btnNew.Click += (s, e) => RequestStartNewGame();

            var btnContinue = new NauticalButton
            {
                Text = "Continuar expedição",
                SubText = continueSub,
                Icon = NauticalIcon.Compass,
                StyleVariant = ButtonStyle.SecondaryWood,
                Enabled = hasSave,
                TabIndex = 1
            };
            btnContinue.Click += (s, e) => LoadSavedGame();

            var btnTutorial = new NauticalButton
            {
                Text = "Tutorial",
                Icon = NauticalIcon.Journal,
                StyleVariant = ButtonStyle.SecondaryWood,
                TabIndex = 2
            };
            btnTutorial.Click += (s, e) => ShowTutorial(false, false);

            var btnSettings = new NauticalButton
            {
                Text = "Configurações",
                Icon = NauticalIcon.Helm,
                StyleVariant = ButtonStyle.OceanToggle,
                TabIndex = 3
            };
            btnSettings.Click += (s, e) => ShowOptionsModal(false);

            var btnFullscreen = new NauticalButton
            {
                Text = fullScreen ? "Modo Janela" : "Tela Cheia",
                Icon = NauticalIcon.Fullscreen,
                StyleVariant = ButtonStyle.OceanToggle,
                TabIndex = 4
            };
            btnFullscreen.Click += (s, e) => ToggleFullScreen();

            var btnExit = new NauticalButton
            {
                Text = "Sair do jogo",
                Icon = NauticalIcon.CrossDanger,
                StyleVariant = ButtonStyle.Danger,
                TabIndex = 5
            };
            btnExit.Click += (s, e) => Close();

            mainCard.Controls.AddRange(new Control[] { btnNew, btnContinue, btnTutorial, btnSettings, btnFullscreen, btnExit });

            currentCardLayout = () =>
            {
                Rectangle content = mainCard.ContentBounds;
                Rectangle footer = mainCard.FooterBounds;
                int gap = content.Height < 170 ? 6 : 10;
                int rowH = Math.Max(36, Math.Min(54, (content.Height - gap * 2 - 4) / 3));
                int totalRowsH = rowH * 3 + gap * 2;
                int y = content.Y + Math.Max(2, (content.Height - totalRowsH) / 2);

                btnNew.SetBounds(content.X, y, content.Width, rowH);
                y += rowH + gap;

                btnContinue.SetBounds(content.X, y, content.Width, rowH);
                y += rowH + gap;

                int halfW = (content.Width - gap) / 2;
                btnTutorial.SetBounds(content.X, y, halfW, rowH);
                btnSettings.SetBounds(content.X + halfW + gap, y, content.Width - halfW - gap, rowH);

                int footBtnH = Math.Max(30, footer.Height - 6);
                btnFullscreen.SetBounds(footer.X, footer.Y + 3, halfW, footBtnH);
                btnExit.SetBounds(footer.X + halfW + gap, footer.Y + 3, footer.Width - halfW - gap, footBtnH);
            };

            Arrange();
            btnNew.Focus();
        }

        private void RequestStartNewGame()
        {
            if (saves.Exists)
            {
                ShowConfirmationModal(
                    "SUBSTITUIR PROGRESSO SALVO?",
                    "Confirmação de segurança do caderno de bordo",
                    "Já existe uma expedição salva neste computador.\n\nSe você iniciar uma nova aventura agora, o progresso anterior será substituído por uma nova jornada começando na Praia das Conchas com 100 HP e 0 pontos.",
                    "Sim, iniciar nova aventura",
                    "Cancelar e manter salvo",
                    StartNewGameConfirmed);
                return;
            }
            StartNewGameConfirmed();
        }

        private void StartNewGameConfirmed()
        {
            CloseModal();
            game = new GameSession();
            arena.PlayerLane = game.Level.Center ? Lane.Center : Lane.Left;
            arena.ClearAttack();
            last = null;
            feedback = "";
            Persist();
            ShowIntroduction(0);
        }

        private void LoadSavedGame()
        {
            try
            {
                game = saves.Load();
                arena.PlayerLane = game.Input.Count > 0
                    ? game.Input[game.Input.Count - 1]
                    : (game.Level.Center ? Lane.Center : Lane.Left);
                arena.ClearAttack();
                last = null;
                feedback = "";
                screenMode = ScreenMode.Campaign;
                audio.Music();
                Render();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException || e is NotSupportedException || e is InvalidDataException)
            {
                ShowMessageModal(
                    "FALHA AO CARREGAR O SAVE",
                    "O arquivo de progresso não pôde ser lido",
                    "Não foi possível carregar o progresso salvo.\n\nDetalhes técnicos:\n" + e.Message,
                    true);
            }
        }

        // =====================================================================
        // 2. INTRODUÇÃO E APRESENTAÇÃO NARRATIVA
        // =====================================================================
        public void ShowIntroduction(int page = 0)
        {
            CloseModal();
            screenMode = ScreenMode.Introduction;
            introPage = Math.Max(0, Math.Min(1, page));
            hud.Visible = false;
            menuBanner.Visible = false;
            mainCard.Visible = true;
            arena.ShowCombatFooter = false;
            arena.ShowPlatforms = true;
            arena.BackdropDimAlpha = 120;
            audio.PlayTrack(MusicTrack.MenuCalm);
            ShowArt(0, ArtView.Exploration, false);

            ClearCardControls(mainCard);
            mainCard.ParchmentInterior = true;
            mainCard.RuledJournalLines = true;
            mainCard.HeaderIcon = NauticalIcon.Lighthouse;
            mainCard.HeaderTitle = "A ILHA QUE ESQUECEU SEU NOME";
            mainCard.HeaderSubtitle = "Prólogo da Expedição • Crônicas do Farol";
            mainCard.HeaderBadge = "PÁGINA " + (introPage + 1) + " / 2";
            mainCard.HeaderHeight = 58;
            mainCard.FooterHeight = 56;

            string pageHeading = introPage == 0
                ? "I. O Desembarque na Costa Silenciosa"
                : "II. Os Guardiões e o Leviatã do Esquecimento";

            string pageBody = introPage == 0
                ? "Você é um desbravador que desembarca numa ilha litorânea. O farol se apagou, os caminhos mudaram e os habitantes perderam suas histórias.\n\nNas pedras úmidas do cais, páginas soltas de um antigo diário de bordo revelam que a memória da ilha ainda pode ser restaurada."
                : "Siga as pistas da praia até o coração da ilha. Observe os guardiões, memorize seus movimentos e recupere as informações que revelam a passagem.\n\nO Leviatã do Esquecimento espera no fim da jornada.";

            var narrativeBox = new NarrativePageControl(pageHeading, pageBody,
                introPage == 0
                    ? "Avance para a próxima página ou inicie o tutorial guiado."
                    : "Pronto para aprender os comandos ou desembarcar diretamente na Praia das Conchas.");

            var btnPrev = new NauticalButton
            {
                Text = introPage == 0 ? "Voltar ao menu" : "← Página 1",
                StyleVariant = ButtonStyle.SecondaryWood,
                TabIndex = 1
            };
            btnPrev.Click += (s, e) =>
            {
                if (introPage == 0) ShowMenu();
                else ShowIntroduction(0);
            };

            var btnSkip = new NauticalButton
            {
                Text = "Ir direto à ilha",
                Icon = NauticalIcon.Compass,
                StyleVariant = ButtonStyle.SecondaryWood,
                TabIndex = 2
            };
            btnSkip.Click += (s, e) =>
            {
                screenMode = ScreenMode.Campaign;
                audio.Music();
                Render();
            };

            var btnNext = new NauticalButton
            {
                Text = introPage == 0 ? "Próxima página →" : "Aprender a jogar",
                Icon = introPage == 0 ? NauticalIcon.Journal : NauticalIcon.PlayArrow,
                StyleVariant = ButtonStyle.Primary,
                TabIndex = 0
            };
            btnNext.Click += (s, e) =>
            {
                if (introPage == 0) ShowIntroduction(1);
                else ShowTutorial(true, false);
            };

            mainCard.Controls.AddRange(new Control[] { narrativeBox, btnPrev, btnSkip, btnNext });

            currentCardLayout = () =>
            {
                Rectangle content = mainCard.ContentBounds;
                Rectangle footer = mainCard.FooterBounds;
                narrativeBox.SetBounds(content.X, content.Y, content.Width, content.Height);

                int gap = 8;
                int btnH = Math.Max(34, footer.Height - 6);
                int w1 = footer.Width * 26 / 100;
                int w2 = footer.Width * 32 / 100;
                int w3 = Math.Max(120, footer.Width - w1 - w2 - gap * 2);

                btnPrev.SetBounds(footer.X, footer.Y + 3, w1, btnH);
                btnSkip.SetBounds(footer.X + w1 + gap, footer.Y + 3, w2, btnH);
                btnNext.SetBounds(footer.X + w1 + w2 + gap * 2, footer.Y + 3, w3, btnH);
            };

            Arrange();
            btnNext.Focus();
        }

        // =====================================================================
        // 3. TUTORIAL GUIADO EM ETAPAS + PRÁTICA ISOLADA (SANDBOX)
        // =====================================================================
        public void ShowTutorial(bool startCampaignAfter, bool returnToActiveCampaign, int step = 0)
        {
            CloseModal();
            screenMode = ScreenMode.Tutorial;
            returnToCampaignFromTutorial = returnToActiveCampaign;
            tutorialStep = Math.Max(0, Math.Min(2, step));
            menuBanner.Visible = false;
            hud.Visible = false;
            mainCard.Visible = true;

            if (tutorialStep == 2)
            {
                ShowArt(0, ArtView.Combat, true);
                arena.ShowCombatFooter = true;
                arena.ConfirmActionEnabled = true;
                arena.ShowPlatforms = true;
                arena.BackdropDimAlpha = 0;
                arena.ClearAttack();
                arena.PlayerLane = Lane.Center;
                tutorialProgress = 0;
                tutorialFeedback = "Escolha A/S/D (ou Setas) para posicionar e Enter para confirmar.";
                arena.CustomFooterPrompt = "Treino • Posição: " + Levels.Symbol(arena.PlayerLane) + " (" + CoastalTheme.LaneName(arena.PlayerLane) + ") • Pressione Enter ou Confirmar";
                audio.PlayTrack(MusicTrack.OceanAmbient);
            }
            else
            {
                ShowArt(0, ArtView.Exploration, false);
                arena.ConfirmActionEnabled = false;
                arena.ShowCombatFooter = false;
                arena.ShowPlatforms = true;
                arena.BackdropDimAlpha = 120;
                arena.ClearAttack();
                audio.PlayTrack(MusicTrack.MenuCalm);
            }

            ClearCardControls(mainCard);
            mainCard.ParchmentInterior = true;
            mainCard.RuledJournalLines = false;
            mainCard.HeaderIcon = tutorialStep == 2 ? NauticalIcon.PracticeTarget : NauticalIcon.Journal;
            mainCard.HeaderTitle = tutorialStep == 0 ? "TUTORIAL • 1. OBSERVAR E GUARDAR" :
                                   tutorialStep == 1 ? "TUTORIAL • 2. ESQUIVA, VIDA E COMBO" :
                                                       "TUTORIAL • 3. PRÁTICA LIVRE NA AREIA";
            mainCard.HeaderSubtitle = tutorialStep == 2
                ? "Escolha a posição com A/S/D (ou Setas) e confirme com Enter"
                : "Guia passo a passo da expedição (sem exigência de reflexos)";
            mainCard.HeaderBadge = "ETAPA " + (tutorialStep + 1) + " / 3";
            mainCard.HeaderHeight = 54;
            mainCard.FooterHeight = 52;

            Control bodyControl;
            if (tutorialStep == 0)
            {
                bodyControl = new TutorialStepInfoControl(0);
            }
            else if (tutorialStep == 1)
            {
                bodyControl = new TutorialStepInfoControl(1);
            }
            else
            {
                bodyControl = new TutorialPracticeBannerControl(tutorialPattern, tutorialProgress, tutorialFeedback);
            }

            string finishLabel = startCampaignAfter ? "Desembarcar na ilha" :
                                 returnToActiveCampaign ? "Voltar à partida" : "Voltar ao menu";

            var btnBack = new NauticalButton
            {
                Text = "← Etapa anterior",
                StyleVariant = ButtonStyle.SecondaryWood,
                Visible = tutorialStep > 0,
                TabIndex = 1
            };
            btnBack.Click += (s, e) =>
            {
                if (tutorialStep > 0)
                    ShowTutorial(startCampaignAfter, returnToActiveCampaign, tutorialStep - 1);
            };

            var btnSkipOrFinish = new NauticalButton
            {
                Text = finishLabel,
                SubText = tutorialStep < 2 ? "Pular tutorial" : "Concluir guia",
                Icon = startCampaignAfter || returnToActiveCampaign ? NauticalIcon.PlayArrow : NauticalIcon.Anchor,
                StyleVariant = tutorialStep == 2 ? ButtonStyle.Primary : ButtonStyle.SecondaryWood,
                TabIndex = tutorialStep == 2 ? 0 : 2
            };
            btnSkipOrFinish.Click += (s, e) => FinishTutorial(startCampaignAfter, returnToActiveCampaign);

            var btnNextStep = new NauticalButton
            {
                Text = tutorialStep == 0 ? "Próxima: Combate e HP →" :
                       tutorialStep == 1 ? "Próxima: Prática Livre →" :
                                           "Nova sequência de treino",
                Icon = tutorialStep < 2 ? NauticalIcon.Compass : NauticalIcon.PracticeTarget,
                StyleVariant = tutorialStep < 2 ? ButtonStyle.Primary : ButtonStyle.OceanToggle,
                TabIndex = tutorialStep < 2 ? 0 : 1
            };
            btnNextStep.Click += (s, e) =>
            {
                if (tutorialStep < 2)
                    ShowTutorial(startCampaignAfter, returnToActiveCampaign, tutorialStep + 1);
                else
                    ResetTutorialPracticeSequence();
            };

            mainCard.Controls.AddRange(new Control[] { bodyControl, btnBack, btnSkipOrFinish, btnNextStep });

            currentCardLayout = () =>
            {
                Rectangle content = mainCard.ContentBounds;
                Rectangle footer = mainCard.FooterBounds;
                bodyControl.SetBounds(content.X, content.Y, content.Width, content.Height);

                int gap = 8;
                int btnH = Math.Max(32, footer.Height - 6);
                if (tutorialStep == 0)
                {
                    int wSkip = footer.Width * 38 / 100;
                    btnSkipOrFinish.SetBounds(footer.X, footer.Y + 3, wSkip, btnH);
                    btnNextStep.SetBounds(footer.X + wSkip + gap, footer.Y + 3, footer.Width - wSkip - gap, btnH);
                }
                else
                {
                    int w1 = footer.Width * 28 / 100;
                    int w2 = footer.Width * 36 / 100;
                    int w3 = Math.Max(120, footer.Width - w1 - w2 - gap * 2);

                    btnBack.SetBounds(footer.X, footer.Y + 3, w1, btnH);
                    btnNextStep.SetBounds(footer.X + w1 + gap, footer.Y + 3, w2, btnH);
                    btnSkipOrFinish.SetBounds(footer.X + w1 + w2 + gap * 2, footer.Y + 3, w3, btnH);
                }
            };

            Arrange();
            if (tutorialStep < 2) btnNextStep.Focus();
            else ActiveControl = null;
        }

        private void ResetTutorialPracticeSequence()
        {
            tutorialSequenceIndex = (tutorialSequenceIndex + 1) % tutorialSequences.Length;
            tutorialPattern.Clear();
            tutorialPattern.AddRange(tutorialSequences[tutorialSequenceIndex]);
            tutorialProgress = 0;
            tutorialHp = 100;
            tutorialCombo = 0;
            arena.ClearAttack();
            arena.ConfirmActionEnabled = true;
            arena.CustomFooterPrompt = "Treino • Posição: " + Levels.Symbol(arena.PlayerLane) + " (" + CoastalTheme.LaneName(arena.PlayerLane) + ") • Pressione Enter ou Confirmar";
            tutorialFeedback = "HP Treino: 100/100 • Combo: 0/3 — Escolha a tecla do 1º passo e confirme com Enter.";
            RefreshTutorialPracticeBanner();
        }

        private void SelectTutorialLane(Lane lane)
        {
            if (screenMode != ScreenMode.Tutorial || tutorialStep != 2 || attackPresentation) return;
            arena.PlayerLane = lane;
            ActiveControl = null;
            audio.PlayUiHover();
            arena.CustomFooterPrompt = "Treino • Posição: " + Levels.Symbol(lane) + " (" + CoastalTheme.LaneName(lane) + ") • Pressione Enter ou Confirmar";
            arena.Invalidate();
        }

        private void ConfirmTutorialMove()
        {
            if (screenMode != ScreenMode.Tutorial || tutorialStep != 2 || attackPresentation) return;
            ExecuteTutorialPracticeMove(arena.PlayerLane);
        }

        private void ExecuteTutorialPracticeMove(Lane chosen)
        {
            if (screenMode != ScreenMode.Tutorial || tutorialStep != 2) return;
            Lane expected = tutorialPattern[tutorialProgress % tutorialPattern.Count];
            bool hit = chosen == expected;
            var simResult = new AttackResult(expected, chosen, hit, hit ? 0 : 20, 0, false);
            arena.Play(simResult, false, tutorialProgress + 1);

            if (hit)
            {
                audio.PlayHit();
                tutorialCombo++;
                if (tutorialCombo >= 3)
                {
                    tutorialHp = Math.Min(100, tutorialHp + 50);
                    tutorialCombo = 0;
                }
                tutorialProgress++;
                if (tutorialProgress >= tutorialPattern.Count)
                {
                    tutorialFeedback = "✓ Sequência concluída! (HP Treino: " + tutorialHp + "/100 • Combo: " + tutorialCombo + "/3) — Pronto para a ilha!";
                    tutorialProgress = 0;
                }
                else
                {
                    tutorialFeedback = "✓ Boa esquiva (" + Levels.Symbol(chosen) + ")! HP Treino: " + tutorialHp + "/100 • Combo: " + tutorialCombo + "/3";
                }
            }
            else
            {
                audio.Damage();
                tutorialHp = Math.Max(0, tutorialHp - 20);
                tutorialCombo = 0;
                if (tutorialHp == 0)
                {
                    tutorialHp = 100;
                    tutorialFeedback = "✕ Treino zerou o HP (−20)! Reiniciando HP: 100/100 • Segura era " + Levels.Symbol(expected) + ".";
                }
                else
                {
                    tutorialFeedback = "✕ Dano de treino (−20 HP)! HP: " + tutorialHp + "/100 • Segura era " + Levels.Symbol(expected) + ".";
                }
            }

            RefreshTutorialPracticeBanner();
        }

        private void RefreshTutorialPracticeBanner()
        {
            foreach (Control c in mainCard.Controls)
            {
                var banner = c as TutorialPracticeBannerControl;
                if (banner != null)
                {
                    banner.UpdateState(tutorialPattern, tutorialProgress, tutorialFeedback);
                    break;
                }
            }
        }

        private void FinishTutorial(bool startCampaignAfter, bool returnToActiveCampaign)
        {
            arena.ConfirmActionEnabled = false;
            arena.ClearAttack();
            if (startCampaignAfter || returnToActiveCampaign)
            {
                screenMode = ScreenMode.Campaign;
                audio.Music();
                Render();
            }
            else
            {
                ShowMenu();
            }
        }

        // =====================================================================
        // 4. CAMPANHA E TELAS DE FASE (DIÁRIO, REVELAÇÃO, COMBATE, MEMÓRIA, ETC.)
        // =====================================================================
        private void Act(Action action)
        {
            feedback = "";
            action();
            Persist();
            Render();
        }

        private void Persist()
        {
            if (game == null) return;
            try
            {
                saves.Save(game);
                if (game.Score > settings.BestScore)
                {
                    settings.BestScore = game.Score;
                    saves.SaveSettings(settings);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                ShowMessageModal(
                    "AVISO DE SALVAMENTO",
                    "A partida continua normalmente",
                    "A partida continua, mas o progresso não pôde ser salvo no disco neste momento.\n\nDetalhes: " + e.Message,
                    true);
            }
        }

        private void Render()
        {
            if (game == null) return;
            CloseModal();
            screenMode = ScreenMode.Campaign;
            menuBanner.Visible = false;

            var g = game;
            hud.Visible = true;
            hud.UpdateSession(g);

            // -----------------------------------------------------------------
            // COMBATE (Phase.Input ou apresentação do último efeito)
            // Zero barra lateral! O cenário e a arena ocupam a janela inteira.
            // -----------------------------------------------------------------
            if (g.Phase == Phase.Input || attackPresentation)
            {
                mainCard.Visible = false;
                ClearCardControls(mainCard);
                currentCardLayout = null;

                if (!g.Level.Center && arena.PlayerLane == Lane.Center)
                    arena.PlayerLane = Lane.Left;

                ShowArt(g.LevelIndex, ArtView.Combat, g.Level.Center);
                arena.SetDefeated(g.Hp == 0);
                arena.ShowCombatFooter = true;
                arena.ConfirmActionEnabled = (g.Phase == Phase.Input && !attackPresentation);
                arena.ShowPlatforms = true;
                arena.BackdropDimAlpha = 0;
                audio.PlayTrack(g.Level.Boss ? MusicTrack.BossBattle : MusicTrack.CombatBattle);
                UpdateCombatFooterPrompt();
                Arrange();
                return;
            }

            arena.ConfirmActionEnabled = false;
            arena.ShowCombatFooter = false;
            arena.ShowPlatforms = g.Phase != Phase.Reveal && g.Phase != Phase.Recall;
            arena.BackdropDimAlpha = DefaultBackdropDimForScreen();
            if (g.Phase == Phase.Recall) arena.ClearAttack();
            mainCard.Visible = true;
            ClearCardControls(mainCard);

            string stageBadge = (g.Level.Boss ? "FASE 6 • BOSS • " : "FASE " + (g.LevelIndex + 1) + " • ") + g.Level.Name.ToUpperInvariant();

            switch (g.Phase)
            {
                case Phase.Information:
                    audio.PlayTrack(MusicTrack.MenuCalm);
                    BuildInformationScreen(g, stageBadge);
                    break;
                case Phase.Reveal:
                    audio.PlayTrack(g.Level.Boss ? MusicTrack.BossBattle : MusicTrack.CombatBattle);
                    BuildRevealScreen(g, stageBadge);
                    break;
                case Phase.Recall:
                    audio.PlayTrack(MusicTrack.MenuCalm);
                    BuildRecallScreen(g, stageBadge);
                    break;
                case Phase.Results:
                    audio.PlayTrack(MusicTrack.MenuCalm);
                    BuildResultsScreen(g, stageBadge);
                    break;
                case Phase.Map:
                    audio.PlayTrack(MusicTrack.MenuCalm);
                    BuildMapScreen(g);
                    break;
                case Phase.GameOver:
                    audio.PlayTrack(MusicTrack.GameOver);
                    BuildGameOverScreen(g, stageBadge);
                    break;
                case Phase.Victory:
                    audio.PlayTrack(MusicTrack.Victory);
                    BuildVictoryScreen(g);
                    break;
            }

            Arrange();
        }

        // ---------------------------------------------------------------------
        // FASE: DIÁRIO DA EXPEDIÇÃO (Phase.Information)
        // ---------------------------------------------------------------------
        private void BuildInformationScreen(GameSession g, string stageBadge)
        {
            last = null;
            arena.ClearAttack();
            ShowArt(g.LevelIndex, ArtView.Exploration, g.Level.Center);

            mainCard.ParchmentInterior = true;
            mainCard.RuledJournalLines = true;
            mainCard.HeaderIcon = NauticalIcon.Journal;
            mainCard.HeaderTitle = "DIÁRIO DA EXPEDIÇÃO • " + g.Level.Name.ToUpperInvariant();
            mainCard.HeaderSubtitle = "Guardião da região: " + g.Level.Enemy + " • " + g.Level.Facts.Length + (g.Level.Facts.Length == 1 ? " informação para recordar" : " informações para recordar");
            mainCard.HeaderBadge = stageBadge;
            mainCard.HeaderHeight = 58;
            mainCard.FooterHeight = 56;

            // Uniform narrative text with ZERO highlighting of answer words
            var journalPage = new NarrativePageControl(
                "Registro de Campo — " + g.Level.Name,
                g.Level.Passage,
                "NOTA DE EXPEDIÇÃO: Leia o relato com atenção. Os detalhes serão perguntados após o combate e o diário permanecerá fechado até a próxima fase.");

            var btnMenu = new NauticalButton
            {
                Text = "Salvar e sair",
                Icon = NauticalIcon.Anchor,
                StyleVariant = ButtonStyle.SecondaryWood,
                TabIndex = 1
            };
            btnMenu.Click += (s, e) => { Persist(); ShowMenu(); };

            var btnReveal = new NauticalButton
            {
                Text = "Memorizei • Observar o inimigo",
                SubText = g.Level.Boss
                    ? ("Ver 1ª sequência (" + g.Level.SequenceLength + " ataques)")
                    : ("Ver " + g.Level.Attacks + " ataques"),
                Icon = NauticalIcon.PlayArrow,
                StyleVariant = ButtonStyle.Primary,
                TabIndex = 0
            };
            btnReveal.Click += (s, e) => Act(g.Reveal);

            mainCard.Controls.AddRange(new Control[] { journalPage, btnMenu, btnReveal });

            currentCardLayout = () =>
            {
                Rectangle content = mainCard.ContentBounds;
                Rectangle footer = mainCard.FooterBounds;
                journalPage.SetBounds(content.X, content.Y, content.Width, content.Height);

                int gap = 10;
                int btnH = Math.Max(34, footer.Height - 6);
                int menuW = footer.Width * 32 / 100;
                btnMenu.SetBounds(footer.X, footer.Y + 3, menuW, btnH);
                btnReveal.SetBounds(footer.X + menuW + gap, footer.Y + 3, footer.Width - menuW - gap, btnH);
            };

            btnReveal.Focus();
        }

        // ---------------------------------------------------------------------
        // FASE: REVELAÇÃO DA SEQUÊNCIA (Phase.Reveal)
        // ---------------------------------------------------------------------
        private void BuildRevealScreen(GameSession g, string stageBadge)
        {
            arena.ClearAttack();
            ShowArt(g.LevelIndex, ArtView.Combat, g.Level.Center);

            mainCard.ParchmentInterior = true;
            mainCard.RuledJournalLines = false;
            mainCard.HeaderIcon = NauticalIcon.Compass;
            mainCard.HeaderTitle = g.Level.Boss
                ? (g.Level.Enemy.ToUpperInvariant() + " • " + g.CurrentWave + "ª SEQUÊNCIA (" + g.Level.SequenceLength + " ATAQUES)")
                : (g.Level.Enemy.ToUpperInvariant() + " REVELA " + g.Level.Attacks + " ATAQUES");
            mainCard.HeaderSubtitle = g.Level.Boss
                ? ("Cada cartão indica a TECLA SEGURA (posição de fuga) do " + (g.CurrentWave == 1 ? "1º ao 8º" : "9º ao 16º") + " ataque. Memorize sem pressa.")
                : "Cada cartão indica a TECLA SEGURA (posição para escapar) na ordem correta. Memorize sem pressa.";
            mainCard.HeaderBadge = stageBadge;
            mainCard.HeaderHeight = 54;
            mainCard.FooterHeight = 52;

            var seqBoard = new SequenceBoardControl();
            seqBoard.SetPattern(g.CurrentWavePattern, g.Level.Center, g.CurrentWaveStart);

            var btnMenu = new NauticalButton
            {
                Text = "Salvar e sair",
                Icon = NauticalIcon.Anchor,
                StyleVariant = ButtonStyle.SecondaryWood,
                TabIndex = 1
            };
            btnMenu.Click += (s, e) => { Persist(); ShowMenu(); };

            var btnStartCombat = new NauticalButton
            {
                Text = "Iniciar combate (Ocultar sequência)",
                SubText = g.Level.Center ? "A/S/D ou Setas + Enter" : "A/D ou Setas + Enter",
                Icon = NauticalIcon.PlayArrow,
                StyleVariant = ButtonStyle.Primary,
                TabIndex = 0
            };
            btnStartCombat.Click += (s, e) => Act(g.HidePattern);

            mainCard.Controls.AddRange(new Control[] { seqBoard, btnMenu, btnStartCombat });

            currentCardLayout = () =>
            {
                Rectangle content = mainCard.ContentBounds;
                Rectangle footer = mainCard.FooterBounds;
                seqBoard.SetBounds(content.X, content.Y, content.Width, content.Height);

                int gap = 10;
                int btnH = Math.Max(32, footer.Height - 6);
                int menuW = footer.Width * 30 / 100;
                btnMenu.SetBounds(footer.X, footer.Y + 3, menuW, btnH);
                btnStartCombat.SetBounds(footer.X + menuW + gap, footer.Y + 3, footer.Width - menuW - gap, btnH);
            };

            btnStartCombat.Focus();
        }

        // ---------------------------------------------------------------------
        // FASE: RECUPERAÇÃO DE MEMÓRIA (Phase.Recall)
        // ---------------------------------------------------------------------
        private void BuildRecallScreen(GameSession g, string stageBadge)
        {
            ShowArt(g.LevelIndex, ArtView.Combat, g.Level.Center);
            arena.SetDefeated(false);

            mainCard.ParchmentInterior = true;
            mainCard.RuledJournalLines = false;
            mainCard.HeaderIcon = NauticalIcon.Journal;
            mainCard.HeaderTitle = "RECUPERE A INFORMAÇÃO INICIAL";
            mainCard.HeaderSubtitle = (!string.IsNullOrEmpty(feedback) && ClientSize.Width >= 780)
                ? ("Último golpe: " + feedback)
                : ("Você resistiu aos " + g.Level.Attacks + " ataques! Responda às pistas do diário.");
            mainCard.HeaderBadge = g.Level.Facts.Length + (g.Level.Facts.Length == 1 ? " PERGUNTA" : " PERGUNTAS");
            mainCard.HeaderHeight = 56;
            mainCard.FooterHeight = 54;

            var questionControls = new List<NauticalQuestionInput>();
            for (int i = 0; i < g.Level.Facts.Length; i++)
            {
                var qInput = new NauticalQuestionInput(i, g.Level.Facts[i].Question)
                {
                    TabIndex = i
                };
                questionControls.Add(qInput);
                mainCard.Controls.Add(qInput);
            }

            var feedbackBanner = new StatusBannerControl
            {
                BannerText = "Digite a palavra-chave de cada pergunta (maiúsculas e acentos são ignorados).",
                IsError = false
            };
            mainCard.Controls.Add(feedbackBanner);

            Action submitAnswers = () =>
            {
                string[] typed = questionControls.Select(x => x.AnswerText).ToArray();
                if (g.Recall(typed))
                {
                    Persist();
                    Render();
                }
                else
                {
                    int wrongCount = 0;
                    for (int i = 0; i < questionControls.Count; i++)
                    {
                        bool ok = g.IsFactCorrect(i, typed[i]);
                        questionControls[i].SetValidation(ok);
                        if (!ok) wrongCount++;
                    }

                    feedbackBanner.IsError = true;
                    feedbackBanner.BannerText = wrongCount == 1
                        ? "1 resposta não confere com o diário. É necessário repetir a fase."
                        : (wrongCount + " respostas não conferem com o diário. É necessário repetir a fase.");
                    feedbackBanner.Invalidate();

                    string msg = (wrongCount == questionControls.Count && questionControls.Count > 1
                        ? "Nenhuma das respostas digitadas corresponde às informações do diário."
                        : (wrongCount + " de " + questionControls.Count + " resposta(s) não correspondem às informações do diário."))
                        + "\n\nConforme as regras da expedição, ao errar a recuperação da memória você deverá repetir esta fase.";

                    ShowRepeatStageModal(
                        "MEMÓRIA INCORRETA • REPETIR FASE",
                        "As pistas não foram recuperadas corretamente",
                        msg,
                        () => Act(g.RepeatCurrentLevel));
                }
            };

            foreach (var qc in questionControls)
                qc.EnterPressed += (s, e) => submitAnswers();

            var btnMenu = new NauticalButton
            {
                Text = "Salvar e sair",
                Icon = NauticalIcon.Anchor,
                StyleVariant = ButtonStyle.SecondaryWood,
                TabIndex = questionControls.Count + 1
            };
            btnMenu.Click += (s, e) => { Persist(); ShowMenu(); };

            var btnValidate = new NauticalButton
            {
                Text = "Validar memória",
                Icon = NauticalIcon.ShieldCheck,
                StyleVariant = ButtonStyle.Primary,
                TabIndex = questionControls.Count
            };
            btnValidate.Click += (s, e) => submitAnswers();

            mainCard.Controls.Add(btnMenu);
            mainCard.Controls.Add(btnValidate);

            currentCardLayout = () =>
            {
                Rectangle content = mainCard.ContentBounds;
                Rectangle footer = mainCard.FooterBounds;

                int bannerH = content.Height < 210 ? 28 : 36;
                feedbackBanner.SetBounds(content.X, content.Bottom - bannerH, content.Width, bannerH);

                int availQHeight = Math.Max(60, content.Height - bannerH - 6);
                int qCount = Math.Max(1, questionControls.Count);
                int gap = availQHeight < 180 ? 3 : 6;
                int eachH = Math.Max(38, Math.Min(80, (availQHeight - gap * (qCount - 1)) / qCount));

                int y = content.Y;
                for (int i = 0; i < questionControls.Count; i++)
                {
                    questionControls[i].SetBounds(content.X, y, content.Width, eachH);
                    y += eachH + gap;
                }

                int btnH = Math.Max(32, footer.Height - 6);
                int menuW = footer.Width * 32 / 100;
                btnMenu.SetBounds(footer.X, footer.Y + 3, menuW, btnH);
                btnValidate.SetBounds(footer.X + menuW + 10, footer.Y + 3, footer.Width - menuW - 10, btnH);
            };

            if (questionControls.Count > 0)
                questionControls[0].InnerTextBox.Focus();
        }

        // ---------------------------------------------------------------------
        // FASE: RESULTADOS DA REGIÃO (Phase.Results)
        // ---------------------------------------------------------------------
        private void BuildResultsScreen(GameSession g, string stageBadge)
        {
            ShowArt(g.LevelIndex, ArtView.Exploration, g.Level.Center);

            bool fullRecall = g.RecallTotal == 0 || g.RecallCorrect == g.RecallTotal;
            mainCard.ParchmentInterior = true;
            mainCard.RuledJournalLines = false;
            mainCard.HeaderIcon = NauticalIcon.SunTrophy;
            mainCard.HeaderTitle = (fullRecall ? "REGIÃO LIBERTADA! • " : "TRAVESSIA CONCLUÍDA • ") + g.Level.Name.ToUpperInvariant();
            mainCard.HeaderSubtitle = fullRecall
                ? "As memórias desta costa foram restauradas com sucesso"
                : "Você avançou na travessia com recuperação parcial de memória";
            mainCard.HeaderBadge = stageBadge;
            mainCard.HeaderHeight = 56;
            mainCard.FooterHeight = 54;

            var reportView = new ResultsReportControl(g);

            var btnMenu = new NauticalButton
            {
                Text = "Salvar e sair",
                Icon = NauticalIcon.Anchor,
                StyleVariant = ButtonStyle.SecondaryWood,
                TabIndex = 1
            };
            btnMenu.Click += (s, e) => { Persist(); ShowMenu(); };

            var btnContinue = new NauticalButton
            {
                Text = g.Level.Boss ? "Revelar o destino da ilha" : "Abrir mapa da expedição",
                SubText = g.Level.Boss ? "Conclusão da jornada" : "Ver rota para a próxima região",
                Icon = g.Level.Boss ? NauticalIcon.Lighthouse : NauticalIcon.Compass,
                StyleVariant = ButtonStyle.Primary,
                TabIndex = 0
            };
            btnContinue.Click += (s, e) => Act(g.LeaveResults);

            mainCard.Controls.AddRange(new Control[] { reportView, btnMenu, btnContinue });

            currentCardLayout = () =>
            {
                Rectangle content = mainCard.ContentBounds;
                Rectangle footer = mainCard.FooterBounds;
                reportView.SetBounds(content.X, content.Y, content.Width, content.Height);

                int btnH = Math.Max(34, footer.Height - 6);
                int menuW = footer.Width * 32 / 100;
                btnMenu.SetBounds(footer.X, footer.Y + 3, menuW, btnH);
                btnContinue.SetBounds(footer.X + menuW + 10, footer.Y + 3, footer.Width - menuW - 10, btnH);
            };

            btnContinue.Focus();
        }

        // ---------------------------------------------------------------------
        // FASE: MAPA DE PROGRESSÃO (Phase.Map)
        // ---------------------------------------------------------------------
        private void BuildMapScreen(GameSession g)
        {
            ShowArt(g.LevelIndex + 1, ArtView.Map, g.Level.Center);

            var nextLevel = Levels.All[Math.Min(Levels.All.Count - 1, g.LevelIndex + 1)];
            mainCard.ParchmentInterior = true;
            mainCard.RuledJournalLines = false;
            mainCard.HeaderIcon = NauticalIcon.Compass;
            mainCard.HeaderTitle = "CARTA NÁUTICA • ROTA DA EXPEDIÇÃO";
            mainCard.HeaderSubtitle = "Próximo destino: " + nextLevel.Name + " (" + nextLevel.Enemy + ")";
            mainCard.HeaderBadge = (g.LevelIndex + 1) + " DE " + Levels.All.Count + " CONCLUÍDAS";
            mainCard.HeaderHeight = 56;
            mainCard.FooterHeight = 54;

            var mapCanvas = new RouteMapCanvas();
            mapCanvas.SetCompletedLevel(g.LevelIndex);

            var btnMenu = new NauticalButton
            {
                Text = "Salvar e sair",
                Icon = NauticalIcon.Anchor,
                StyleVariant = ButtonStyle.SecondaryWood,
                TabIndex = 1
            };
            btnMenu.Click += (s, e) => { Persist(); ShowMenu(); };

            var btnNextLevel = new NauticalButton
            {
                Text = "Seguir para a próxima região",
                SubText = nextLevel.Name,
                Icon = NauticalIcon.PlayArrow,
                StyleVariant = ButtonStyle.Primary,
                TabIndex = 0
            };
            btnNextLevel.Click += (s, e) => Act(g.NextLevel);

            mainCard.Controls.AddRange(new Control[] { mapCanvas, btnMenu, btnNextLevel });

            currentCardLayout = () =>
            {
                Rectangle content = mainCard.ContentBounds;
                Rectangle footer = mainCard.FooterBounds;
                mapCanvas.SetBounds(content.X, content.Y, content.Width, content.Height);

                int btnH = Math.Max(34, footer.Height - 6);
                int menuW = footer.Width * 32 / 100;
                btnMenu.SetBounds(footer.X, footer.Y + 3, menuW, btnH);
                btnNextLevel.SetBounds(footer.X + menuW + 10, footer.Y + 3, footer.Width - menuW - 10, btnH);
            };

            btnNextLevel.Focus();
        }

        // ---------------------------------------------------------------------
        // FASE: DERROTA (Phase.GameOver)
        // ---------------------------------------------------------------------
        private void BuildGameOverScreen(GameSession g, string stageBadge)
        {
            ShowArt(g.LevelIndex, ArtView.Combat, g.Level.Center);
            arena.SetDefeated(true);

            mainCard.ParchmentInterior = false;
            mainCard.RuledJournalLines = false;
            mainCard.HeaderIcon = NauticalIcon.WaveSkull;
            mainCard.HeaderTitle = "O MAR LEVOU SUAS FORÇAS";
            mainCard.HeaderSubtitle = "Fim da expedição em " + g.Level.Name + " (" + g.Level.Enemy + ")";
            mainCard.HeaderBadge = "DERROTA";
            mainCard.HeaderHeight = 58;
            mainCard.FooterHeight = 56;

            var outcomeCtrl = new EndScreenSummaryControl(
                false,
                "O mar levou suas forças, mas a jornada pode recomeçar.",
                !string.IsNullOrEmpty(feedback)
                    ? ("Último golpe sofrido: " + feedback)
                    : "Sua vitalidade chegou a zero durante o confronto contra o guardião.",
                g.Score,
                g.Hp,
                "Uma nova aventura começa na Praia das Conchas com 100 HP.");

            var btnMenu = new NauticalButton
            {
                Text = "Voltar ao menu",
                Icon = NauticalIcon.Anchor,
                StyleVariant = ButtonStyle.SecondaryWood,
                TabIndex = 1
            };
            btnMenu.Click += (s, e) => { Persist(); ShowMenu(); };

            var btnRetry = new NauticalButton
            {
                Text = "Iniciar nova aventura",
                SubText = "Recomeçar com 100 HP",
                Icon = NauticalIcon.PlayArrow,
                StyleVariant = ButtonStyle.Primary,
                TabIndex = 0
            };
            btnRetry.Click += (s, e) => StartNewGameConfirmed();

            mainCard.Controls.AddRange(new Control[] { outcomeCtrl, btnMenu, btnRetry });

            currentCardLayout = () =>
            {
                Rectangle content = mainCard.ContentBounds;
                Rectangle footer = mainCard.FooterBounds;
                outcomeCtrl.SetBounds(content.X, content.Y, content.Width, content.Height);

                int btnH = Math.Max(34, footer.Height - 6);
                int menuW = footer.Width * 36 / 100;
                btnMenu.SetBounds(footer.X, footer.Y + 3, menuW, btnH);
                btnRetry.SetBounds(footer.X + menuW + 10, footer.Y + 3, footer.Width - menuW - 10, btnH);
            };

            btnRetry.Focus();
        }

        // ---------------------------------------------------------------------
        // FASE: VITÓRIA (Phase.Victory)
        // ---------------------------------------------------------------------
        private void BuildVictoryScreen(GameSession g)
        {
            ShowArt(0, ArtView.Victory, false);

            mainCard.ParchmentInterior = true;
            mainCard.RuledJournalLines = false;
            mainCard.HeaderIcon = NauticalIcon.Lighthouse;
            mainCard.HeaderTitle = "AURORA VOLTOU A LEMBRAR";
            mainCard.HeaderSubtitle = "O farol brilha novamente sobre todas as costas da ilha";
            mainCard.HeaderBadge = "VITÓRIA!";
            mainCard.HeaderHeight = 58;
            mainCard.FooterHeight = 56;

            var victoryCtrl = new EndScreenSummaryControl(
                true,
                "O Leviatã recua. Você devolve a pérola ao farol e a ilha recupera seu nome. As histórias voltam a atravessar o mar.",
                g.Score >= 12050
                    ? "★ Campanha Perfeita! Você concluiu todas as 6 fases sem nenhum erro!"
                    : "Todas as memórias das seis regiões da ilha foram restauradas ao diário.",
                g.Score,
                g.Hp,
                "Expedição concluída • " + g.Score + " pontos • " + g.Hp + " HP restantes");

            var btnMenu = new NauticalButton
            {
                Text = "Salvar e voltar ao menu",
                Icon = NauticalIcon.Anchor,
                StyleVariant = ButtonStyle.SecondaryWood,
                TabIndex = 1
            };
            btnMenu.Click += (s, e) => { Persist(); ShowMenu(); };

            var btnNewAdventure = new NauticalButton
            {
                Text = "Jogar nova aventura",
                SubText = "Nova travessia",
                Icon = NauticalIcon.PlayArrow,
                StyleVariant = ButtonStyle.Primary,
                TabIndex = 0
            };
            btnNewAdventure.Click += (s, e) => RequestStartNewGame();

            mainCard.Controls.AddRange(new Control[] { victoryCtrl, btnMenu, btnNewAdventure });

            currentCardLayout = () =>
            {
                Rectangle content = mainCard.ContentBounds;
                Rectangle footer = mainCard.FooterBounds;
                victoryCtrl.SetBounds(content.X, content.Y, content.Width, content.Height);

                int btnH = Math.Max(34, footer.Height - 6);
                int menuW = footer.Width * 42 / 100;
                btnMenu.SetBounds(footer.X, footer.Y + 3, menuW, btnH);
                btnNewAdventure.SetBounds(footer.X + menuW + 10, footer.Y + 3, footer.Width - menuW - 10, btnH);
            };

            btnMenu.Focus();
        }

        // =====================================================================
        // 5. MODAIS INTEGRADOS (CONFIRMAÇÕES, ERROS E OPÇÕES SOB DEMANDA)
        // =====================================================================
        private void ShowConfirmationModal(string title, string subtitle, string message, string confirmLabel, string cancelLabel, Action onConfirm, Action onCancel = null, string footerNote = null)
        {
            PrepareModalOpen();
            ClearCardControls(modalCard);
            modalCard.ParchmentInterior = true;
            modalCard.RuledJournalLines = false;
            modalCard.HeaderIcon = NauticalIcon.Compass;
            modalCard.HeaderTitle = title;
            modalCard.HeaderSubtitle = subtitle;
            modalCard.HeaderBadge = "CONFIRMAÇÃO";
            modalCard.HeaderHeight = 54;
            modalCard.FooterHeight = 54;

            var msgBox = new NarrativePageControl(
                "Atenção às anotações do diário",
                message,
                footerNote ?? "Pressione ESC ou clique em Cancelar para manter seu progresso atual intacto.");

            var btnCancel = new NauticalButton
            {
                Text = cancelLabel,
                Icon = NauticalIcon.ShieldCheck,
                StyleVariant = ButtonStyle.SecondaryWood,
                TabIndex = 0
            };
            btnCancel.Click += (s, e) =>
            {
                CloseModal();
                if (onCancel != null) onCancel();
            };

            var btnConfirm = new NauticalButton
            {
                Text = confirmLabel,
                Icon = NauticalIcon.PlayArrow,
                StyleVariant = ButtonStyle.Danger,
                TabIndex = 1
            };
            btnConfirm.Click += (s, e) =>
            {
                CloseModal();
                if (onConfirm != null) onConfirm();
            };

            modalCancelAction = () =>
            {
                CloseModal();
                if (onCancel != null) onCancel();
            };
            modalCard.Controls.AddRange(new Control[] { msgBox, btnCancel, btnConfirm });

            currentModalLayout = () =>
            {
                Rectangle content = modalCard.ContentBounds;
                Rectangle footer = modalCard.FooterBounds;
                msgBox.SetBounds(content.X, content.Y, content.Width, content.Height);

                int gap = 10;
                int btnH = Math.Max(34, footer.Height - 6);
                int half = (footer.Width - gap) / 2;
                btnCancel.SetBounds(footer.X, footer.Y + 3, half, btnH);
                btnConfirm.SetBounds(footer.X + half + gap, footer.Y + 3, footer.Width - half - gap, btnH);
            };

            modalCard.Visible = true;
            modalCard.BringToFront();
            Arrange();
            btnCancel.Focus();
        }

        private void ShowMessageModal(string title, string subtitle, string message, bool isError)
        {
            PrepareModalOpen();
            ClearCardControls(modalCard);
            modalCard.ParchmentInterior = true;
            modalCard.RuledJournalLines = false;
            modalCard.HeaderIcon = isError ? NauticalIcon.CrossDanger : NauticalIcon.Journal;
            modalCard.HeaderTitle = title;
            modalCard.HeaderSubtitle = subtitle;
            modalCard.HeaderBadge = isError ? "ERRO" : "AVISO";
            modalCard.HeaderHeight = 54;
            modalCard.FooterHeight = 52;

            var msgBox = new NarrativePageControl(
                isError ? "Aviso do Sistema de Arquivos" : "Informação",
                message,
                "Pressione Enter ou Esc para fechar este aviso.");

            var btnOk = new NauticalButton
            {
                Text = "Entendido • Fechar aviso",
                Icon = NauticalIcon.ShieldCheck,
                StyleVariant = ButtonStyle.Primary,
                TabIndex = 0
            };
            btnOk.Click += (s, e) => CloseModal();

            modalCancelAction = CloseModal;
            modalCard.Controls.AddRange(new Control[] { msgBox, btnOk });

            currentModalLayout = () =>
            {
                Rectangle content = modalCard.ContentBounds;
                Rectangle footer = modalCard.FooterBounds;
                msgBox.SetBounds(content.X, content.Y, content.Width, content.Height);

                int btnH = Math.Max(34, footer.Height - 6);
                int btnW = Math.Min(footer.Width, 280);
                btnOk.SetBounds(footer.X + (footer.Width - btnW) / 2, footer.Y + 3, btnW, btnH);
            };

            modalCard.Visible = true;
            modalCard.BringToFront();
            Arrange();
            btnOk.Focus();
        }

        private void ShowRepeatStageModal(string title, string subtitle, string message, Action onRepeat)
        {
            PrepareModalOpen();
            ClearCardControls(modalCard);
            modalCard.ParchmentInterior = true;
            modalCard.RuledJournalLines = false;
            modalCard.HeaderIcon = NauticalIcon.CrossDanger;
            modalCard.HeaderTitle = title;
            modalCard.HeaderSubtitle = subtitle;
            modalCard.HeaderBadge = "REPETIR";
            modalCard.HeaderHeight = 54;
            modalCard.FooterHeight = 54;

            var msgBox = new NarrativePageControl(
                "Revisão do Diário de Expedição",
                message,
                "Pressione Enter ou clique em Repetir fase para recomeçar esta região.");

            var btnRepeat = new NauticalButton
            {
                Text = "Repetir esta fase",
                Icon = NauticalIcon.PlayArrow,
                StyleVariant = ButtonStyle.Primary,
                TabIndex = 0
            };
            btnRepeat.Click += (s, e) =>
            {
                CloseModal();
                if (onRepeat != null) onRepeat();
            };

            modalCancelAction = () =>
            {
                CloseModal();
                if (onRepeat != null) onRepeat();
            };
            modalCard.Controls.AddRange(new Control[] { msgBox, btnRepeat });

            currentModalLayout = () =>
            {
                Rectangle content = modalCard.ContentBounds;
                Rectangle footer = modalCard.FooterBounds;
                msgBox.SetBounds(content.X, content.Y, content.Width, content.Height);

                int btnH = Math.Max(34, footer.Height - 6);
                int btnW = Math.Min(footer.Width, 300);
                btnRepeat.SetBounds(footer.X + (footer.Width - btnW) / 2, footer.Y + 3, btnW, btnH);
            };

            modalCard.Visible = true;
            modalCard.BringToFront();
            Arrange();
            btnRepeat.Focus();
        }

        private void ShowOptionsModal(bool inCampaign)
        {
            PrepareModalOpen();
            ClearCardControls(modalCard);
            modalCard.ParchmentInterior = false;
            modalCard.RuledJournalLines = false;
            modalCard.HeaderIcon = NauticalIcon.Helm;
            modalCard.HeaderTitle = inCampaign ? "MENU DA EXPEDIÇÃO" : "CONFIGURAÇÕES DA AVENTURA";
            modalCard.HeaderSubtitle = "Ajustes de áudio, animações, tela e navegação";
            modalCard.HeaderBadge = "OPÇÕES";
            modalCard.HeaderHeight = 54;
            modalCard.FooterHeight = 52;

            var btnAudio = new NauticalButton
            {
                Text = audio.Enabled ? "Som e Música: Ligado" : "Som e Música: Desligado",
                SubText = audio.Enabled ? "ATIVO" : "MUDO",
                Icon = audio.Enabled ? NauticalIcon.SoundOn : NauticalIcon.SoundOff,
                StyleVariant = ButtonStyle.OceanToggle,
                TabIndex = 0
            };
            btnAudio.Click += (s, e) =>
            {
                audio.Enabled = !audio.Enabled;
                if (audio.Enabled) audio.ResumeCurrentTrack();
                settings.AudioEnabled = audio.Enabled;
                saves.SaveSettings(settings);
                ShowOptionsModal(inCampaign);
            };

            var btnMotion = new NauticalButton
            {
                Text = arena.MotionEnabled ? "Animações: Ligadas" : "Animações: Desligadas",
                SubText = arena.MotionEnabled ? "ATIVAS" : "ESTÁTICO",
                Icon = arena.MotionEnabled ? NauticalIcon.MotionOn : NauticalIcon.MotionOff,
                StyleVariant = ButtonStyle.OceanToggle,
                TabIndex = 1
            };
            btnMotion.Click += (s, e) =>
            {
                arena.MotionEnabled = !arena.MotionEnabled;
                settings.MotionEnabled = arena.MotionEnabled;
                saves.SaveSettings(settings);
                ShowOptionsModal(inCampaign);
            };

            var btnFullscreen = new NauticalButton
            {
                Text = fullScreen ? "Alternar para Modo Janela" : "Alternar para Tela Cheia",
                SubText = "F11 / Alt+Enter",
                Icon = NauticalIcon.Fullscreen,
                StyleVariant = ButtonStyle.OceanToggle,
                TabIndex = 2
            };
            btnFullscreen.Click += (s, e) =>
            {
                ToggleFullScreen();
                ShowOptionsModal(inCampaign);
            };

            var btnTutorial = new NauticalButton
            {
                Text = "Consultar Tutorial e Prática",
                SubText = "Regras e comandos",
                Icon = NauticalIcon.Journal,
                StyleVariant = ButtonStyle.SecondaryWood,
                TabIndex = 3
            };
            btnTutorial.Click += (s, e) =>
            {
                CloseModal();
                ShowTutorial(false, inCampaign, 0);
            };

            var btnClose = new NauticalButton
            {
                Text = inCampaign ? "Voltar à expedição" : "Concluir ajustes",
                Icon = NauticalIcon.PlayArrow,
                StyleVariant = ButtonStyle.Primary,
                TabIndex = 4
            };
            btnClose.Click += (s, e) =>
            {
                CloseModal();
                if (!inCampaign && screenMode == ScreenMode.MainMenu) ShowMenu();
            };

            NauticalButton btnSaveMenu = null;
            if (inCampaign)
            {
                btnSaveMenu = new NauticalButton
                {
                    Text = "Salvar e ir ao menu",
                    Icon = NauticalIcon.Anchor,
                    StyleVariant = ButtonStyle.SecondaryWood,
                    TabIndex = 5
                };
                btnSaveMenu.Click += (s, e) =>
                {
                    CloseModal();
                    Persist();
                    ShowMenu();
                };
                modalCard.Controls.Add(btnSaveMenu);
            }

            modalCancelAction = () =>
            {
                CloseModal();
                if (!inCampaign && screenMode == ScreenMode.MainMenu) ShowMenu();
            };

            modalCard.Controls.AddRange(new Control[] { btnAudio, btnMotion, btnFullscreen, btnTutorial, btnClose });

            currentModalLayout = () =>
            {
                Rectangle content = modalCard.ContentBounds;
                Rectangle footer = modalCard.FooterBounds;

                int count = 4;
                int gap = content.Height < 170 ? 5 : 8;
                int eachH = Math.Max(32, Math.Min(46, (content.Height - gap * (count - 1)) / count));
                int y = content.Y;

                foreach (var btn in new[] { btnAudio, btnMotion, btnFullscreen, btnTutorial })
                {
                    btn.SetBounds(content.X, y, content.Width, eachH);
                    y += eachH + gap;
                }

                int fBtnH = Math.Max(32, footer.Height - 6);
                if (btnSaveMenu != null)
                {
                    int half = (footer.Width - 10) / 2;
                    btnSaveMenu.SetBounds(footer.X, footer.Y + 3, half, fBtnH);
                    btnClose.SetBounds(footer.X + half + 10, footer.Y + 3, footer.Width - half - 10, fBtnH);
                }
                else
                {
                    btnClose.SetBounds(footer.X, footer.Y + 3, footer.Width, fBtnH);
                }
            };

            modalCard.Visible = true;
            modalCard.BringToFront();
            Arrange();
            btnClose.Focus();
        }

        // =====================================================================
        // 6. COMBATE E MOVIMENTAÇÃO POR TECLA
        // =====================================================================
        private void ShowArt(int stage, ArtView view, bool center)
        {
            arena.Configure(stage, view, center);
            arena.Invalidate();
        }

        private void UpdateCombatFooterPrompt()
        {
            if (game == null) return;
            int atkNum = Math.Min(game.Level.Attacks, game.Resolved + 1);
            string posStr = Levels.Symbol(arena.PlayerLane) + " (" + CoastalTheme.LaneName(arena.PlayerLane) + ")";
            string keysHint = game.Level.Center ? "A/S/D" : "A/D";
            arena.CustomFooterPrompt = ClientSize.Width < 760
                ? ("Atk " + atkNum + "/" + game.Level.Attacks + " • Escolhida: " + posStr + " • Enter confirma")
                : ("Guardião: " + game.Level.Enemy + " • Ataque " + atkNum + "/" + game.Level.Attacks + " • Posição: " + posStr + " (" + keysHint + " muda, Enter confirma)");
        }

        private void SelectCampaignLane(Lane lane)
        {
            if (game == null || game.Phase != Phase.Input || attackPresentation) return;
            if (!game.Level.Center && lane == Lane.Center)
            {
                arena.CustomFooterPrompt = "Fase 1 possui apenas Esquerda (A ←) e Direita (D →). Posição: " + Levels.Symbol(arena.PlayerLane);
                arena.Invalidate();
                return;
            }
            arena.PlayerLane = lane;
            audio.PlayUiHover();
            UpdateCombatFooterPrompt();
            arena.Invalidate();
        }

        private void MoveAndAttack(Lane lane)
        {
            if (game == null || game.Phase != Phase.Input || attackPresentation) return;
            if (!game.Level.Center && lane == Lane.Center) return;
            if (mainCard.Visible)
            {
                mainCard.Visible = false;
                currentCardLayout = null;
                arena.ShowCombatFooter = true;
                Arrange();
            }
            arena.ConfirmActionEnabled = false;
            audio.PlayTrack(game.Level.Boss ? MusicTrack.BossBattle : MusicTrack.CombatBattle);
            last = game.SubmitMove(lane);
            attackPresentation = true;
            arena.Play(last, game.Hp == 0, game.Resolved);
            feedback = last.Correct
                ? ("Esquiva correta! +100 pontos." + (last.Combo ? (" Combo! +150 pontos e +" + last.Healing + " HP.") : ""))
                : ("Posição incorreta: −" + last.Damage + " HP e −50 pontos. Posição segura: " + Levels.Symbol(last.Safe) + ".");
            if (!last.Correct) audio.Damage();
            else if (last.Combo) audio.PlayCombo();
            else audio.PlayHit();

            Persist();

            // Update HUD & Combat Footer in-place without rebuilding controls!
            hud.UpdateSession(game);
            arena.SetDefeated(game.Hp == 0);
            arena.Invalidate();
        }

        // =====================================================================
        // 7. LAYOUT RESPONSIVO (640×480, 1280×800, 1920×1080 E DPI 100%..200%)
        // =====================================================================
        private void Arrange()
        {
            if (arranging || ClientSize.Width < 1 || ClientSize.Height < 1) return;
            arranging = true;
            try
            {
                float dpi = DeviceDpi / 96f;
                int cw = ClientSize.Width;
                int ch = ClientSize.Height;
                bool compact = cw < 820 || ch < 580;

                // 1. Top HUD Bar
                int hudHeight = 0;
                if (hud.Visible)
                {
                    hudHeight = compact ? Math.Max(46, (int)(48 * dpi)) : Math.Min((int)(68 * dpi), Math.Max(54, ch * 9 / 100));
                    int marginX = compact ? 6 : 12;
                    hud.SetBounds(marginX, 6, Math.Max(100, cw - marginX * 2), hudHeight);
                    hudHeight += 10;
                }

                Rectangle workArea = new Rectangle(0, hudHeight, cw, Math.Max(100, ch - hudHeight));

                // 2. Main Menu Title Banner + Centered Expedition Card
                if (screenMode == ScreenMode.MainMenu)
                {
                    int bannerW = Math.Min(cw - 20, Math.Max(420, Math.Min((int)(720 * dpi), cw * 72 / 100)));
                    int bannerH = compact ? Math.Max(78, ch * 21 / 100) : Math.Min((int)(126 * dpi), Math.Max(96, ch * 19 / 100));
                    int bannerX = (cw - bannerW) / 2;
                    int bannerY = compact ? 8 : Math.Max(14, ch * 4 / 100);
                    menuBanner.SetBounds(bannerX, bannerY, bannerW, bannerH);

                    int availBelowBanner = Math.Max(220, ch - menuBanner.Bottom - (compact ? 14 : 28));
                    int cardW = Math.Min(cw - 20, Math.Max(380, Math.Min((int)(470 * dpi), cw * 52 / 100)));
                    int cardH = Math.Min(availBelowBanner, Math.Max(255, (int)(325 * dpi)));
                    int cardX = (cw - cardW) / 2;
                    int cardY = menuBanner.Bottom + Math.Max(6, (availBelowBanner - cardH) / 2);

                    mainCard.HeaderHeight = compact ? 44 : 54;
                    mainCard.FooterHeight = compact ? 42 : 50;
                    mainCard.SetBounds(cardX, cardY, cardW, cardH);
                    arena.PlayArea = ClientRectangle;
                }
                // 3. Tutorial Step 3 (Live Sandbox Practice) & Reveal Screen:
                // Position card in the upper area so the live enemy & position platforms are visible below!
                else if ((screenMode == ScreenMode.Tutorial && tutorialStep == 2) ||
                         (screenMode == ScreenMode.Campaign && game != null && game.Phase == Phase.Reveal && !attackPresentation))
                {
                    bool isReveal = screenMode == ScreenMode.Campaign;
                    int cardW = Math.Min(cw - 16, Math.Max(480, Math.Min((int)(840 * dpi), cw * 82 / 100)));
                    int maxCardH = isReveal
                        ? (compact ? Math.Min(workArea.Height - 10, 275) : Math.Min(workArea.Height * 50 / 100, (int)(310 * dpi)))
                        : (compact ? Math.Min(workArea.Height * 48 / 100, 195) : Math.Min(workArea.Height * 38 / 100, (int)(225 * dpi)));
                    int cardH = Math.Max(isReveal ? 195 : 155, maxCardH);
                    int cardX = (cw - cardW) / 2;
                    int cardY = workArea.Y + 4;

                    mainCard.HeaderHeight = compact ? 42 : 52;
                    mainCard.FooterHeight = compact ? 40 : 48;
                    mainCard.SetBounds(cardX, cardY, cardW, cardH);

                    // Reserve bottom portion for the live combat arena preview / sandbox
                    int arenaTop = compact && isReveal ? workArea.Y : mainCard.Bottom + 2;
                    int arenaH = Math.Max(140, ch - arenaTop);
                    arena.PlayArea = new Rectangle(0, arenaTop, cw, arenaH);
                }
                // 4. Full-Screen Combat (Phase.Input or attackPresentation)
                else if (screenMode == ScreenMode.Campaign && game != null && (game.Phase == Phase.Input || attackPresentation))
                {
                    arena.PlayArea = workArea;
                }
                // 5. Narrative, Tutorial 1-2, Diary, Recall, Results, Map, GameOver, Victory
                else if (currentCardLayout != null)
                {
                    bool isMap = screenMode == ScreenMode.Campaign && game != null && game.Phase == Phase.Map;
                    bool isRecall = screenMode == ScreenMode.Campaign && game != null && game.Phase == Phase.Recall;
                    int maxW = isMap ? (int)(920 * dpi) : (int)(760 * dpi);
                    int cardW = Math.Min(cw - 16, Math.Max(460, Math.Min(maxW, cw * 84 / 100)));

                    int targetH = isRecall
                        ? (int)((210 + game.Level.Facts.Length * 68) * dpi)
                        : isMap ? (int)(480 * dpi) : (int)(410 * dpi);
                    int cardH = Math.Min(workArea.Height - 12, Math.Max(270, targetH));

                    mainCard.HeaderHeight = compact ? 44 : 56;
                    mainCard.FooterHeight = compact ? 42 : 52;
                    mainCard.SetBounds((cw - cardW) / 2, workArea.Y + (workArea.Height - cardH) / 2, cardW, cardH);
                    arena.PlayArea = workArea;
                }

                if (currentCardLayout != null)
                    currentCardLayout();

                // 6. Modal Dialog Overlay
                if (currentModalLayout != null)
                {
                    int mw = Math.Min(cw - 20, Math.Max(420, Math.Min((int)(590 * dpi), cw * 74 / 100)));
                    int mh = Math.Min(ch - 20, Math.Max(300, Math.Min((int)(385 * dpi), ch * 80 / 100)));
                    modalCard.HeaderHeight = compact ? 44 : 52;
                    modalCard.FooterHeight = compact ? 42 : 50;
                    modalCard.SetBounds((cw - mw) / 2, (ch - mh) / 2, mw, mh);
                    currentModalLayout();
                }

                arena.Invalidate();
            }
            finally
            {
                arranging = false;
            }
        }

        private void ToggleFullScreen()
        {
            SuspendLayout();
            if (!fullScreen)
            {
                windowState = WindowState;
                windowBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                var monitor = Screen.FromControl(this);
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                Bounds = monitor.Bounds;
                fullScreen = true;
            }
            else
            {
                fullScreen = false;
                FormBorderStyle = FormBorderStyle.Sizable;
                WindowState = FormWindowState.Normal;
                Bounds = windowBounds;
                WindowState = windowState;
            }
            ResumeLayout();
            Arrange();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            bool repeated = (msg.LParam.ToInt64() & (1L << 30)) != 0;

            if (keyData == Keys.F11 || keyData == (Keys.Alt | Keys.Enter))
            {
                if (!repeated) ToggleFullScreen();
                return true;
            }

            if (keyData == Keys.Escape)
            {
                if (fullScreen)
                {
                    if (!repeated) ToggleFullScreen();
                    return true;
                }
                if (modalCard.Visible)
                {
                    if (!repeated && modalCancelAction != null) modalCancelAction();
                    return true;
                }
                if (screenMode == ScreenMode.Campaign && game != null && !repeated)
                {
                    ShowOptionsModal(true);
                    return true;
                }
            }

            // When a modal window is open, block all combat/practice position keys so no accidental move occurs!
            if (modalCard.Visible)
                return HandleDialogNavigationSound(ref msg, keyData);

            // Isolated Tutorial Practice Sandbox (Step 3)
            if (screenMode == ScreenMode.Tutorial && tutorialStep == 2)
            {
                Lane? tutLane = MapKeyToLane(keyData);
                if (tutLane.HasValue)
                {
                    if (!repeated) SelectTutorialLane(tutLane.Value);
                    return true;
                }
                if (keyData == Keys.Enter && !(ActiveControl is NauticalButton))
                {
                    if (!repeated) ConfirmTutorialMove();
                    return true;
                }
            }

            // Campaign Combat Input (choose position with A/S/D or Arrows, confirm with Enter)
            if (screenMode == ScreenMode.Campaign && (game?.Phase == Phase.Input || attackPresentation))
            {
                Lane? lane = MapKeyToLane(keyData);
                if (lane.HasValue)
                {
                    if (!repeated && !attackPresentation && game?.Phase == Phase.Input)
                        SelectCampaignLane(lane.Value);
                    return true;
                }
                if (keyData == Keys.Enter)
                {
                    if (!repeated && !attackPresentation && game?.Phase == Phase.Input)
                        MoveAndAttack(arena.PlayerLane);
                    return true;
                }
            }

            return HandleDialogNavigationSound(ref msg, keyData);
        }

        private bool HandleDialogNavigationSound(ref Message msg, Keys keyData)
        {
            Control prevFocus = ActiveControl;
            bool handled = base.ProcessCmdKey(ref msg, keyData);
            if ((keyData & Keys.KeyCode) == Keys.Tab && ActiveControl != prevFocus && ActiveControl is NauticalButton)
                audio.PlayUiHover();
            return handled;
        }

        private static Lane? MapKeyToLane(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.A:
                case Keys.Left:
                    return Lane.Left;
                case Keys.D:
                case Keys.Right:
                    return Lane.Right;
                case Keys.S:
                case Keys.Down:
                    return Lane.Center;
                default:
                    return null;
            }
        }
    }

    // =========================================================================
    // SUB-COMPONENTES TEMÁTICOS DE APRESENTAÇÃO
    // =========================================================================

    // Main Menu Title Banner in weathered wood, brass, and coastal emblem
    internal sealed class TitleBannerControl : Control
    {
        public TitleBannerControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = CoastalTheme.WoodBase;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
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
            CoastalTheme.DrawWoodFrame(g, new Rectangle(0, 0, Width, Height), false, false, true);

            bool compact = Height < 92 || Width < 500;
            var titleFont = CoastalTheme.TitleFont(compact ? 14.5f : 23f, FontStyle.Bold);
            var subFont = CoastalTheme.BodyFont(compact ? 8.5f : 10.5f, FontStyle.Bold);

            string titleText = "FISHERMAN'S MEMORY";
            string subText = "Uma ilha guarda suas histórias. O mar tenta apagá-las.";
            Size titleSize = TextRenderer.MeasureText(g, titleText, titleFont, Size.Empty, TextFormatFlags.NoPrefix);
            Size subSize = TextRenderer.MeasureText(g, subText, subFont, Size.Empty, TextFormatFlags.NoPrefix);

            Image logo = CoastalTheme.GetLogoImage();
            int logoSz = logo != null ? (compact ? Math.Max(46, Height - 22) : Math.Max(68, Height - 28)) : 0;
            int gap = logo != null ? (compact ? 10 : 16) : 0;
            int textW = Math.Min(Width - logoSz - gap - 36, Math.Max(titleSize.Width, subSize.Width) + 8);
            int groupW = logoSz + gap + textW;
            int startX = Math.Max(16, (Width - groupW) / 2);

            if (logo != null)
            {
                Rectangle logoRect = new Rectangle(startX, (Height - logoSz) / 2, logoSz, logoSz);
                var oldInterp = g.InterpolationMode;
                var oldOffset = g.PixelOffsetMode;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(logo, logoRect);
                g.InterpolationMode = oldInterp;
                g.PixelOffsetMode = oldOffset;
            }

            int textX = startX + logoSz + gap;
            Rectangle topHalf = new Rectangle(textX, compact ? 8 : 14, textW, Height * 46 / 100);
            Rectangle botHalf = new Rectangle(textX, Height * 52 / 100, textW, Height * 36 / 100);

            TextRenderer.DrawText(g, titleText, titleFont, new Rectangle(topHalf.X + 2, topHalf.Y + 2, topHalf.Width, topHalf.Height),
                CoastalTheme.WoodOutline, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, titleText, titleFont, topHalf,
                CoastalTheme.BrassBright, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(g, subText, subFont, botHalf,
                CoastalTheme.SeafoamLight, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    // Expedition Diary / Narrative Page Control (with zero highlighting of answer words)
    internal sealed class NarrativePageControl : Control
    {
        private readonly string heading;
        private readonly string passage;
        private readonly string footerNote;

        public NarrativePageControl(string headingText, string bodyText, string noteText)
        {
            heading = headingText ?? "";
            passage = bodyText ?? "";
            footerNote = noteText ?? "";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = CoastalTheme.ParchmentLight;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            CoastalTheme.DrawParchmentSurface(g, new Rectangle(0, 0, Width, Height), true);

            bool compact = Height < 210 || Width < 480;
            int padX = compact ? 16 : 24;
            int padY = compact ? 8 : 14;

            int noteH = string.IsNullOrEmpty(footerNote) ? 0 : (compact ? 42 : 52);
            if (noteH > 0)
            {
                Rectangle noteRect = new Rectangle(12, Height - noteH - 8, Width - 24, noteH);
                CoastalTheme.DrawSubCard(g, noteRect, CoastalTheme.ParchmentMid, CoastalTheme.ParchmentBorder, CoastalTheme.BrassGold);
                var noteFont = CoastalTheme.BodyFont(compact ? 8f : 9f, FontStyle.Bold);
                TextRenderer.DrawText(g, footerNote, noteFont, Rectangle.Inflate(noteRect, -10, -4),
                    CoastalTheme.InkMedium, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            }

            int y = padY;
            if (!string.IsNullOrEmpty(heading))
            {
                var headFont = CoastalTheme.TitleFont(compact ? 10.5f : 12.5f, FontStyle.Bold);
                Rectangle hRect = new Rectangle(padX, y, Width - padX * 2, compact ? 20 : 26);
                TextRenderer.DrawText(g, heading, headFont, hRect, CoastalTheme.WoodBase,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                y += hRect.Height + 3;

                using (var rulePen = new Pen(CoastalTheme.ParchmentBorder, 1))
                    g.DrawLine(rulePen, padX, y, Width - padX, y);
                y += 6;
            }

            int bodyAvailH = Math.Max(40, Height - y - noteH - 14);
            Rectangle bodyRect = new Rectangle(padX, y, Width - padX * 2, bodyAvailH);

            // Adaptive font size so text never clips even in 640x480 or compact modals
            float bodyPt = compact ? 10f : 11.5f;
            var bodyFont = CoastalTheme.BodyFont(bodyPt, FontStyle.Regular);
            while (bodyPt > 8.5f)
            {
                Size measured = TextRenderer.MeasureText(g, passage, bodyFont, new Size(bodyRect.Width, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                if (measured.Height <= bodyRect.Height) break;
                bodyPt -= 0.5f;
                bodyFont = CoastalTheme.BodyFont(bodyPt, FontStyle.Regular);
            }

            TextRenderer.DrawText(g, passage, bodyFont, bodyRect, CoastalTheme.InkDark,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }

    // Guided Tutorial Visual Step Card (Steps 1 & 2)
    internal sealed class TutorialStepInfoControl : Control
    {
        private readonly int stepIndex;

        public TutorialStepInfoControl(int step)
        {
            stepIndex = step;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = CoastalTheme.ParchmentLight;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            CoastalTheme.DrawParchmentSurface(g, new Rectangle(0, 0, Width, Height), false);

            bool compact = Height < 220 || Width < 520;
            int pad = compact ? 10 : 16;

            if (stepIndex == 0)
            {
                // Step 1: 3 clean, roomy cards explaining the core 3-step loop
                string[] titles =
                {
                    "1. LEIA O DIÁRIO DA FASE",
                    "2. OBSERVE OS SINAIS DO GUARDIÃO",
                    "3. POSICIONE, CONFIRME E RESPONDA"
                };
                string[] bodies =
                {
                    "Cada fase traz uma informação para memorizar. Guarde os detalhes — eles serão cobrados após o combate.",
                    "O guardião mostra a sequência das teclas seguras antes da luta. Observe sem pressa e clique em Iniciar combate.",
                    "Escolha a posição (A/S/D ou Setas) e confirme com Enter ou no botão Confirmar. Ao final, recupere a memória para avançar."
                };

                int cardGap = compact ? 6 : 10;
                int cardH = Math.Max(42, (Height - pad * 2 - cardGap * 2) / 3);
                var tFont = CoastalTheme.TitleFont(compact ? 9f : 10.5f, FontStyle.Bold);
                var bFont = CoastalTheme.BodyFont(compact ? 8.5f : 10f, FontStyle.Regular);

                for (int i = 0; i < 3; i++)
                {
                    Rectangle cRect = new Rectangle(pad, pad + i * (cardH + cardGap), Width - pad * 2, cardH);
                    CoastalTheme.DrawSubCard(g, cRect, CoastalTheme.ParchmentMid, CoastalTheme.ParchmentBorder, CoastalTheme.WoodBase);
                    TextRenderer.DrawText(g, titles[i], tFont, new Rectangle(cRect.X + 12, cRect.Y + 4, cRect.Width - 24, 20),
                        CoastalTheme.WoodBase, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    TextRenderer.DrawText(g, bodies[i], bFont, new Rectangle(cRect.X + 12, cRect.Y + 24, cRect.Width - 24, cRect.Height - 28),
                        CoastalTheme.InkDark, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                }
            }
            else
            {
                // Step 2: Clean 3-position layout on the left + combat/HP rules on the right
                int leftW = Width * 44 / 100;
                Rectangle leftCol = new Rectangle(pad, pad, leftW - pad, Height - pad * 2);
                Rectangle rightCol = new Rectangle(leftCol.Right + 10, pad, Width - leftCol.Right - pad - 10, Height - pad * 2);

                CoastalTheme.DrawSubCard(g, leftCol, CoastalTheme.OceanDeep, CoastalTheme.WoodBase, CoastalTheme.BrassGold);
                var diagTitle = CoastalTheme.TitleFont(compact ? 8.5f : 10f, FontStyle.Bold);
                TextRenderer.DrawText(g, "POSIÇÕES E CONTROLES", diagTitle,
                    new Rectangle(leftCol.X + 6, leftCol.Y + 6, leftCol.Width - 12, 22),
                    CoastalTheme.BrassBright, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                int cx = leftCol.X + leftCol.Width / 2;
                int cy = leftCol.Y + leftCol.Height / 2 + 2;
                int ox = Math.Min(78, leftCol.Width * 31 / 100);
                int oy = Math.Min(30, leftCol.Height * 14 / 100);

                DrawPositionNode(g, cx - ox, cy - oy / 2, "A ←", "ESQ.", false, compact);
                DrawPositionNode(g, cx, cy + oy, "S ↓", "CENTRO*", true, compact);
                DrawPositionNode(g, cx + ox, cy - oy / 2, "D →", "DIR.", false, compact);

                var noteFont = CoastalTheme.BodyFont(compact ? 7.5f : 8.5f, FontStyle.Regular);
                TextRenderer.DrawText(g, "*Fase 1: apenas Esq./Dir. • Enter confirma", noteFont,
                    new Rectangle(leftCol.X + 8, leftCol.Bottom - 28, leftCol.Width - 16, 22),
                    CoastalTheme.SeafoamLight, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                CoastalTheme.DrawSubCard(g, rightCol, CoastalTheme.ParchmentMid, CoastalTheme.WoodBase, CoastalTheme.SafeGreenDark);
                var hFont = CoastalTheme.TitleFont(compact ? 9f : 10.5f, FontStyle.Bold);
                var bodyFont = CoastalTheme.BodyFont(compact ? 8.5f : 9.5f, FontStyle.Regular);

                TextRenderer.DrawText(g, "REGRAS ESSENCIAIS", hFont,
                    new Rectangle(rightCol.X + 12, rightCol.Y + 8, rightCol.Width - 24, 22),
                    CoastalTheme.SafeGreenDark, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                string rulesRight =
                    "• Escolha com A/S/D ou Setas e confirme com Enter (sem pressa).\n\n" +
                    "• Acerto dá +100 pts. Erro causa dano (−20 HP, −30 no Boss) e −50 pts.\n\n" +
                    "• Combo ×3: 3 acertos seguidos curam +50% do HP (máx. 100) e dão +150 pts.";

                TextRenderer.DrawText(g, rulesRight, bodyFont,
                    new Rectangle(rightCol.X + 12, rightCol.Y + 36, rightCol.Width - 24, rightCol.Height - 44),
                    CoastalTheme.InkDark, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            }
        }

        private static void DrawPositionNode(Graphics g, int cx, int cy, string key, string dir, bool highlight, bool compact)
        {
            int w = compact ? 58 : 70;
            int h = compact ? 32 : 38;
            Rectangle box = new Rectangle(cx - w / 2, cy - h / 2, w, h);
            CoastalTheme.DrawSubCard(g, box,
                highlight ? CoastalTheme.SafeGreenDark : CoastalTheme.OceanMid,
                highlight ? CoastalTheme.SafeGreenBright : CoastalTheme.BrassGold);

            var kf = CoastalTheme.TitleFont(compact ? 8.5f : 10f, FontStyle.Bold);
            var df = CoastalTheme.BodyFont(compact ? 7f : 8f, FontStyle.Bold);
            TextRenderer.DrawText(g, key, kf, new Rectangle(box.X, box.Y + 2, box.Width, box.Height * 55 / 100),
                Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, dir, df, new Rectangle(box.X, box.Y + box.Height * 52 / 100, box.Width, box.Height * 45 / 100),
                highlight ? CoastalTheme.SafeGreenBright : CoastalTheme.BrassBright,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    // Guided Tutorial Step 3: Isolated Live Practice Control
    internal sealed class TutorialPracticeBannerControl : Control
    {
        private IReadOnlyList<Lane> sequence;
        private int currentStep;
        private string feedbackText;

        public TutorialPracticeBannerControl(IReadOnlyList<Lane> seq, int step, string feedback)
        {
            sequence = seq;
            currentStep = step;
            feedbackText = feedback;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = CoastalTheme.ParchmentMid;
        }

        public void UpdateState(IReadOnlyList<Lane> seq, int step, string feedback)
        {
            sequence = seq;
            currentStep = step;
            feedbackText = feedback;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            CoastalTheme.DrawParchmentSurface(g, new Rectangle(0, 0, Width, Height), false);

            bool compact = Height < 90 || Width < 520;
            int pad = 6;
            int topH = Math.Max(38, Height * 56 / 100);

            // Top row: 4 sequence steps cleanly centered across the top (no duplicate key buttons)
            int count = sequence != null ? sequence.Count : 0;
            if (count > 0)
            {
                int gap = 8;
                int cardW = (Width - pad * 2 - gap * (count - 1)) / count;
                for (int i = 0; i < count; i++)
                {
                    bool isCurrent = i == currentStep;
                    bool isDone = i < currentStep;
                    Lane l = sequence[i];
                    Rectangle cRect = new Rectangle(pad + i * (cardW + gap), pad, cardW, topH - pad);

                    Color bg = isDone ? CoastalTheme.SafeGreenDark :
                               isCurrent ? CoastalTheme.WoodBase : CoastalTheme.OceanDeep;
                    Color border = isCurrent ? CoastalTheme.BrassBright :
                                   isDone ? CoastalTheme.SafeGreen : CoastalTheme.OceanBorder;

                    CoastalTheme.DrawSubCard(g, cRect, bg, CoastalTheme.WoodOutline, border);

                    var sFont = CoastalTheme.TitleFont(compact ? 7.5f : 8.5f, FontStyle.Bold);
                    var kFont = CoastalTheme.TitleFont(compact ? 10.5f : 12.5f, FontStyle.Bold);
                    string topLbl = isDone ? "✓ FEITO" : isCurrent ? "→ AGORA" : ("#" + (i + 1));

                    TextRenderer.DrawText(g, topLbl, sFont, new Rectangle(cRect.X + 2, cRect.Y + 2, cRect.Width - 4, cRect.Height * 42 / 100),
                        isCurrent ? CoastalTheme.BrassBright : Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    TextRenderer.DrawText(g, Levels.Symbol(l) + " " + CoastalTheme.LaneName(l), kFont,
                        new Rectangle(cRect.X + 2, cRect.Y + cRect.Height * 42 / 100, cRect.Width - 4, cRect.Height * 54 / 100),
                        Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
            }

            // Bottom feedback strip
            Rectangle botRect = new Rectangle(pad, topH + 4, Width - pad * 2, Math.Max(22, Height - topH - 8));
            CoastalTheme.DrawSubCard(g, botRect, CoastalTheme.OceanDeep, CoastalTheme.WoodOutline, CoastalTheme.OceanBorder);
            var fbFont = CoastalTheme.BodyFont(compact ? 8.5f : 9.5f, FontStyle.Bold);
            TextRenderer.DrawText(g, feedbackText, fbFont, Rectangle.Inflate(botRect, -8, -2),
                CoastalTheme.SandLight, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    // Status / Validation Banner for Recall screen
    internal sealed class StatusBannerControl : Control
    {
        public string BannerText { get; set; } = "";
        public bool IsError { get; set; }

        public StatusBannerControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Color bg = IsError ? Color.FromArgb(248, 222, 218) : CoastalTheme.ParchmentMid;
            Color border = IsError ? CoastalTheme.DangerCoral : CoastalTheme.ParchmentBorder;
            Color fg = IsError ? CoastalTheme.DangerDark : CoastalTheme.InkMedium;

            CoastalTheme.DrawSubCard(g, new Rectangle(0, 0, Width, Height), bg, border,
                IsError ? (Color?)CoastalTheme.DangerCoral : null);

            int iconSz = Math.Min(20, Height - 6);
            RectangleF iconRect = new RectangleF(8, (Height - iconSz) / 2f, iconSz, iconSz);
            CoastalTheme.DrawNauticalIcon(g, IsError ? NauticalIcon.CrossDanger : NauticalIcon.Compass,
                iconRect, fg, border);

            var font = CoastalTheme.BodyFont(Width < 560 || Height < 32 ? 8f : 9.5f, FontStyle.Bold);
            Rectangle txtRect = new Rectangle((int)iconRect.Right + 6, 0, Width - (int)iconRect.Right - 10, Height);
            TextRenderer.DrawText(g, BannerText, font, txtRect, fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    // Stage Results Report Card (Phase.Results)
    internal sealed class ResultsReportControl : Control
    {
        private readonly int correct, errors, hp, hpBonus, memoryBonus, recallCorrect, recallTotal, stageScore, totalScore;
        private readonly bool fullRecall;

        public ResultsReportControl(GameSession g)
        {
            correct = g.Correct;
            errors = g.Errors;
            hp = g.Hp;
            fullRecall = g.RecallTotal == 0 || g.RecallCorrect == g.RecallTotal;
            recallCorrect = g.RecallTotal > 0 ? g.RecallCorrect : g.Level.Facts.Length;
            recallTotal = g.RecallTotal > 0 ? g.RecallTotal : g.Level.Facts.Length;
            hpBonus = fullRecall ? g.Hp * 5 : 0;
            memoryBonus = g.RecallTotal > 0 ? Math.Max(0, g.RecallBonus - hpBonus) : 500;
            stageScore = g.StageScore;
            totalScore = g.Score;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = CoastalTheme.ParchmentLight;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            CoastalTheme.DrawParchmentSurface(g, new Rectangle(0, 0, Width, Height), false);

            bool compact = Height < 200 || Width < 480;
            int pad = compact ? 12 : 18;
            int gap = compact ? 6 : 10;

            // Top row: 3 Metric Cards [Esquivas Corretas / Erros] [Memória Recuperada] [Bônus de HP]
            int topH = Math.Max(62, Height * 46 / 100);
            int cardW = (Width - pad * 2 - gap * 2) / 3;

            DrawMetricCard(g, new Rectangle(pad, pad, cardW, topH),
                "DESEMPENHO",
                "✓ " + correct + " acertos\n✕ " + errors + " erros",
                NauticalIcon.ShieldCheck, CoastalTheme.SafeGreenDark, compact);

            DrawMetricCard(g, new Rectangle(pad + cardW + gap, pad, cardW, topH),
                "MEMÓRIA",
                fullRecall
                    ? ("+" + memoryBonus + " pts\nDiário completo")
                    : ("+" + memoryBonus + " pts\n" + recallCorrect + "/" + recallTotal + " memórias"),
                NauticalIcon.Journal, fullRecall ? CoastalTheme.WoodBase : CoastalTheme.DangerDark, compact);

            DrawMetricCard(g, new Rectangle(pad + (cardW + gap) * 2, pad, Width - pad * 2 - (cardW + gap) * 2, topH),
                "VITALIDADE",
                fullRecall
                    ? ("HP " + hp + " × 5\n+" + hpBonus + " pts")
                    : ("HP " + hp + "\nSem bônus total"),
                NauticalIcon.Heart, Color.FromArgb(168, 42, 48), compact);

            // Bottom row: Stage Score & Total Accumulated Score Banner
            int botY = pad + topH + gap;
            int botH = Math.Max(54, Height - botY - pad);
            int halfW = (Width - pad * 2 - gap) / 2;

            Rectangle stageBox = new Rectangle(pad, botY, halfW, botH);
            Rectangle totalBox = new Rectangle(pad + halfW + gap, botY, Width - pad * 2 - halfW - gap, botH);

            CoastalTheme.DrawSubCard(g, stageBox, CoastalTheme.ParchmentMid, CoastalTheme.WoodBase, CoastalTheme.BrassGold);
            CoastalTheme.DrawSubCard(g, totalBox, CoastalTheme.OceanDeep, CoastalTheme.WoodOutline, CoastalTheme.BrassBright);

            var lblFont = CoastalTheme.TitleFont(compact ? 8.5f : 10f, FontStyle.Bold);
            var valFont = CoastalTheme.TitleFont(compact ? 14f : 18f, FontStyle.Bold);

            TextRenderer.DrawText(g, "SALDO DESTA FASE", lblFont,
                new Rectangle(stageBox.X + 10, stageBox.Y + 6, stageBox.Width - 20, stageBox.Height * 40 / 100),
                CoastalTheme.InkMedium, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, (stageScore >= 0 ? "+" : "") + stageScore + " PONTOS", valFont,
                new Rectangle(stageBox.X + 10, stageBox.Y + stageBox.Height * 40 / 100, stageBox.Width - 20, stageBox.Height * 55 / 100),
                CoastalTheme.WoodBase, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(g, "PONTUAÇÃO ACUMULADA", lblFont,
                new Rectangle(totalBox.X + 10, totalBox.Y + 6, totalBox.Width - 20, totalBox.Height * 40 / 100),
                CoastalTheme.BrassGold, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, totalScore + " PONTOS", valFont,
                new Rectangle(totalBox.X + 10, totalBox.Y + totalBox.Height * 40 / 100, totalBox.Width - 20, totalBox.Height * 55 / 100),
                CoastalTheme.BrassBright, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        private static void DrawMetricCard(Graphics g, Rectangle r, string title, string valueText, NauticalIcon icon, Color accent, bool compact)
        {
            CoastalTheme.DrawSubCard(g, r, CoastalTheme.ParchmentMid, CoastalTheme.ParchmentBorder, accent);
            var tFont = CoastalTheme.TitleFont(compact ? 8f : 9.5f, FontStyle.Bold);
            var vFont = CoastalTheme.BodyFont(compact ? 9.5f : 11f, FontStyle.Bold);

            int iconSz = compact ? 18 : 24;
            CoastalTheme.DrawNauticalIcon(g, icon, new RectangleF(r.X + 8, r.Y + 6, iconSz, iconSz), accent, CoastalTheme.BrassGold);

            TextRenderer.DrawText(g, title, tFont, new Rectangle(r.X + iconSz + 12, r.Y + 6, r.Width - iconSz - 16, iconSz),
                accent, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(g, valueText, vFont, new Rectangle(r.X + 8, r.Y + iconSz + 10, r.Width - 16, r.Height - iconSz - 14),
                CoastalTheme.InkDark, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }

    // Victory & Defeat Summary Control
    internal sealed class EndScreenSummaryControl : Control
    {
        private readonly bool victory;
        private readonly string storyText;
        private readonly string detailText;
        private readonly int finalScore;
        private readonly int finalHp;
        private readonly string badgeSummary;

        public EndScreenSummaryControl(bool isVictory, string story, string detail, int score, int hp, string summaryLine)
        {
            victory = isVictory;
            storyText = story;
            detailText = detail;
            finalScore = score;
            finalHp = hp;
            badgeSummary = summaryLine;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (victory)
                CoastalTheme.DrawParchmentSurface(g, new Rectangle(0, 0, Width, Height), false);
            else
                CoastalTheme.DrawOceanSurface(g, new Rectangle(0, 0, Width, Height));

            bool compact = Height < 200 || Width < 480;
            int pad = compact ? 14 : 22;

            Color mainFg = victory ? CoastalTheme.InkDark : CoastalTheme.SandLight;
            Color subFg = victory ? CoastalTheme.InkMedium : CoastalTheme.SandMuted;

            var storyFont = CoastalTheme.BodyFont(compact ? 10.5f : 12.5f, FontStyle.Bold);
            var detailFont = CoastalTheme.BodyFont(compact ? 9f : 10.5f, FontStyle.Regular);

            int statBoxH = compact ? 58 : 72;
            Rectangle statRect = new Rectangle(pad, Height - statBoxH - pad, Width - pad * 2, statBoxH);

            int textAvailH = Math.Max(50, statRect.Y - pad - 8);
            Rectangle storyRect = new Rectangle(pad, pad, Width - pad * 2, textAvailH * 60 / 100);
            Rectangle detailRect = new Rectangle(pad, storyRect.Bottom + 4, Width - pad * 2, textAvailH * 40 / 100);

            TextRenderer.DrawText(g, storyText, storyFont, storyRect, mainFg,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, detailText, detailFont, detailRect, subFg,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

            CoastalTheme.DrawSubCard(g, statRect,
                victory ? CoastalTheme.OceanDeep : Color.FromArgb(22, 34, 48),
                CoastalTheme.WoodOutline,
                victory ? CoastalTheme.BrassBright : CoastalTheme.DangerCoral);

            int iconSz = compact ? 28 : 38;
            RectangleF iconBox = new RectangleF(statRect.X + 14, statRect.Y + (statRect.Height - iconSz) / 2f, iconSz, iconSz);
            CoastalTheme.DrawNauticalIcon(g, victory ? NauticalIcon.SunTrophy : NauticalIcon.Compass,
                iconBox, CoastalTheme.BrassBright, victory ? CoastalTheme.Seafoam : CoastalTheme.DangerCoral);

            var scoreFont = CoastalTheme.TitleFont(compact ? 12f : 15f, FontStyle.Bold);
            var sumFont = CoastalTheme.BodyFont(compact ? 8.5f : 10f, FontStyle.Bold);

            int tx = (int)iconBox.Right + 12;
            int tw = Math.Max(60, statRect.Right - tx - 12);

            TextRenderer.DrawText(g, "PONTUAÇÃO FINAL: " + finalScore + " PONTOS" + (victory ? ("  •  HP: " + finalHp + "/100") : ""),
                scoreFont, new Rectangle(tx, statRect.Y + 6, tw, statRect.Height * 50 / 100),
                CoastalTheme.BrassBright, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(g, badgeSummary, sumFont,
                new Rectangle(tx, statRect.Y + statRect.Height * 52 / 100, tw, statRect.Height * 44 / 100),
                CoastalTheme.SeafoamLight, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
