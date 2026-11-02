using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FishermansMemory.Core
{

// DTO do checkpoint. A UI recebe cópias; somente o motor altera o estado real.
public sealed class SaveData
{
    public int Version { get; set; } = 3;
    public int LevelIndex { get; set; }
    public int Hp { get; set; } = 100;
    public int Score { get; set; }
    public int Combo { get; set; }
    public int LevelStartScore { get; set; }
    public int Resolved { get; set; }
    public int Correct { get; set; }
    public int Errors { get; set; }
    public Phase Phase { get; set; } = Phase.Information;
    public List<Lane> Pattern { get; set; } = new List<Lane>();
    public List<Lane> Input { get; set; } = new List<Lane>();
}

public sealed class AttackResult
{
    public Lane Safe { get; }
    public Lane Chosen { get; }
    public bool Correct { get; }
    public int Damage { get; }
    public int Healing { get; }
    public bool Combo { get; }
    public AttackResult(Lane safe, Lane chosen, bool correct, int damage, int healing, bool combo)
    {
        Safe = safe;
        Chosen = chosen;
        Correct = correct;
        Damage = damage;
        Healing = healing;
        Combo = combo;
    }
}

public sealed class GameSession
{
    private readonly Random random;
    private readonly SaveData data;
    public Level Level => Levels.All[data.LevelIndex];
    public Phase Phase => data.Phase;
    public int Hp => data.Hp;
    public int Score => data.Score;
    public int Combo => data.Combo;
    public int LevelIndex => data.LevelIndex;
    public int Resolved => data.Resolved;
    public int Correct => data.Correct;
    public int Errors => data.Errors;
    public int StageScore => data.Score - data.LevelStartScore;
    public IReadOnlyList<Lane> Pattern => data.Pattern.AsReadOnly();
    public IReadOnlyList<Lane> Input => data.Input.AsReadOnly();

    public GameSession(Random random = null) { this.random = random ?? new Random(); data = new SaveData(); }
    private GameSession(SaveData restored) { data = restored; random = new Random(); }
    public SaveData Snapshot() => new SaveData()
    {
        Version = data.Version, LevelIndex = data.LevelIndex, Hp = Hp, Score = Score,
        Combo = Combo, LevelStartScore = data.LevelStartScore, Resolved = Resolved,
        Correct = Correct, Errors = Errors, Phase = Phase,
        Pattern = new List<Lane>(data.Pattern), Input = new List<Lane>(data.Input)
    };
    public static GameSession Restore(SaveData d)
    {
        if (d.Version != 3 || d.LevelIndex < 0 || d.LevelIndex >= Levels.All.Count ||
            d.Hp < 0 || d.Hp > 100 || d.Combo < 0 || d.Combo > 2 || !Enum.IsDefined(typeof(Phase), d.Phase) ||
            d.Pattern is null || d.Input is null || d.Correct < 0 || d.Errors < 0)
            throw new InvalidDataException("Checkpoint inválido ou versão incompatível.");
        var l = Levels.All[d.LevelIndex];
        var lanes = Levels.Lanes(l);
        bool fresh = d.Phase == Phase.Information;
        if (d.Pattern.Any(x => !lanes.Contains(x)) || d.Input.Any(x => !lanes.Contains(x)) ||
            (fresh ? d.Pattern.Count != 0 : d.Pattern.Count != l.Attacks) ||
            d.Input.Count > d.Pattern.Count || d.Resolved < 0 || d.Resolved != d.Input.Count ||
            d.Correct + d.Errors != d.Resolved ||
            (d.Phase == Phase.GameOver ? d.Hp != 0 : d.Hp == 0) ||
            (d.Phase == Phase.Input && d.Resolved >= l.Attacks) ||
            ((d.Phase == Phase.Recall || d.Phase == Phase.Results || d.Phase == Phase.Map || d.Phase == Phase.Victory) && d.Resolved != l.Attacks) ||
            ((d.Phase == Phase.Information || d.Phase == Phase.Reveal) && d.Resolved != 0) ||
            ((d.Phase == Phase.Information || d.Phase == Phase.Reveal) && d.Input.Count != 0) ||
            (d.Phase == Phase.Victory && d.LevelIndex != Levels.All.Count - 1) ||
            (d.Phase == Phase.Map && d.LevelIndex == Levels.All.Count - 1))
            throw new InvalidDataException("Checkpoint inconsistente.");
        return new GameSession(new GameSession(d).Snapshot());
    }
    private void Require(Phase phase)
    {
        if (Phase != phase) throw new InvalidOperationException($"Ação indisponível em {Phase}.");
    }
    public void Reveal()
    {
        Require(Phase.Information);
        var lanes = Levels.Lanes(Level);
        for (int i = 0; i < Level.Attacks; i++) data.Pattern.Add(lanes[random.Next(lanes.Length)]);
        data.Phase = Phase.Reveal;
    }
    public void HidePattern() { Require(Phase.Reveal); data.Phase = Phase.Input; }
    // Uma tecla = uma ação irreversível. Não existe buffer de sequência para confirmar.
    public AttackResult SubmitMove(Lane chosen)
    {
        Require(Phase.Input);
        if (!Levels.Lanes(Level).Contains(chosen)) throw new ArgumentOutOfRangeException(nameof(chosen));
        var safe = data.Pattern[data.Resolved];
        data.Input.Add(chosen);
        bool correct = safe == chosen; int damage = 0, healing = 0; bool combo = false;
        if (correct)
        {
            data.Correct++; data.Score += 100; data.Combo++;
            if (data.Combo == 3)
            {
                combo = true; data.Score += 150; healing = Math.Min(50, 100 - Hp);
                data.Hp += healing; data.Combo = 0;
            }
        }
        else
        {
            damage = Level.Boss ? 30 : 20; data.Hp = Math.Max(0, Hp - damage);
            data.Score -= 50; data.Combo = 0; data.Errors++;
        }
        data.Resolved++;
        if (Hp == 0) data.Phase = Phase.GameOver;
        else if (data.Resolved == data.Pattern.Count) data.Phase = Phase.Recall;
        return new AttackResult(safe, chosen, correct, damage, healing, combo);
    }
    public bool Recall(IReadOnlyList<string> answers)
    {
        Require(Phase.Recall);
        if (answers.Count != Level.Facts.Length ||
            Level.Facts.Where((f, i) => Normalize(f.Answer) != Normalize(answers[i])).Any()) return false;
        data.Score += 500 + 5 * Hp; data.Phase = Phase.Results; return true;
    }
    public void LeaveResults()
    {
        Require(Phase.Results);
        data.Phase = data.LevelIndex == Levels.All.Count - 1 ? Phase.Victory : Phase.Map;
    }
    public void NextLevel()
    {
        Require(Phase.Map);
        data.LevelIndex++; data.LevelStartScore = Score; data.Resolved = 0;
        data.Correct = 0; data.Errors = 0; data.Pattern.Clear(); data.Input.Clear();
        data.Phase = Phase.Information;
    }
    private static string Normalize(string s)
    {
        var chars = s.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        return string.Join(" ", new string(chars.ToArray()).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }
}

}
