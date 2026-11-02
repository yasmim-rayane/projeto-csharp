using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Media;

namespace FishermansMemory.Infrastructure
{
// SoundPlayer reproduz WAV, sem mixer. Dano interrompe a música, retomada na próxima ação.
public sealed class AudioService : IDisposable
{
    private SoundPlayer music, damage;
    public bool Enabled { get; set; } = true;
    public AudioService()
    {
        music = Load("ocean.wav"); damage = Load("damage.wav");
    }
    private static SoundPlayer Load(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", name);
        if (!File.Exists(path)) return null;
        var player = new SoundPlayer(path);
        try { player.Load(); return player; }
        catch (Exception e) when (e is IOException || e is InvalidOperationException || e is TimeoutException)
        { player.Dispose(); return null; }
    }
    public void Music() { if (Enabled) music?.PlayLooping(); }
    public void Damage() { if (Enabled) { music?.Stop(); damage?.Play(); } }
    public void Stop() { music?.Stop(); damage?.Stop(); }
    public void Dispose() { Stop(); music?.Dispose(); damage?.Dispose(); }
}

}
