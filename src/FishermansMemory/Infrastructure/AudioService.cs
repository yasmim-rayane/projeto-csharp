using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;

namespace FishermansMemory.Infrastructure
{
    public enum MusicTrack
    {
        None,
        MenuCalm,
        OceanAmbient,
        CombatBattle,
        BossBattle,
        Victory,
        GameOver
    }

    // Motor de áudio via Windows MCI (mpegvideo) permitindo música em loop contínuo,
    // camada ambiente de mar/gaivotas e efeitos sonoros simultâneos sem interromper a trilha.
    public sealed class AudioService : IDisposable
    {
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern int mciSendString(string command, StringBuilder buffer, int bufferSize, IntPtr hwndCallback);

        private const string BgmAlias = "fm_bgm";
        private const string AmbAlias = "fm_amb";

        private readonly string audioDir;
        private readonly Dictionary<string, string> filePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly SoundPlayer fallbackDamage;
        private bool enabled = true;
        private bool disposed;
        private int sfxCounter;
        private MusicTrack currentTrack = MusicTrack.None;
        private bool bgmOpen;
        private bool ambOpen;

        public bool Enabled
        {
            get { return enabled; }
            set
            {
                if (enabled == value) return;
                enabled = value;
                if (!enabled) StopPlaybackOnly();
            }
        }

        public MusicTrack CurrentTrack => currentTrack;

        public AudioService()
        {
            audioDir = ResolveAudioDir();
            RegisterFile("ocean.wav");
            RegisterFile("menu_calm.wav");
            RegisterFile("combat_battle.wav");
            RegisterFile("boss_battle.wav");
            RegisterFile("victory.wav");
            RegisterFile("gameover.wav");
            RegisterFile("hit.wav");
            RegisterFile("combo.wav");
            RegisterFile("damage.wav");
            RegisterFile("ui_hover.wav");
            RegisterFile("ui_select.wav");

            fallbackDamage = LoadFallbackPlayer("damage.wav");
        }

        private static string ResolveAudioDir()
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "Assets", "Audio");
            if (Directory.Exists(dir)) return dir;
            string asmDir = Path.GetDirectoryName(typeof(AudioService).Assembly.Location) ?? AppContext.BaseDirectory;
            string asmAudio = Path.Combine(asmDir, "Assets", "Audio");
            if (Directory.Exists(asmAudio)) return asmAudio;
            return Path.Combine(AppContext.BaseDirectory, "Assets");
        }

        private void RegisterFile(string fileName)
        {
            string p = Path.Combine(audioDir, fileName);
            if (!File.Exists(p))
            {
                string legacy = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
                if (File.Exists(legacy)) p = legacy;
            }
            if (File.Exists(p)) filePaths[fileName] = p;
        }

        private SoundPlayer LoadFallbackPlayer(string name)
        {
            string path;
            if (!filePaths.TryGetValue(name, out path)) return null;
            var player = new SoundPlayer(path);
            try { player.Load(); return player; }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is TimeoutException)
            { player.Dispose(); return null; }
        }

        private static bool Mci(string command)
        {
            try
            {
                var sb = new StringBuilder(128);
                return mciSendString(command, sb, sb.Capacity, IntPtr.Zero) == 0;
            }
            catch
            {
                return false;
            }
        }

        private static string Quote(string path) => "\"" + path + "\"";

        public void PlayTrack(MusicTrack track)
        {
            if (disposed) return;
            if (track == currentTrack && (!enabled || bgmOpen || ambOpen)) return;
            currentTrack = track;
            if (!enabled) return;

            StopPlaybackOnly();
            StartTrackInternal(track);
        }

        public void ResumeCurrentTrack()
        {
            if (disposed || !enabled || currentTrack == MusicTrack.None) return;
            if (!bgmOpen && !ambOpen)
                StartTrackInternal(currentTrack);
        }

        private void StartTrackInternal(MusicTrack track)
        {
            if (track == MusicTrack.None) return;

            string mainFile;
            bool loopMain = true;
            int mainVol = 520;
            bool withOceanLayer = false;
            int oceanVol = 220;

            switch (track)
            {
                case MusicTrack.MenuCalm:
                    mainFile = "menu_calm.wav";
                    mainVol = 540;
                    break;
                case MusicTrack.OceanAmbient:
                    mainFile = "ocean.wav";
                    mainVol = 480;
                    break;
                case MusicTrack.CombatBattle:
                    mainFile = "combat_battle.wav";
                    mainVol = 480;
                    withOceanLayer = true;
                    oceanVol = 160;
                    break;
                case MusicTrack.BossBattle:
                    mainFile = "boss_battle.wav";
                    mainVol = 500;
                    withOceanLayer = true;
                    oceanVol = 180;
                    break;
                case MusicTrack.Victory:
                    mainFile = "victory.wav";
                    loopMain = false;
                    mainVol = 580;
                    withOceanLayer = true;
                    oceanVol = 260;
                    break;
                case MusicTrack.GameOver:
                    mainFile = "gameover.wav";
                    loopMain = false;
                    mainVol = 540;
                    withOceanLayer = true;
                    oceanVol = 280;
                    break;
                default:
                    return;
            }

            string path;
            if (filePaths.TryGetValue(mainFile, out path))
            {
                if (Mci("open " + Quote(path) + " type mpegvideo alias " + BgmAlias))
                {
                    bgmOpen = true;
                    Mci("setaudio " + BgmAlias + " volume to " + mainVol);
                    Mci("play " + BgmAlias + (loopMain ? " repeat" : " from 0"));
                }
            }

            if (withOceanLayer && filePaths.TryGetValue("ocean.wav", out string ambPath))
            {
                if (Mci("open " + Quote(ambPath) + " type mpegvideo alias " + AmbAlias))
                {
                    ambOpen = true;
                    Mci("setaudio " + AmbAlias + " volume to " + oceanVol);
                    Mci("play " + AmbAlias + " repeat");
                }
            }
        }

        public void Music()
        {
            if (currentTrack == MusicTrack.None) PlayTrack(MusicTrack.MenuCalm);
            else ResumeCurrentTrack();
        }

        public void PlayHit() => PlaySfx("hit.wav", 680, false);
        public void PlayCombo() => PlaySfx("combo.wav", 780, false);
        public void PlayUiHover() => PlaySfx("ui_hover.wav", 360, false);
        public void PlayUiSelect() => PlaySfx("ui_select.wav", 520, false);
        public void Damage() => PlaySfx("damage.wav", 820, true);

        private void PlaySfx(string fileName, int volume, bool allowFallback)
        {
            if (disposed || !enabled) return;
            string path;
            if (!filePaths.TryGetValue(fileName, out path)) return;

            int slot = (sfxCounter++ % 6);
            string alias = "fm_sfx_" + slot;
            Mci("close " + alias);
            if (Mci("open " + Quote(path) + " type mpegvideo alias " + alias))
            {
                Mci("setaudio " + alias + " volume to " + volume);
                Mci("play " + alias + " from 0");
            }
            else if (allowFallback && fallbackDamage != null)
            {
                fallbackDamage.Play();
            }
        }

        private void StopPlaybackOnly()
        {
            if (bgmOpen) { Mci("stop " + BgmAlias); Mci("close " + BgmAlias); bgmOpen = false; }
            if (ambOpen) { Mci("stop " + AmbAlias); Mci("close " + AmbAlias); ambOpen = false; }
            for (int i = 0; i < 6; i++) Mci("close fm_sfx_" + i);
            fallbackDamage?.Stop();
        }

        public void Stop()
        {
            currentTrack = MusicTrack.None;
            StopPlaybackOnly();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Stop();
            fallbackDamage?.Dispose();
        }
    }
}
