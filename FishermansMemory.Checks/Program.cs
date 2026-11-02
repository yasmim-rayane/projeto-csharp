using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using FishermansMemory.Core;
using FishermansMemory.Infrastructure;

namespace FishermansMemory.Checks
{
    internal static class Program
    {
        private static int count;
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            count++; Console.WriteLine("OK: " + message);
        }
        private static GameSession Ready(int stage, int hp = 100)
        {
            var g = GameSession.Restore(new SaveData { LevelIndex = stage, Hp = hp });
            g.Reveal(); g.HidePattern(); return g;
        }
        private static AttackResult Hit(GameSession g) { return g.SubmitMove(g.Pattern[g.Resolved]); }
        private static AttackResult Miss(GameSession g)
        { return g.SubmitMove(Levels.Lanes(g.Level).First(x => x != g.Pattern[g.Resolved])); }
        private static void Main()
        {
            using (var art = new ArtAssets())
            {
                Check(art.Available, "Atlas: " + (art.Error ?? "OK"));
                for (int i = 0; i < Levels.All.Count; i++)
                    Check(art.Frame("scene." + i, 0) != null && art.Frame("enemy." + i, 0) != null, "Arte da fase " + i);
                Check(art.Frame("hero.idle", 0) == art.Frame("hero.idle", 1), "Pose do protagonista é estática");
                for (int i = 0; i < Levels.All.Count; i++)
                {
                    Check(art.Frame("enemy." + i + ".idle", 0) != null, "Ociosidade disponível: " + i);
                    Check(art.Frame("enemy." + i + ".attack", 0) != null, "Ataque disponível: " + i);
                }
                Check(art.Frame("enemy.0.idle", 0) != art.Frame("enemy.0.idle", 1), "Caranguejo tem poses diferentes");
                Check(art.Frame("enemy.1.attack", 0) != art.Frame("enemy.1.attack", 1), "Tubarão tem poses de ataque diferentes");
            }
            Check(Levels.All.Count == 3 && Levels.All.Select(x => x.Attacks).SequenceEqual(new[] { 4, 6, 8 }), "Campanha de três fases");
            Check(Levels.All.Select(x => x.Facts.Length).SequenceEqual(new[] { 1, 2, 3 }), "Complexidade de memória 1/2/3");
            foreach (var l in Levels.All) Check(Levels.Lanes(l).Length == 4, "Quatro posições WASD");
            foreach (var level in Levels.All)
            {
                Check(level.Passage.Length >= 250, "Memória apresentada como texto contextual");
                foreach (var fact in level.Facts)
                    Check(System.Globalization.CultureInfo.GetCultureInfo("pt-BR").CompareInfo.IndexOf(
                        level.Passage, fact.Answer, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0,
                        "Resposta presente no texto: " + fact.Question);
            }
            var game = Ready(0, 40);
            Hit(game);
            Check(game.Resolved == 1 && game.Input.Count == 1 && game.Score == 100 && game.Phase == Phase.Input,
                "Um input resolve e pontua imediatamente, sem confirmação");
            Hit(game); Hit(game);
            Check(game.Hp == 90 && game.Score == 450 && game.Combo == 0, "Combo cura 50 e pontua 150");
            Hit(game);
            Check(game.Hp == 90 && game.Score == 550 && game.Phase == Phase.Recall, "Quarta tecla termina combate");
            bool blocked = false;
            try { game.SubmitMove(Lane.Left); } catch (InvalidOperationException) { blocked = true; }
            Check(blocked && game.Score == 550, "Teclas após o combate não duplicam recompensa");
            Check(!game.Recall(new[] { "verde" }) && game.Score == 550, "Resposta errada não premia");
            Check(game.Recall(new[] { " AZUL! " }) && game.Score == 1500, "Memória correta e bônus de HP");
            game.LeaveResults(); game.NextLevel();
            Check(game.Hp == 90 && game.Score == 1500 && game.LevelIndex == 1, "Continuidade entre fases");
            var capped = Ready(0, 80); Hit(capped); Hit(capped); Hit(capped);
            Check(capped.Hp == 100, "Cura limitada a 100 HP");
            bool oldRejected = false;
            try { GameSession.Restore(new SaveData { Version = 2 }); } catch (InvalidDataException) { oldRejected = true; }
            Check(oldRejected, "Campanha 6/9/12 não é reinterpretada como 4/6/8");
            var normal = Ready(0); Hit(normal); Hit(normal); Miss(normal);
            Check(normal.Hp == 80 && normal.Combo == 0 && normal.Score == 150, "Erro normal quebra combo e causa 20 de dano");
            var boss = Ready(2); Miss(boss); Miss(boss); Miss(boss); Miss(boss);
            Check(boss.Hp == 0 && boss.Phase == Phase.GameOver && boss.Resolved == 4 && boss.Score == -200, "Boss causa 30 de dano e morte interrompe ações");
            var up = Ready(0); up.SubmitMove(Lane.Up);
            Check(up.Input[0] == Lane.Up && up.Resolved == 1, "W é uma posição própria");
            var snapshot = up.Snapshot(); snapshot.Input.Add(Lane.Left);
            bool invalid = false;
            try { GameSession.Restore(snapshot); } catch (InvalidDataException) { invalid = true; }
            Check(invalid, "Save rejeita input sem resolução correspondente");
            invalid = false;
            try { GameSession.Restore(new SaveData { Version = 1 }); } catch (InvalidDataException) { invalid = true; }
            Check(invalid, "Versão antiga não é reinterpretada como a nova campanha");
            var streamed = Ready(1); Hit(streamed); Miss(streamed); Hit(streamed);
            string folder = Path.Combine(Path.GetTempPath(), "FishermansChecks-" + Guid.NewGuid());
            try
            {
                File.WriteAllText(Path.Combine(CreateDirectory(folder), "save.json"), "antigo");
                var service = new SaveService(folder); service.Save(streamed); var loaded = service.Load();
                Check(loaded.Resolved == 3 && loaded.Phase == Phase.Input && loaded.Input.SequenceEqual(streamed.Input), "Retoma entre teclas");
                while (streamed.Phase == Phase.Input) { Hit(streamed); Hit(loaded); }
                Check(loaded.Hp == streamed.Hp && loaded.Score == streamed.Score, "Retomada não reaplica ataques");
                service.Save(loaded);
                Check(File.ReadAllText(Path.Combine(folder, "save.json")) == "antigo", "Save anterior é preservado");
                Check(File.Exists(Path.Combine(folder, "save-v3.json.bak")), "Backup do novo save");
                File.WriteAllText(Path.Combine(folder, "save-v3.json"), "{ inválido");
                bool corrupt = false; try { service.Load(); } catch (JsonException) { corrupt = true; }
                Check(corrupt, "Corrupção rejeitada");
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
            var campaign = new GameSession(new Random(42));
            for (int i = 0; i < Levels.All.Count; i++)
            {
                campaign.Reveal(); campaign.HidePattern();
                while (campaign.Phase == Phase.Input) Hit(campaign);
                Check(!campaign.Recall(new string[0]), "Todas as informações são obrigatórias");
                Check(campaign.Recall(campaign.Level.Facts.Select(x => x.Answer).ToArray()), "Memória recuperada");
                campaign.LeaveResults(); if (i + 1 < Levels.All.Count) campaign.NextLevel();
            }
            Check(campaign.Phase == Phase.Victory && campaign.Hp == 100 && campaign.Score == 5700, "Campanha perfeita: 5700 pontos");
            Console.WriteLine(count + " verificações concluídas.");
        }
        private static string CreateDirectory(string path) { Directory.CreateDirectory(path); return path; }
    }
}
