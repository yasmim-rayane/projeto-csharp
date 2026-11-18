# Fisherman’s Memory

> Aventura litorânea 2D em pixel art de memória tática e narrativa, desenvolvida em **C# 7.3**, **Windows Forms** e **.NET Framework 4.7.2**.

---

## Índice

1. [Visão Geral e Mecânicas](#1-visão-geral-e-mecânicas)
2. [Estrutura de Pastas e Arquivos](#2-estrutura-de-pastas-e-arquivos)
3. [Requisitos, Compilação e Execução](#3-requisitos-compilação-e-execução)
4. [Controles e Fluxo da Partida](#4-controles-e-fluxo-da-partida)
5. [Vida, Combo, Pontuação e Progressão](#5-vida-combo-pontuação-e-progressão)
6. [Funcionalidades e Resumo das Alterações](#6-funcionalidades-e-resumo-das-alterações)
7. [Artes, Áudio e Fontes](#7-artes-áudio-e-fontes)
8. [Salvamento de Progresso e Configurações](#8-salvamento-de-progresso-e-configurações)
9. [Explicação Técnica para Apresentação](#9-explicação-técnica-para-apresentação)
10. [Testes Realizados e Limitações Conhecidas](#10-testes-realizados-e-limitações-conhecidas)

---

## 1. Visão Geral e Mecânicas

**Fisherman’s Memory** é um jogo de aventura e memória tática ambientado em uma ilha litorânea cujas histórias estão desaparecendo sob o domínio do **Leviatã do Esquecimento**. Ao contrário de jogos tradicionais de encontrar pares, cada fase combina duas formas de memorização:

- **Memória contextual (Diário de Expedição)**: o jogador lê um relato narrativo antes do combate, retendo informações-chave sem que as respostas recebam destaque visual.
- **Memória de padrão espacial (Combate)**: o guardião da região revela uma sequência de posições seguras (`W ↑`, `A ←`, `S ↓`, `D →`). Após ocultar o padrão, o jogador desvia de cada golpe pressionando a tecla correspondente, sem limite de tempo ou exigência de reflexos rápidos.
- **Recuperação de memória**: ao sobreviver aos ataques, o jogador responde às perguntas do diário (o Boss inclui também uma pergunta acumulada da primeira região).

---

## 2. Estrutura de Pastas e Arquivos

A raiz do repositório mantém apenas o arquivo de solução (`.sln`), o launcher (`FishermansMemory.exe`), este `README.md` e configurações de Git:

```text
projeto-csharp/
├── FishermansMemory.sln
├── FishermansMemory.exe                 # Launcher na raiz com ícone oficial (inicia bin/Release ou bin/Debug)
├── README.md
├── .gitattributes
├── .gitignore
└── src/
    └── FishermansMemory/
        ├── FishermansMemory.csproj
        ├── Program.cs                   # Ponto de entrada WinForms (STAThread)
        ├── App.config                   # Redirecionamentos de assembly e suporte PerMonitorV2 DPI
        ├── app.manifest                 # Manifesto de aplicação Windows
        ├── Core/
        │   ├── GameSession.cs           # Máquina de estados, regras de combate, HP, combo e pontuação
        │   └── Levels.cs                # Definição das 3 fases, diários, perguntas e respostas
        ├── Infrastructure/
        │   ├── ArtAssets.cs             # Carregamento do atlas.json, recortes em memória e máscaras
        │   ├── AudioService.cs          # Reprodução simultânea via Windows MCI + fallback SoundPlayer
        │   └── SaveService.cs           # Persistência atômica JSON (save-v3.json e settings.json)
        ├── UI/
        │   ├── CoastalTheme.cs          # Paleta litorânea, fontes embarcadas, molduras e logo/ícone
        │   ├── CoastalControls.cs       # Controles reutilizáveis (botões, cartões, entradas, mapa, sequência)
        │   ├── MainForm.cs              # Orquestração de telas, navegação, teclado e modais
        │   ├── GameHud.cs               # Barra superior compacta (fase, ataques, vida, pontos, combo, menu)
        │   └── Arena.cs                 # Cenário, sprites em pixel art, placas WASD e efeitos de golpe
        └── Assets/
            ├── Art/                     # Cenários 1536×1024, sprites HD RGBA, atlas.json, logo.png e app.ico
            ├── Audio/                   # Trilhas em loop, som de mar/gaivotas e SFX em WAV
            └── Fonts/                   # Fontes Pixelify Sans e Chakra Petch + licenças OFL 1.1
```

---

## 3. Requisitos, Compilação e Execução

### Pré-requisitos
- **Windows** com **.NET Framework 4.7.2 Developer Pack**.
- **Visual Studio 2019/2022** (ou *MSBuild Build Tools*) com a carga de trabalho **Desenvolvimento para desktop com .NET**.
- Pacote NuGet `System.Text.Json` **8.0.6** (restaurado automaticamente pelo MSBuild/Visual Studio).

### Compilar e Executar
1. Abra `FishermansMemory.sln` no Visual Studio ou no *Developer PowerShell*.
2. Restaure pacotes e compile em `Release`:
   ```powershell
   msbuild FishermansMemory.sln /restore /p:Configuration=Release
   ```
3. Pressione **F5** no Visual Studio ou execute `FishermansMemory.exe` diretamente na raiz do repositório (ou `src\FishermansMemory\bin\Release\FishermansMemory.exe`).
4. Para distribuição avulsa, copie todo o conteúdo de `src\FishermansMemory\bin\Release\` (executável, DLLs, `.exe.config` e pasta `Assets\`).

---

## 4. Controles e Fluxo da Partida

### Fluxo Completo
`Menu Principal` → `Introdução (2 páginas)` → `Tutorial (3 etapas com prática isolada)` → `Diário (Informação)` → `Revelação da Sequência` → `Combate por tecla` → `Recuperação de Memória` → `Resultados` → `Mapa de Progressão` → próxima fase (até `Vitória` ou `Derrota`).

### Controles
- **Esquiva em combate e treino**: `W / ↑` (Cima), `A / ←` (Esquerda), `S / ↓` (Baixo) e `D / →` (Direita). Cada tecla válida resolve imediatamente um ataque; segurar a tecla não repete o comando.
- **Menus, confirmações e respostas**: mouse, `Tab` (navegação), `Enter` / `Espaço` (confirmar) e `Esc` (fechar modais ou sair da tela cheia).
- **Tela cheia**: `F11` ou `Alt+Enter` alternam modo sem bordas preservando tamanho/posição anteriores.

---

## 5. Vida, Combo, Pontuação e Progressão

| Fase | Região / Guardião | Ataques | Perguntas de Memória | Dano por Erro |
|---|---|---:|---:|---:|
| **1** | Praia das Conchas / Caranguejo Sentinela | 4 | 1 | −20 HP |
| **2** | Ilha Rochosa / Tubarão das Rochas | 6 | 2 | −20 HP |
| **Boss** | Coração da Ilha / Leviatã do Esquecimento | 8 | 4 *(inclui 1 acumulada da Fase 1)* | −30 HP |

- **HP inicial**: `100` (mantido entre fases; limite entre `0` e `100`). Ao chegar a `0`, entra em **Game Over** imediatamente.
- **Combo (×3)**: 3 esquivas corretas consecutivas curam `+50 HP` (limitado a `100`) e concedem `+150 pontos` extras. Um erro zera o contador de combo.
- **Pontuação**: `+100` por esquiva correta; `−50` por erro de posicionamento (sem piso zero); `+500 + 5 × HP restante` ao recuperar todas as informações da fase; `+100` por resposta correta parcial caso o jogador confirme avançar com erros.
- **Campanha perfeita**: **5.700 pontos** (`1.550 + 1.900 + 2.250`), sem bônus por velocidade ou cronômetro.

---

## 6. Funcionalidades e Resumo das Alterações

- **Interface litorânea sem barra lateral**: substituição da antiga coluna fixa por layout modular em madeira envelhecida, pergaminho pautado e ardósia oceânica, liberando o cenário completo durante o combate e aplicando escurecimento de fundo atrás de cartões e pop-ups.
- **Tutorial em 3 etapas com prática isolada**: explica o ciclo do jogo, as 4 posições e permite praticar em arena isolada com `HP Treino` e `Combo` próprios sem afetar o progresso da campanha.
- **Novas artes pixel art exclusivas**: sprites refeitos para o protagonista (`idle`, `dodge` ao acertar esquiva, `hurt` e `dead`), Caranguejo Sentinela (com ataque de pinças convergentes), Tubarão das Rochas e logo oficial do peixe saltando (usada no cabeçalho, ícone da janela e executáveis).
- **Trilha sonora e efeitos simultâneos**: músicas próprias para menu/exploração (`menu_calm.wav`), mar com gaivotas (`ocean.wav`), batalha normal (`combat_battle.wav`), chefe (`boss_battle.wav`), vitória, derrota e SFX de combate e botões (`ui_hover.wav`, `ui_select.wav`).
- **Validação flexível e confirmação de envio**: comparação de memória ignora maiúsculas, acentos e pontuação; respostas incorretas abrem o aviso **"Você tem certeza?"** permitindo revisar sem perder o texto digitado ou avançar com pontuação menor.
- **Persistência completa e limpeza**: gravação de configurações e recorde em `settings.json`, inclusão de pergunta acumulada da Fase 1 no Boss, organização em `src/` e exclusão de assets/arquivos obsoletos (`concept.png`, `boss-hd.png`, `campaign.png`, `details.png`, `click.wav` e pasta `tests/`).

---

## 7. Artes, Áudio e Fontes

- **Artes (`src/FishermansMemory/Assets/Art/`)**: `beach-hd.png`, `rocks-hd.png`, `boss-clean-hd.png`, `leviathan-sprite.png`, `crab-idle-hd.png`, `crab-idle2-hd.png`, `crab-attack-hd.png`, `shark-idle-hd.png`, `shark-idle2-hd.png`, `shark-attack-hd.png`, `hero-idle-hd.png`, `hero-dodge-hd.png`, `hero-hurt-hd.png`, `hero-dead-hd.png`, `splash.png`, `logo.png`, `app.ico` e `atlas.json`.
- **Áudio (`src/FishermansMemory/Assets/Audio/`)**: `menu_calm.wav`, `ocean.wav`, `combat_battle.wav`, `boss_battle.wav`, `victory.wav`, `gameover.wav`, `hit.wav`, `combo.wav`, `damage.wav`, `ui_hover.wav` e `ui_select.wav`.
- **Fontes (`src/FishermansMemory/Assets/Fonts/`)**: carregadas em memória via `PrivateFontCollection` sem depender de instalação no sistema:
  - **Pixelify Sans** (`PixelifySans.ttf`, licença **SIL OFL 1.1** em `PixelifySans-OFL.txt`): títulos, selos e botões.
  - **Chakra Petch** (`ChakraPetch-Regular.ttf`, `ChakraPetch-Bold.ttf`, licença **SIL OFL 1.1** em `ChakraPetch-OFL.txt`): textos narrativos, diário e campos de resposta com suporte completo a acentuação em português.

---

## 8. Salvamento de Progresso e Configurações

Os arquivos são gravados em `%LOCALAPPDATA%\FishermansMemory\` por `SaveService`:
- `save-v3.json` (com backup `.bak` via escrita temporária + `File.Replace`): armazena versão, fase, HP, pontuação, combo, padrão gerado (`Pattern`), entradas já resolvidas (`Input`), contador `Resolved` e resumo da recuperação de memória. A retomada continua exatamente do próximo ataque sem reaplicar pontos ou dano anteriores. Saves de versões antigas (`v1`/`v2`) não são convertidos e são rejeitados por `GameSession.Restore()`.
- `settings.json`: persiste `AudioEnabled`, `MotionEnabled` e `BestScore`. Na primeira execução ou em caso de arquivo corrompido, retorna padrões seguros (`true`, `true`, `0`) sem interromper o jogo.

---

## 9. Explicação Técnica para Apresentação

Esta seção resume o funcionamento interno do código para apresentação acadêmica.

### Tecnologias e Por que Foram Escolhidas
- **C# 7.3 e .NET Framework 4.7.2 com Windows Forms (`System.Windows.Forms`)**: escolhido por ser a plataforma desktop nativa solicitada no projeto, permitindo construir toda a interface por código (`MainForm`, `CoastalControls`, `GameHud`, `Arena`) com controle preciso sobre eventos de teclado (`ProcessCmdKey`), foco e `DoubleBuffered` sem depender de engines externas.
- **`System.Drawing` / `System.Drawing.Drawing2D`**: responsável pela renderização 2D, escalonamento `NearestNeighbor` para preservar nitidez de pixel art, regiões com cantos em degrau (`CoastalTheme.CreatePixelRegion`), desenho procedural de molduras e carregamento de fontes via `PrivateFontCollection`.
- **`System.Collections.Generic` e `System.Random`**: listas (`List<Lane>`, `Dictionary<string, Bitmap[]>`) armazenam padrões de ataque, sequência de respostas e cache de sprites; `System.Random` sorteia a sequência segura de posições em `GameSession.Reveal()`.
- **`System.IO` e `System.Text.Json`**: serializam o estado da partida (`SaveData`) e preferências (`GameSettings`) com leitura/escrita atômica (`File.Replace` com `.bak`).
- **`winmm.dll` (MCI `mciSendString`) + `System.Media.SoundPlayer`**: durante o desenvolvimento observou-se que `SoundPlayer` só reproduz um áudio por vez no processo (um efeito sonoro interrompia a música de fundo). Por isso, `AudioService` usa MCI (`mpegvideo`) para tocar música em loop, camada de mar/gaivotas e SFX simultaneamente, mantendo `SoundPlayer` como fallback para `damage.wav`.

### Organização e Comunicação Entre Camadas
O projeto separa **regras** (`Core`), **recursos/persistência** (`Infrastructure`) e **interface** (`UI`):
- `Core` (`GameSession`, `Levels`) não referencia `System.Windows.Forms` nem `System.Drawing`. Toda transição de estado ocorre pelos métodos `Reveal()`, `HidePattern()`, `SubmitMove(Lane)`, `Recall()`, `SubmitRecallWithErrors()`, `LeaveResults()` e `NextLevel()`, protegidos por `Require(Phase)`.
- `MainForm` atua como controlador de telas: lê o estado de `GameSession`, atualiza `GameHud` (`UpdateSession(g)`) e `Arena` (`Configure(...)`, `Play(...)`), persiste via `SaveService` e dispara trilhas via `AudioService`.

### Como Funciona a Mecânica Central de Memória e Combate
1. **Exposição e Ocultação (`Levels.All` → `GameSession.Reveal`)**: na `Phase.Information`, `MainForm.BuildInformationScreen` exibe o texto `Level.Passage` em `NarrativePageControl`. Ao avançar, `GameSession.Reveal()` sorteia `Level.Attacks` posições válidas em `data.Pattern` e muda para `Phase.Reveal`, onde `SequenceBoardControl` exibe a sequência completa sem limite de tempo.
2. **Entrada e Bloqueio de Autorrepetição (`MainForm.ProcessCmdKey` → `GameSession.SubmitMove`)**: ao clicar em iniciar combate (`HidePattern()`), `MainForm.ProcessCmdKey` intercepta `WASD` e Setas. O bit 30 de `msg.LParam` (`(msg.LParam.ToInt64() & (1L << 30)) != 0`) bloqueia autorrepetição ao segurar a tecla; modais abertos (`modalCard.Visible`) também bloqueiam comandos de combate. Cada tecla válida chama `MoveAndAttack(lane)` → `game.SubmitMove(lane)`, que compara `chosen == data.Pattern[data.Resolved]`, aplica HP/pontos/combo imediatamente e salva antes mesmo da animação terminar.
3. **Validação da Memória (`GameSession.Normalize` e `Recall`)**: ao final dos ataques (`Phase.Recall`), `GameSession.Normalize` remove marcas diacríticas (`NormalizationForm.FormD` + `UnicodeCategory.NonSpacingMark`), converte letras para minúsculas (`ToLowerInvariant`) e normaliza espaços, garantindo equivalência com ou sem acentos e maiúsculas. Se todas conferirem, `Recall()` aplica `+500 + 5 * Hp` e avança para `Phase.Results`. Se houver divergência, `MainForm` preserva o texto digitado, destaca os campos e abre o modal **"Você tem certeza?"**; confirmando `"Enviar mesmo assim"`, `SubmitRecallWithErrors()` avança concedendo apenas `+100` por acerto individual (sem `+500` nem `+5 * Hp`).

### Gerenciamento de Imagens, Eventos e Temporização
- **`ArtAssets` + `atlas.json`**: na inicialização, `ArtAssets` lê `atlas.json`, recorta cada frame em `PixelFormat.Format32bppArgb` e aplica `MaskBorder` (flood-fill nas bordas quando `Transparent: true`). Os cenários escalados têm cache de até 2 bitmaps em `Arena` para não reprocessar imagens de `1536×1024` a cada quadro.
- **Temporização (`Arena`)**: um `Timer` (`25 ms`) aliado a `Stopwatch` controla a ociosidade dos inimigos e a apresentação de `650 ms` de cada golpe (`AttackDuration = .65`). Quando `MotionEnabled == false`, o tempo de feedback cai para `120 ms` e poses ficam estáticas. O timer não aplica dano nem altera pontuação — apenas dispara `AttackFinished` ao concluir o último golpe para que `MainForm` abra a tela de memória ou Game Over.

### Decisões Técnicas, Vantagens e Limitações
- **Resolução imediata por tecla vs confirmação prévia**: em vez de acumular um buffer de sequência e exigir botão de confirmar a cada ataque (como descrito no esboço inicial), optou-se por `1 tecla = 1 ataque` com efeito imediato. *Vantagem*: resposta fluida e zero perda de inputs rápidos; *Limitação*: não permite desfazer a tecla após pressioná-la.
- **Campanha em 3 fases (4/6/8 ataques, 4 posições) vs tabela protótipo de 6 níveis**: consolidou-se a progressão em 3 encontros distintos (Caranguejo, Tubarão e Leviatã) com 4 direções `WASD` desde o início e pergunta acumulada no Boss.
- **Permissão de avanço com erro via confirmação**: para evitar que o jogador fique preso caso esqueça uma palavra do diário, adicionou-se confirmação explícita com penalidade na pontuação em vez de obrigar a repetir toda a fase.

---

## 10. Testes Realizados e Limitações Conhecidas

- **Compilação (`MSBuild`, `Release`)**: solução compilada com `0` erros.
- **Suíte Automatizada de Auditoria e Regras (`33/33` aprovados)**: validou carregamento do `atlas.json` e sprites HD, campanha completa de 3 fases (`5.700` pontos), pergunta acumulada do Boss, 3 e 6 esquivas consecutivas, cura limitada a `100 HP`, quebra de combo por erro, dano normal (`−20`) e de Boss (`−30`), `Game Over` em `HP 0` bloqueando ações, normalização de acentos/maiúsculas, persistência e fallback de `settings.json` e `save-v3.json` (incluindo `.bak` e JSON corrompido), transições de áudio e isolamento do tutorial.
- **Suíte Automatizada de Interface (`24/24` aprovados)**: validou logo e ícone da janela/executável, início e retomada de áudio nas configurações, disparo de sons de hover/seleção nos botões, estado desabilitado de *Continuar* sem save, modal de erro ao ler save inválido, bloqueio de autorrepetição (bit 30), bloqueio de comandos em modais, fluxo `"Você tem certeza?"` preservando texto digitado e alternância `F11`/`Esc`.
- **Limitações conhecidas**: a audibilidade subjetiva nos alto-falantes e o comportamento visual em monitores físicos com escalas DPI de `150%`/`200%` no Windows não foram testados fisicamente neste ambiente (que opera em display único de `96 DPI`), tendo sido validados apenas os cálculos proporcionais via `DeviceDpi / 96f` e renderizações em `640×480`, `1280×800` e `1920×1080`.
