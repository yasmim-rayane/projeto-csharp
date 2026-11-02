# Fish’s Memory — campanha de três fases

C# 7.3 • Windows Forms • .NET Framework 4.7.2 • Visual Studio 2022.

## Abrir

1. Extraia o ZIP em uma pasta nova e abra `FishermansMemory.sln`.
2. Tenha a carga de trabalho **Desenvolvimento para desktop com .NET** e o **Developer Pack do .NET Framework 4.7.2** instalados.
3. Restaure os pacotes NuGet. `System.Text.Json 8.0.6` e suas dependências estão configurados nos projetos.
4. Defina `FishermansMemory` como projeto de inicialização e pressione F5.

Distribua todo o conteúdo de `FishermansMemory/bin/Release`, incluindo DLLs, `.exe.config`, `Assets/Art` e WAVs. As artes são copiadas automaticamente durante a compilação.

## O que mudou nesta versão

- Protagonista parado em posições fixas; inimigos agora têm ociosidade e ataque distintos.
- Caranguejo alterna poses e golpeia com pinças; tubarão alterna poses e investe com rastros de água; Leviatã pulsa em uma camada transparente e lança tentáculos.
- Campos WASD com base escura, contorno de alto contraste e rótulos de resultado. Posição escolhida dourada.
- HUD fixo separado do texto: coração/barra segmentada de HP, contador de pontos e combo. Sem cronômetro ou inventário extra.
- Cartões de NOTA em cinza, distintos dos cartões de DICA/CONTROLES/ÚLTIMA AÇÃO.
- Diário narrativo por fase, com respostas espalhadas no início e no meio das orações; não há destaque visual das respostas.
- Novos cenários em 1536 × 1024 preenchem o fundo inteiro em todas as telas. O preenchimento preserva a proporção e pode cortar bordas do cenário conforme o monitor; sprites são enquadrados inteiros na área de combate.
- A campanha passou para três fases com 4, 6 e 8 ataques; o último encontro é o Boss.
- Todas as fases usam quatro posições distintas: **W cima, A esquerda, S baixo, D direita**. Setas são alternativas equivalentes.
- Cada pressionamento resolve imediatamente um ataque e inicia seu efeito visual. Não há confirmação de sequência, botão de executar ataque ou desfazer.
- Autor repetição do Windows é ignorada: solte a tecla antes de pressionar novamente. Teclas diferentes e novos pressionamentos são aceitos durante o efeito anterior; todos são validados e o efeito visual passa a representar o ataque mais recente.
- O último efeito é exibido antes da tela de memória ou Game Over. Nenhum tempo de resposta é exigido.

## Janela e tela cheia

**F11** ou **Alt+Enter** alternam tela cheia sem bordas no monitor atual; **Esc** volta à janela. O tamanho, posição e estado maximizado anteriores são restaurados. O painel fica à esquerda em áreas largas e embaixo em áreas estreitas, com rolagem para textos extensos. A área do combate é reservada e não fica atrás do painel. Configuração PerMonitorV2 para DPI do Windows; mantenha o `.exe.config` distribuído com o executável.

## Progressão

| Fase | Região / inimigo | Ataques | Informações | Dano por erro |
|---|---|---:|---:|---:|
| 1 | Praia das Conchas / Caranguejo sentinela | 4 | 1 | 20 |
| 2 | Ilha Rochosa / Tubarão das rochas | 6 | 2 | 20 |
| Boss | Coração da Ilha / Leviatã do Esquecimento | 8 | 3 | 30 |

A quantidade de informação e a extensão das sequências aumentam a complexidade sem exigir reflexos. O desbravador segue as pistas da praia até a ilha rochosa e enfrenta o Leviatã para recuperar o nome Aurora e a memória guardada na pérola.

## Fluxo e controles

Menu → Introdução → Tutorial → Informação → Revelação → Combate por tecla → Recuperação → Resultados → Mapa → próxima fase. O Boss termina no desfecho; HP zero leva a Game Over.

Leia o diário e avance para observar as posições seguras. O botão **Iniciar combate** oculta o padrão; ele inicia a etapa de memória e não confirma ações de esquiva. Depois disso, apenas WASD/setas executam os movimentos. Os destinos são absolutos: dois A consecutivos correspondem a dois ataques cuja posição escolhida é esquerda.

A cada tecla, `GameSession.SubmitMove()` valida a posição, registra a resposta, calcula HP e pontos e avança o índice. O estado é salvo antes de aguardar qualquer efeito visual. Cada golpe tem 650 ms de apresentação; novos pressionamentos continuam aceitos e substituem a apresentação anterior. O temporizador da arena anima a ociosidade do inimigo, o golpe específico e uma transição curta de cenário. Não controla a resolução, não aplica dano e não concede pontos.

Ao completar os ataques, responda com as palavras-chave. A comparação ignora maiúsculas, acentos, pontuação e espaços excedentes. Todas as respostas devem estar corretas. Respostas erradas permitem nova tentativa sem penalidade adicional.

## HP e pontuação

- HP inicial 100, persistente entre fases; erro normal −20 e Boss −30.
- Cada grupo de três esquivas corretas consecutivas cura 50 HP, limitado a 100, e concede +150 pontos. Um erro quebra o combo.
- Cada esquiva correta concede +100; cada erro subtrai 50.
- Recuperar todas as informações concede +500, mais 5 × HP final, uma vez por fase.
- HP zero interrompe o combate imediatamente. Não há bônus de tempo nem piso zero para pontuação.
- Campanha perfeita: **5.700 pontos** (1.550 + 1.900 + 2.250; combo continua entre fases).

## Salvamento

O novo progresso usa `%LOCALAPPDATA%\FishermansMemory\save-v3.json`, com backup `.bak` e escrita temporária seguida de substituição. O formato tem versão 3 e registra a fase, HP, pontos, combo, padrão, respostas já resolvidas e contador de ataques. Continuar retoma a próxima posição sem reaplicar os ataques anteriores.

As versões anteriores usavam `save.json` e `save-v2.json`. Ambos são preservados. **Esse arquivo antigo não é apagado nem convertido automaticamente**, porque a nova campanha e o combate têm estrutura diferente. Comece uma nova aventura nesta versão; ela não sobrescreve o save anterior. Gravações v1/v2 são rejeitadas se copiadas manualmente para o caminho v3.

## Arquitetura

| Arquivo | Responsabilidade |
|---|---|
| `Core/Levels.cs` | Três fases, quatro posições, fatos e perguntas |
| `Core/GameSession.cs` | Máquina de estados e resolução imediata por tecla |
| `Infrastructure/SaveService.cs` | JSON v3 e backup |
| `Infrastructure/ArtAssets.cs` | Recortes em memória e cache das pranchas |
| `Infrastructure/AudioService.cs` | Música e efeito WAV com SoundPlayer |
| `UI/MainForm.cs` | Navegação, WASD e bloqueio de autorrepetição |
| `UI/Arena.cs` | Campos WASD, animação de inimigos, fundo e efeitos |
| `UI/GameHud.cs` | Barra de HP, contador de pontos e combo desenhados em código |
| `FishermansMemory.Checks` | Verificações de regras, retomada e assets |

As telas são criadas em código. O projeto mantém os namespaces solicitados: Windows.Forms, Drawing, Media, IO, Text.Json, Collections.Generic e a classe System.Random.

## Verificação

No Developer PowerShell for VS 2022:

```powershell
msbuild FishermansMemory.sln /restore /p:Configuration=Release
.\FishermansMemory.Checks\bin\Release\FishermansMemory.Checks.exe
```

Os testes cobrem três fases, quatro posições, resolução imediata, dano, cura, combo, morte, respostas obrigatórias, prevenção de recompensa duplicada, salvamento entre teclas, preservação do save anterior, corrupção e pontuação da campanha. Verificam também carregamento das artes e os recursos de ociosidade/ataque.

Nesta entrega: revisão de código, validação de referências dos projetos, dimensões do atlas, cálculos de enquadramento e integridade do ZIP. **Compilação e testes C# não executados neste ambiente**, que não dispõe de compilador C# nem Windows. Validação visual em WinForms permanece pendente.

No Windows, confira: segurar A gera uma ação; soltar e pressionar gera outra; W e S são posições diferentes; pressionamentos rápidos pontuam individualmente; a última ação mostra o efeito antes de abrir memória/Game Over; fechar e continuar não duplica pontuação; os cenários preenchem o fundo sem distorção; o protagonista fica estático, inimigos se movimentam, notas e HUD permanecem legíveis. Veja `GUIA_VISUAL.md`.

As respostas e regras foram preservadas nesta revisão: o save-v3 atual continua compatível. Uma partida já iniciada pode continuar; para ler os novos diários desde o início, inicie uma nova aventura.
