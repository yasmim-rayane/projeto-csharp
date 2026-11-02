using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FishermansMemory.Core;

namespace FishermansMemory.Infrastructure
{
public sealed class SaveService
{
    private readonly string path;
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions()
    { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public SaveService(string directory = null)
    {
        path = Path.Combine(directory ?? Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "FishermansMemory"), "save-v3.json");
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
}

}
