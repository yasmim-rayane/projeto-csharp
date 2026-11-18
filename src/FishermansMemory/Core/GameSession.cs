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
    public int LevelStartCombo { get; set; }
    public int Resolved { get; set; }
    public int Correct { get; set; }
    public int Errors { get; set; }
    public int RecallCorrect { get; set; }
    public int RecallTotal { get; set; }
    public int RecallBonus { get; set; }
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
    // Utilizado exclusivamente para gerar as sequências aleatórias do boss final.
    private readonly Random bossRandom;
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
    public int RecallCorrect => data.RecallCorrect;
    public int RecallTotal => data.RecallTotal;
    public int RecallBonus => data.RecallBonus;
    public int StageScore => data.Score - data.LevelStartScore;
    public IReadOnlyList<Lane> Pattern => data.Pattern.AsReadOnly();
    public IReadOnlyList<Lane> Input => data.Input.AsReadOnly();
    public int TotalWaves => Level.SequenceCount;
    public int CurrentWave => Level.Boss ? (data.Pattern.Count > Level.SequenceLength ? 2 : 1) : 1;
    public int CurrentWaveStart => Level.Boss && data.Pattern.Count > Level.SequenceLength ? Level.SequenceLength : 0;
    public IReadOnlyList<Lane> CurrentWavePattern =>
        data.Pattern.Skip(CurrentWaveStart).Take(Level.SequenceLength).ToList().AsReadOnly();

    public GameSession(Random bossRandom = null) { this.bossRandom = bossRandom ?? new Random(); data = new SaveData(); }
    private GameSession(SaveData restored) { data = restored; bossRandom = new Random(); }

    public SaveData Snapshot() => new SaveData()
    {
        Version = data.Version, LevelIndex = data.LevelIndex, Hp = Hp, Score = Score,
        Combo = Combo, LevelStartScore = data.LevelStartScore, LevelStartCombo = data.LevelStartCombo,
        Resolved = Resolved, Correct = Correct, Errors = Errors, RecallCorrect = RecallCorrect,
        RecallTotal = RecallTotal, RecallBonus = RecallBonus, Phase = Phase,
        Pattern = new List<Lane>(data.Pattern), Input = new List<Lane>(data.Input)
    };

    public static GameSession Restore(SaveData d)
    {
        if (d.Version != 3 || d.LevelIndex < 0 || d.LevelIndex >= Levels.All.Count ||
            d.Hp < 0 || d.Hp > 100 || d.Combo < 0 || d.Combo > 2 ||
            d.LevelStartCombo < 0 || d.LevelStartCombo > 2 ||
            !Enum.IsDefined(typeof(Phase), d.Phase) ||
            d.Pattern is null || d.Input is null || d.Correct < 0 || d.Errors < 0)
            throw new InvalidDataException("Checkpoint inválido ou versão incompatível.");

        var l = Levels.All[d.LevelIndex];
        var lanes = Levels.Lanes(l);
        bool fresh = d.Phase == Phase.Information;

        if (d.Pattern.Any(x => !lanes.Contains(x)) || d.Input.Any(x => !lanes.Contains(x)) ||
            d.Input.Count > d.Pattern.Count || d.Resolved < 0 || d.Resolved != d.Input.Count ||
            d.Correct + d.Errors != d.Resolved ||
            (d.Phase == Phase.GameOver ? d.Hp != 0 : d.Hp == 0) ||
            (d.Phase == Phase.Victory && d.LevelIndex != Levels.All.Count - 1) ||
            (d.Phase == Phase.Map && d.LevelIndex == Levels.All.Count - 1))
            throw new InvalidDataException("Checkpoint inconsistente.");

        if (fresh)
        {
            if (d.Pattern.Count != 0 || d.Resolved != 0)
                throw new InvalidDataException("Checkpoint inconsistente.");
        }
        else if (!l.Boss)
        {
            if (d.Pattern.Count != l.Attacks || !d.Pattern.SequenceEqual(l.FixedPattern))
                throw new InvalidDataException("Sequência da fase normal inválida.");
            if ((d.Phase == Phase.Reveal && d.Resolved != 0) ||
                (d.Phase == Phase.Input && d.Resolved >= l.Attacks) ||
                ((d.Phase == Phase.Recall || d.Phase == Phase.Results || d.Phase == Phase.Map || d.Phase == Phase.Victory) && d.Resolved != l.Attacks))
                throw new InvalidDataException("Checkpoint inconsistente.");
        }
        else
        {
            bool wave1 = d.Pattern.Count == l.SequenceLength;
            bool wave2 = d.Pattern.Count == l.Attacks;
            if (!wave1 && !wave2)
                throw new InvalidDataException("Sequência do boss inválida.");
            if (d.Phase == Phase.Reveal && !((wave1 && d.Resolved == 0) || (wave2 && d.Resolved == l.SequenceLength)))
                throw new InvalidDataException("Checkpoint inconsistente.");
            if (d.Phase == Phase.Input && !((wave1 && d.Resolved < l.SequenceLength) || (wave2 && d.Resolved >= l.SequenceLength && d.Resolved < l.Attacks)))
                throw new InvalidDataException("Checkpoint inconsistente.");
            if ((d.Phase == Phase.Recall || d.Phase == Phase.Results || d.Phase == Phase.Map || d.Phase == Phase.Victory) &&
                (!wave2 || d.Resolved != l.Attacks))
                throw new InvalidDataException("Checkpoint inconsistente.");
        }

        return new GameSession(new GameSession(d).Snapshot());
    }

    private void Require(Phase phase)
    {
        if (Phase != phase) throw new InvalidOperationException($"Ação indisponível em {Phase}.");
    }

    public void Reveal()
    {
        Require(Phase.Information);
        data.Pattern.Clear();
        if (Level.Boss)
            data.Pattern.AddRange(Levels.GenerateBossSequence(bossRandom, Level, Level.SequenceLength));
        else
            data.Pattern.AddRange(Level.FixedPattern);
        data.Phase = Phase.Reveal;
    }

    public void HidePattern()
    {
        Require(Phase.Reveal);
        data.Phase = Phase.Input;
    }

    public AttackResult SubmitMove(Lane chosen)
    {
        Require(Phase.Input);
        if (!Levels.Lanes(Level).Contains(chosen)) throw new ArgumentOutOfRangeException(nameof(chosen));

        var safe = data.Pattern[data.Resolved];
        data.Input.Add(chosen);
        bool correct = safe == chosen;
        int damage = 0, healing = 0;
        bool combo = false;

        if (correct)
        {
            data.Correct++;
            data.Score += 100;
            data.Combo++;
            if (data.Combo == 3)
            {
                combo = true;
                data.Score += 150;
                healing = Math.Min(50, 100 - Hp);
                data.Hp += healing;
                data.Combo = 0;
            }
        }
        else
        {
            damage = Level.Boss ? 30 : 20;
            data.Hp = Math.Max(0, Hp - damage);
            data.Score -= 50;
            data.Combo = 0;
            data.Errors++;
        }

        data.Resolved++;
        if (Hp == 0)
        {
            data.Phase = Phase.GameOver;
        }
        else if (data.Resolved == data.Pattern.Count)
        {
            if (Level.Boss && data.Pattern.Count < Level.Attacks)
            {
                data.Pattern.AddRange(Levels.GenerateBossSequence(bossRandom, Level, Level.SequenceLength));
                data.Phase = Phase.Reveal;
            }
            else
            {
                data.Phase = Phase.Recall;
            }
        }

        return new AttackResult(safe, chosen, correct, damage, healing, combo);
    }

    public bool IsFactCorrect(int index, string answer)
    {
        if (index < 0 || index >= Level.Facts.Length) return false;
        return Normalize(Level.Facts[index].Answer) == Normalize(answer);
    }

    public bool Recall(IReadOnlyList<string> answers)
    {
        Require(Phase.Recall);
        if (answers == null || answers.Count != Level.Facts.Length ||
            Level.Facts.Where((f, i) => Normalize(f.Answer) != Normalize(answers[i])).Any())
            return false;

        int bonus = 500 + 5 * Hp;
        data.RecallCorrect = Level.Facts.Length;
        data.RecallTotal = Level.Facts.Length;
        data.RecallBonus = bonus;
        data.Score += bonus;
        data.Phase = Phase.Results;
        return true;
    }

    public void RepeatCurrentLevel()
    {
        Require(Phase.Recall);
        data.Score = data.LevelStartScore;
        data.Combo = data.LevelStartCombo;
        data.Resolved = 0;
        data.Correct = 0;
        data.Errors = 0;
        data.RecallCorrect = 0;
        data.RecallTotal = 0;
        data.RecallBonus = 0;
        data.Pattern.Clear();
        data.Input.Clear();
        data.Phase = Phase.Information;
    }

    public void LeaveResults()
    {
        Require(Phase.Results);
        data.Phase = data.LevelIndex == Levels.All.Count - 1 ? Phase.Victory : Phase.Map;
    }

    public void NextLevel()
    {
        Require(Phase.Map);
        data.LevelIndex++;
        data.LevelStartScore = Score;
        data.LevelStartCombo = Combo;
        data.Resolved = 0;
        data.Correct = 0;
        data.Errors = 0;
        data.RecallCorrect = 0;
        data.RecallTotal = 0;
        data.RecallBonus = 0;
        data.Pattern.Clear();
        data.Input.Clear();
        data.Phase = Phase.Information;
    }

    private static string Normalize(string s)
    {
        var chars = (s ?? string.Empty).Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        return string.Join(" ", new string(chars.ToArray()).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }
}

}
