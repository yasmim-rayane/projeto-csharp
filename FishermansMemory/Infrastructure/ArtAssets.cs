using System;
using FishermansMemory.Core;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;

namespace FishermansMemory.Infrastructure
{
    public sealed class ArtFrame
    {
        public string Sheet { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Transparent { get; set; }
    }
    // Pranchas intactas no disco; recortes e máscaras são criados uma vez em memória.
    public sealed class ArtAssets : IDisposable
    {
        private readonly Dictionary<string, Bitmap[]> clips = new Dictionary<string, Bitmap[]>();
        public string Error { get; private set; }
        public bool Available { get { return Error == null; } }
        public ArtAssets()
        {
            var sheets = new Dictionary<string, Bitmap>();
            try
            {
                string directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Art");
                var manifest = JsonSerializer.Deserialize<Dictionary<string, ArtFrame[]>>(
                    File.ReadAllText(Path.Combine(directory, "atlas.json")));
                if (manifest == null) throw new InvalidDataException("Atlas vazio.");
                foreach (var clip in manifest)
                {
                    if (clip.Value == null || clip.Value.Length == 0) throw new InvalidDataException("Animação vazia: " + clip.Key);
                    var frames = new Bitmap[clip.Value.Length]; clips.Add(clip.Key, frames);
                    for (int i = 0; i < frames.Length; i++)
                    {
                        var f = clip.Value[i];
                        if (Path.GetFileName(f.Sheet) != f.Sheet) throw new InvalidDataException("Nome de prancha inválido.");
                        Bitmap sheet;
                        if (!sheets.TryGetValue(f.Sheet, out sheet))
                        {
                            using (var original = Image.FromFile(Path.Combine(directory, f.Sheet))) sheet = new Bitmap(original);
                            sheets.Add(f.Sheet, sheet);
                        }
                        if (f.X < 0 || f.Y < 0 || f.Width <= 0 || f.Height <= 0 || f.X + f.Width > sheet.Width || f.Y + f.Height > sheet.Height)
                            throw new InvalidDataException("Recorte fora da prancha: " + clip.Key);
                        frames[i] = sheet.Clone(new Rectangle(f.X, f.Y, f.Width, f.Height), PixelFormat.Format32bppArgb);
                        if (f.Transparent) MaskBorder(frames[i]);
                    }
                }
                foreach (string key in new[] { "menu", "victory", "boss", "hero.idle", "hero.walk", "hero.hurt", "hero.dead", "splash", "deck" })
                    if (!clips.ContainsKey(key)) throw new InvalidDataException("Recorte ausente: " + key);
                for (int i = 0; i < Levels.All.Count; i++)
                    if (!clips.ContainsKey("scene." + i) || !clips.ContainsKey("enemy." + i) || !clips.ContainsKey("enemy." + i + ".idle") || !clips.ContainsKey("enemy." + i + ".attack"))
                        throw new InvalidDataException("Arte da fase ausente: " + i);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is JsonException || e is OutOfMemoryException)
            { Error = "Arte indisponível: " + e.Message; Dispose(); }
            finally { foreach (var sheet in sheets.Values) sheet.Dispose(); }
        }
        public Bitmap Frame(string name, int index)
        {
            Bitmap[] frames;
            return clips.TryGetValue(name, out frames) ? frames[Math.Abs(index % frames.Length)] : null;
        }
        private static void MaskBorder(Bitmap bitmap)
        {
            int w = bitmap.Width, h = bitmap.Height;
            var visited = new bool[w * h]; var queue = new Queue<int>();
            for (int x = 0; x < w; x++) { queue.Enqueue(x); queue.Enqueue((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { queue.Enqueue(y * w); queue.Enqueue(y * w + w - 1); }
            while (queue.Count > 0)
            {
                int n = queue.Dequeue(); if (visited[n]) continue; visited[n] = true;
                int x = n % w, y = n / w; Color c = bitmap.GetPixel(x, y);
                // Apenas o fundo azul-escuro conectado à borda; não elimina cores interiores.
                if (!(c.R < 26 && c.G < 36 && c.B < 48 && c.B >= c.R + 6 && c.G >= c.R + 5)) continue;
                bitmap.SetPixel(x, y, Color.Transparent);
                if (x > 0) queue.Enqueue(n - 1); if (x + 1 < w) queue.Enqueue(n + 1);
                if (y > 0) queue.Enqueue(n - w); if (y + 1 < h) queue.Enqueue(n + w);
            }
        }
        public void Dispose()
        {
            foreach (var frames in clips.Values) foreach (var frame in frames) if (frame != null) frame.Dispose();
            clips.Clear();
        }
    }
}
