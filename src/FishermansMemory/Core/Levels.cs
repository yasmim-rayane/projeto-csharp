using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FishermansMemory.Core
{

public enum Lane { Left, Center, Right }
public enum Phase { Information, Reveal, Input, Recall, Results, Map, GameOver, Victory }
public sealed class MemoryFact
{
    public string Statement { get; }
    public string Question { get; }
    public string Answer { get; }
    public MemoryFact(string statement, string question, string answer)
    {
        Statement = statement;
        Question = question;
        Answer = answer;
    }
}
public sealed class Level
{
    public string Name { get; }
    public string Enemy { get; }
    public int Attacks { get; }
    public bool Center { get; }
    public bool Boss { get; }
    public MemoryFact[] Facts { get; }
    public string Passage { get; }
    public IReadOnlyList<Lane> FixedPattern { get; }
    public int SequenceLength { get; }
    public int SequenceCount { get; }

    public Level(
        string name,
        string enemy,
        int attacks,
        bool center,
        bool boss,
        MemoryFact[] facts,
        string passage,
        IReadOnlyList<Lane> fixedPattern = null,
        int sequenceLength = 0,
        int sequenceCount = 1)
    {
        Name = name;
        Enemy = enemy;
        Attacks = attacks;
        Center = center;
        Boss = boss;
        Facts = facts;
        Passage = passage;

        if (boss)
        {
            if (fixedPattern != null)
                throw new ArgumentException("O boss final deve gerar suas sequências aleatoriamente.", nameof(fixedPattern));
            SequenceCount = sequenceCount > 0 ? sequenceCount : 2;
            SequenceLength = sequenceLength > 0 ? sequenceLength : (attacks / SequenceCount);
            if (SequenceCount != 2 || SequenceLength != 8 || attacks != 16)
                throw new ArgumentException("O boss final deve ter duas sequências de 8 ataques (16 no total).");
            FixedPattern = null;
        }
        else
        {
            if (fixedPattern == null || fixedPattern.Count != attacks)
                throw new ArgumentException("Fases normais devem definir uma sequência fixa com o total exato de ataques.", nameof(fixedPattern));
            if (fixedPattern.Any(lane => !center && lane == Lane.Center))
                throw new ArgumentException("A posição central não pode aparecer em fases com apenas duas posições.", nameof(fixedPattern));
            FixedPattern = new ReadOnlyCollection<Lane>(fixedPattern.ToArray());
            SequenceCount = 1;
            SequenceLength = attacks;
        }
    }
}

public static class Levels
{
    public static readonly IReadOnlyList<Level> All = new List<Level>
    {
        new Level(
            "Praia das Conchas",
            "Caranguejo sentinela",
            3,
            false,
            false,
            new[]
            {
                new MemoryFact("O farol é azul.", "Qual é a cor do farol da Praia das Conchas?", "azul")
            },
            "O antigo farol da Praia das Conchas continua pintado de azul diante do cais.",
            new[] { Lane.Left, Lane.Right, Lane.Left }),

        new Level(
            "Ilha Rochosa",
            "Tubarão das rochas",
            4,
            true,
            false,
            new[]
            {
                new MemoryFact("A chave está na gruta.", "Onde a barqueira escondeu a chave?", "gruta")
            },
            "Durante a maré baixa na Ilha Rochosa, a barqueira escondeu a chave de ferro dentro da gruta para protegê-la da espuma do mar.",
            new[] { Lane.Right, Lane.Center, Lane.Left, Lane.Center }),

        new Level(
            "Cais das Marés",
            "Pelicano das marés",
            5,
            true,
            false,
            new[]
            {
                new MemoryFact("O pescador guardou uma bússola sob o cais.", "Qual objeto o pescador guardou sob o Cais das Marés?", "bússola")
            },
            "Sob as tábuas úmidas do Cais das Marés, o velho pescador guardou uma bússola de bronze enrolada em um lenço de linho.",
            new[] { Lane.Center, Lane.Left, Lane.Right, Lane.Center, Lane.Left }),

        new Level(
            "Recife das Correntes",
            "Arraia dos recifes",
            6,
            true,
            false,
            new[]
            {
                new MemoryFact("Os navegadores seguiam a estrela Dalva.", "Qual é o nome da estrela que guiava os navegadores pelo recife?", "Dalva")
            },
            "Para atravessar o Recife das Correntes sem naufragar nos bancos de coral submersos, os antigos navegadores aguardavam o pôr do sol e seguiam o brilho da estrela Dalva alinhada às duas pedras gêmeas do canal, onde as águas finalmente se acalmavam.",
            new[] { Lane.Left, Lane.Center, Lane.Right, Lane.Left, Lane.Right, Lane.Center }),

        new Level(
            "Gruta Submersa",
            "Moreia das profundezas",
            7,
            true,
            false,
            new[]
            {
                new MemoryFact("A pérola é dourada.", "Qual é a cor da pérola encontrada na Gruta Submersa?", "dourada"),
                new MemoryFact("O sino toca ao amanhecer.", "Em que momento do dia o sino da torre toca?", "amanhecer")
            },
            "No altar de pedra da Gruta Submersa repousava a pérola dourada que guarda as lembranças da vila, ao lado de uma inscrição revelando que o sino da torre sempre toca ao amanhecer para anunciar a abertura do canal.",
            new[] { Lane.Right, Lane.Left, Lane.Center, Lane.Right, Lane.Center, Lane.Left, Lane.Right }),

        new Level(
            "Coração da Ilha",
            "Leviatã do Esquecimento",
            16,
            true,
            true,
            new[]
            {
                new MemoryFact("Na Praia das Conchas, o farol é azul.", "Fase 1 (Praia das Conchas): qual é a cor do farol?", "azul"),
                new MemoryFact("Na Ilha Rochosa, a chave foi escondida na gruta.", "Fase 2 (Ilha Rochosa): onde a barqueira escondeu a chave?", "gruta"),
                new MemoryFact("No Cais das Marés, o pescador guardou uma bússola.", "Fase 3 (Cais das Marés): qual objeto o pescador guardou sob o cais?", "bússola"),
                new MemoryFact("No Recife das Correntes, os navegadores seguiam a estrela Dalva.", "Fase 4 (Recife das Correntes): qual é o nome da estrela que guiava os navegadores?", "Dalva"),
                new MemoryFact("Na Gruta Submersa, o sino toca ao amanhecer.", "Fase 5 (Gruta Submersa): em que momento do dia o sino da torre toca?", "amanhecer")
            },
            "Você alcançou o Coração da Ilha, onde o Leviatã do Esquecimento se ergue sobre as águas tempestuosas para apagar tudo o que foi vivido na travessia. Enfrente suas duas sequências de oito ataques e recupere na memória as informações que você registrou nas cinco regiões anteriores para restaurar o farol.",
            null,
            8,
            2)
    };

    public static Lane[] Lanes(Level level) =>
        level != null && level.Center
            ? new[] { Lane.Left, Lane.Center, Lane.Right }
            : new[] { Lane.Left, Lane.Right };

    public static IReadOnlyList<Lane> GenerateBossSequence(Random random, Level level, int count)
    {
        if (level == null || !level.Boss)
            throw new InvalidOperationException("Somente o boss final pode gerar ataques aleatórios.");
        if (random == null)
            throw new ArgumentNullException(nameof(random));
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var lanes = Lanes(level);
        var sequence = new List<Lane>(count);
        for (int i = 0; i < count; i++)
            sequence.Add(lanes[random.Next(lanes.Length)]);
        return sequence;
    }

    public static string Symbol(Lane lane)
    {
        switch (lane)
        {
            case Lane.Left: return "A ←";
            case Lane.Right: return "D →";
            default: return "S ↓";
        }
    }
}
}
