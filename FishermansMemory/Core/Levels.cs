using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace FishermansMemory.Core
{

public enum Lane { Left, Center, Right, Up }
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
    public Level(string name, string enemy, int attacks, bool center, bool boss, MemoryFact[] facts, string passage)
    {
        Name = name;
        Enemy = enemy;
        Attacks = attacks;
        Center = center;
        Boss = boss;
        Facts = facts;
        Passage = passage;
    }
}

public static class Levels
{
    public static readonly IReadOnlyList<Level> All = new List<Level>
    {
        new Level("Praia das Conchas", "Caranguejo sentinela", 4, true, false,
            new[] { new MemoryFact("O farol é azul.", "Qual é a cor do farol?", "azul") },
            "Antes de chegar ao cais, encontrei redes secando sobre as pedras e um barco virado na areia. " +
            "A tinta azul do farol resistia à maresia, embora suas janelas estivessem cobertas de sal. " +
            "Um pescador recolheu as cordas e apontou para a trilha entre as palmeiras. Segui por ela enquanto as gaivotas disputavam os restos da pesca."),
        new Level("Ilha Rochosa", "Tubarão das rochas", 6, true, false,
            new[] { new MemoryFact("A chave está na gruta.", "Onde está a chave?", "gruta"),
                    new MemoryFact("A pérola é dourada.", "Qual é a cor da pérola?", "dourada") },
            "A pérola dourada pendia do pescoço da barqueira enquanto ela remendava uma rede junto à ponte. " +
            "Esperei a maré baixar e atravessei as pedras, levando apenas o diário e uma corda. " +
            "Na gruta, a chave repousava dentro de uma caixa sem tampa, protegida da água por um pano. " +
            "Do lado de fora, um ruído sob a espuma fez a barqueira interromper o trabalho."),
        new Level("Coração da Ilha", "Leviatã do Esquecimento", 8, true, true,
            new[] { new MemoryFact("O nome antigo da ilha é Aurora.", "Qual é o nome antigo da ilha?", "Aurora"),
                    new MemoryFact("A memória está guardada na pérola.", "Em qual objeto está guardada a memória?", "pérola"),
                    new MemoryFact("O sino toca ao amanhecer.", "Quando o sino toca?", "amanhecer") },
            "Aurora aparecia gravado no antigo mapa como o nome da ilha, quase apagado por uma mancha de sal. " +
            "Perto do altar havia redes vazias e fragmentos de madeira trazidos pela tempestade. " +
            "O guardião contou que, ao amanhecer, o sino tocava para orientar os barcos entre as rochas. " +
            "Esperei o vento diminuir antes de abrir o diário. A memória, guardada na pérola que eu carregava, " +
            "ainda preservava as histórias dos moradores. Atrás da porta, o mar começou a subir pelos degraus.")
    };
    public static Lane[] Lanes(Level level) => new[] { Lane.Left, Lane.Up, Lane.Center, Lane.Right };
    public static string Symbol(Lane lane)
    {
        switch (lane)
        {
            case Lane.Left: return "A ←";
            case Lane.Up: return "W ↑";
            case Lane.Right: return "D →";
            default: return "S ↓";
        }
    }
}
}
