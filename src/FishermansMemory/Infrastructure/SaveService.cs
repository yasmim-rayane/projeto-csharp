using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FishermansMemory.Core;

namespace FishermansMemory.Infrastructure
{
    public sealed class GameSettings
    {
        public bool AudioEnabled { get; set; } = true;
        public bool MotionEnabled { get; set; } = true;
        public int BestScore { get; set; }
    }

    public sealed class SaveService
    {
        private readonly string path;
        private readonly string settingsPath;
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions()
        { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

        public SaveService(string directory = null)
        {
            string dir = directory ?? Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData), "FishermansMemory");
            path = Path.Combine(dir, "save-v3.json");
            settingsPath = Path.Combine(dir, "settings.json");
        }

        public bool Exists => File.Exists(path);

        public GameSession Load()
        {
            var d = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path), Options)
                ?? throw new InvalidDataException("Arquivo de progresso vazio.");
            return GameSession.Restore(d);
        }

        public void Save(GameSession game)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, game.Snapshot(), Options);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }

        public GameSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(settingsPath)) return new GameSettings();
                var loaded = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(settingsPath), Options);
                return loaded ?? new GameSettings();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException || e is NotSupportedException)
            {
                return new GameSettings();
            }
        }

        public void SaveSettings(GameSettings settings)
        {
            if (settings == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                string temp = settingsPath + ".tmp";
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, settings, Options);
                    stream.Flush(true);
                }
                if (File.Exists(settingsPath)) File.Replace(temp, settingsPath, settingsPath + ".bak");
                else File.Move(temp, settingsPath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Não interrompe a partida caso o disco esteja somente leitura
            }
        }
    }
}
