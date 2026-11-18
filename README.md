# Fisherman’s Memory

> Aventura litorânea 2D em pixel art de memória tática e narrativa, desenvolvida em **C# 7.3**, **Windows Forms** e **.NET Framework 4.7.2**.

---

## Índice

1. [Visão Geral e Mecânicas](#1-visão-geral-e-mecânicas)
2. [Estrutura de Pastas e Arquivos](#2-estrutura-de-pastas-e-arquivos)
3. [Requisitos, Compilação e Execução](#3-requisitos-compilação-e-execução)
4. [Controles e Fluxo da Partida](#4-controles-e-fluxo-da-partida)
5. [Fases, Sequências Fixas, Vida e Pontuação](#5-fases-sequências-fixas-vida-e-pontuação)
6. [Interpretações de Pontos Indefinidos no Documento](#6-interpretações-de-pontos-indefinidos-no-documento)
7. [Funcionalidades e Resumo das Alterações](#7-funcionalidades-e-resumo-das-alterações)
8. [Artes, Áudio e Fontes](#8-artes-áudio-e-fontes)
9. [Salvamento de Progresso e Configurações](#9-salvamento-de-progresso-e-configurações)
10. [Explicação Técnica para Apresentação](#10-explicação-técnica-para-apresentação)
11. [Testes Realizados e Limitações Conhecidas](#11-testes-realizados-e-limitações-conhecidas)

---

## 1. Visão Geral e Mecânicas

**Fisherman’s Memory** é um jogo de aventura e memória tática ambientado em uma ilha litorânea cujas histórias estão desaparecendo sob o domínio do **Leviatã do Esquecimento**. Ao longo de **6 fases**, cada confronto combina duas formas de memorização:

- **Memória semântica / contextual (Diário de Expedição)**: antes do combate, o jogador recebe uma informação textual inicial (frase curta, média, longa ou duas informações distintas na Fase 5), sem destaque visual nas palavras-chave.
- **Memória de trabalho / padrão espacial (Combate)**: o guardião da região revela a sequência exata de teclas seguras que o jogador deve memorizar para escapar dos ataques (`A ←` Esquerda, `S ↓` Centro e `D →` Direita; na Fase 1 apenas Esquerda e Direita). Após ocultar o padrão, o jogador escolhe sua posição e confirma cada ação com `Enter` ou no botão `✓ CONFIRMAR [ENTER]`, sem limite de tempo nem exigência de reflexos rápidos.
- **Recuperação de memória**: ao sobreviver a todos os ataques da fase, o jogador responde às perguntas correspondentes às informações apresentadas. No **Boss final (Fase 6)**, são cobradas informações acumuladas efetivamente apresentadas nas fases 1 a 5. Caso erre a recuperação da informação, o jogador repete a fase atual.

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
        │   ├── GameSession.cs           # Máquina de estados, combate, sequências fixas/boss, HP, combo e repetição
        │   └── Levels.cs                # Definição das 6 fases, posições, sequências fixas (1–5) e geração do Boss
        ├── Infrastructure/
        │   ├── ArtAssets.cs             # Carregamento do atlas.json, recortes em memória e máscaras
        │   ├── AudioService.cs          # Reprodução simultânea via Windows MCI + fallback SoundPlayer
        │   └── SaveService.cs           # Persistência atômica JSON (save-v3.json e settings.json)
        ├── UI/
        │   ├── CoastalTheme.cs          # Paleta litorânea, fontes embarcadas, molduras, nomes/ícones de 2–3 posições
        │   ├── CoastalControls.cs       # Botões, cartões, entradas de resposta, mapa 2×3 de 6 fases e painel de sequência
        │   ├── MainForm.cs              # Orquestração de telas, confirmação por Enter, modais e tutorial sem Random
        │   ├── GameHud.cs               # Barra superior (fase 1–6, ataques até 16, vida, pontos, combo e menu)
        │   └── Arena.cs                 # Cenário, sprites pixel art dos 6 guardiões, seleção por mouse e botão Confirmar
        └── Assets/
            ├── Art/                     # Cenários, sprites HD RGBA (caranguejo, tubarão, pelicano, arraia, moreia, leviatã), atlas.json, logo e app.ico
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
3. Pressione **F5** no Visual Studio ou execute `FishermansMemory.exe` na raiz do repositório (ou `src\FishermansMemory\bin\Release\FishermansMemory.exe`).
4. Para distribuição avulsa, copie todo o conteúdo de `src\FishermansMemory\bin\Release\` (executável, DLLs, `.exe.config` e pasta `Assets\`).

---

## 4. Controles e Fluxo da Partida

### Fluxo Completo
`Menu Principal` → `Introdução (2 páginas)` → `Tutorial (3 etapas com prática isolada)` → `Informação Inicial (Diário)` → `Revelação do Padrão de Esquivas` → `Ocultação e Combate (Escolha + Confirmação)` → `Recuperação de Memória` *(repete a fase se errar)* → `Resultados` → `Mapa de Progressão (6 regiões)` → próxima fase (até `Vitória` após a Fase 6 ou `Derrota` se `HP = 0`).

No **Boss final (Fase 6)**, o combate é dividido em duas ondas de 8 ataques: o chefe apresenta a 1ª sequência de 8 ataques, executa os 8 golpes após ocultação, depois apresenta separadamente a 2ª sequência de 8 ataques, executa os 8 golpes restantes e, ao final dos 16 ataques, abre a recuperação de memória acumulada.

### Controles
- **Seleção de posição em combate e prática**:
  - `A` ou `←`: **Esquerda**
  - `S` ou `↓`: **Centro** *(habilitada da Fase 2 em diante; oculta e bloqueada na Fase 1)*
  - `D` ou `→`: **Direita**
  - Clique direto sobre uma plataforma disponível na arena também seleciona a posição.
  - A tecla `W` / `↑` não é utilizada.
- **Confirmação da esquiva**: `Enter` ou clique no botão `✓ CONFIRMAR [ENTER]` na barra inferior de combate. Somente após a confirmação o inimigo executa o ataque correspondente.
- **Menus, formulários e respostas**: mouse, `Tab` (navegação), `Enter` / `Espaço` (confirmar) e `Esc` (fechar modais, abrir opções em partida ou sair da tela cheia).
- **Tela cheia**: `F11` ou `Alt+Enter` alternam modo sem bordas preservando tamanho/posição anteriores.

---

## 5. Fases, Sequências Fixas, Vida e Pontuação

### Progressão das 6 Fases

| Fase | Região / Guardião | Ataques | Posições | Informação Inicial | Padrão de Esquivas | Dano por Erro |
|---|---|---:|---|---|---|---|
| **1** | Praia das Conchas / **Caranguejo sentinela** *(original)* | 3 | 2 (`A ←`, `D →`) | Frase curta (1 pista: farol azul) | Fixo: `A ←, D →, A ←` | −20 HP |
| **2** | Ilha Rochosa / **Tubarão das rochas** *(original)* | 4 | 3 (`A ←`, `S ↓`, `D →`) | Frase média (1 pista: chave na gruta) | Fixo: `D →, S ↓, A ←, S ↓` | −20 HP |
| **3** | Cais das Marés / **Pelicano das marés** *(novo)* | 5 | 3 (`A ←`, `S ↓`, `D →`) | Frase média (1 pista: bússola sob o cais) | Fixo: `S ↓, A ←, D →, S ↓, A ←` | −20 HP |
| **4** | Recife das Correntes / **Arraia dos recifes** *(novo)* | 6 | 3 (`A ←`, `S ↓`, `D →`) | Frase longa (1 pista: estrela Dalva) | Fixo (padrões parecidos com inversão): `A ←, S ↓, D →, A ←, D →, S ↓` | −20 HP |
| **5** | Gruta Submersa / **Moreia das profundezas** *(novo)* | 7 | 3 (`A ←`, `S ↓`, `D →`) | 2 informações distintas (pérola dourada; sino ao amanhecer) | Fixo complexo: `D →, A ←, S ↓, D →, S ↓, A ←, D →` | −20 HP |
| **6 (Boss)** | Coração da Ilha / **Leviatã do Esquecimento** *(original)* | 16 *(2 × 8)* | 3 (`A ←`, `S ↓`, `D →`) | 5 informações acumuladas das fases 1 a 5 | Aleatório exclusivo com `System.Random` (2 sequências de 8 armazenadas antes de apresentar) | −30 HP |

### Sistema de Vida e Pontuação
- **HP inicial e máximo**: `100 HP`, preservado entre batalhas e limitado ao máximo de `100 HP`. Ao atingir `0 HP`, o jogo encerra em **Game Over**.
- **Erro de posicionamento**: `−20 HP` contra inimigos normais (Fases 1 a 5), `−30 HP` contra o Boss (Fase 6) e `−50 pontos` (zera o contador de acertos consecutivos).
- **Esquiva correta**: não perde HP e soma `+100 pontos`.
- **Três esquivas consecutivas (Combo ×3)**: recupera `+50 HP` (limitado ao máximo de `100 HP`) e concede `+150 pontos` adicionais.
- **Recuperação correta da informação inicial**: concede `+500 pontos` e `+5 pontos` para cada ponto de HP restante ao final da fase. Se errar qualquer pergunta da fase, repete a fase atual mantendo o HP restante e restaurando a pontuação ao valor de início da fase.
- **Sem bônus por tempo/velocidade**.
- **Campanha perfeita (6 fases sem erros)**: **12.050 pontos** (`41 × 100` de esquivas + `13 × 150` de combos ×3 + `6 × (500 + 5 × 100)` de memória e vitalidade).

---

## 6. Interpretações de Pontos Indefinidos no Documento

Para aspectos que o documento `JOGO DA MEMORIA.pdf` deixou em aberto, adotaram-se explicitamente as definições abaixo:

1. **Quantidade de ataques do Boss**: o documento especifica apenas *"padrões múltiplos"* na tabela de escalamento sem definir um número de golpes. Seguindo a definição complementar deste projeto, o Boss utiliza **duas sequências de 8 ataques** (16 ataques no confronto completo), apresentadas e executadas uma após a outra.
2. **Mapeamento da tecla da posição central**: o documento menciona *"Setas direcionais (ou WASD)"* e posições *Esquerda / Centro / Direita*, sem especificar qual tecla representa o centro. Padronizou-se `A / ←` (Esquerda), `S / ↓` (Centro) e `D / →` (Direita), sem utilizar `W` como quarta posição.
3. **Base de cálculo de *"recupera 50% do HP"***: o documento não explicita se 50% incide sobre o HP máximo, o HP atual ou o HP perdido. Manteve-se a regra já implementada no motor original: **50% do HP máximo (`100`) = `+50 HP`**, respeitando o limite máximo de `100 HP`.
4. **Repetição do bônus de 3 esquivas consecutivas**: o documento não define se o bônus se repete após ser alcançado. Manteve-se a implementação cíclica do projeto original: ao atingir 3 acertos seguidos, o bônus (`+50 HP` e `+150 pts`) é aplicado e o contador retorna a `0`, podendo ser reativado a cada nova sequência de 3 acertos consecutivos (inclusive mantendo a contagem ao avançar de fase).
5. **Estado ao repetir a fase por erro na recuperação de memória**: ao errar a recuperação da informação após sobreviver ao combate, o jogador retorna ao início da mesma fase (`Phase.Information`) preservando o HP restante e restaurando pontuação e contador de combo ao estado do início da fase (`LevelStartScore` e `LevelStartCombo`) para evitar acúmulo artificial de pontos por repetições intencionais.

---

## 7. Funcionalidades e Resumo das Alterações

- **Expansão de 3 para 6 fases (`Core/Levels.cs`, `Core/GameSession.cs`)**: preservação dos dois inimigos originais nas Fases 1 e 2 e do Leviatã como Boss na Fase 6, com adição de Pelicano das marés (Fase 3), Arraia dos recifes (Fase 4) e Moreia das profundezas (Fase 5), totalizando 3, 4, 5, 6, 7 e 16 (2×8) ataques.
- **Aleatoriedade isolada no Boss**: fases 1 a 5 usam exclusivamente sequências fixas (`Level.FixedPattern`) e o tutorial usa sequências fixas alternadas; `System.Random` é usado unicamente em `Levels.GenerateBossSequence` para gerar cada sequência de 8 ataques do Boss antes de exibi-la ao jogador.
- **Três novos inimigos em pixel art (`Assets/Art/` + `atlas.json` + `UI/Arena.cs`)**: geração e integração dos sprites `pelican-*`, `stingray-*` e `moray-*` (`idle`, `idle2` e `attack` em `320×300` RGBA transparente), mantendo proporções, contornos escuros, olhos âmbar e temática costeira, além de animações próprias de ociosidade e efeitos de golpe em pixel art.
- **Padronização de controles e confirmação (`UI/MainForm.cs`, `UI/Arena.cs`, `UI/CoastalTheme.cs`)**: remoção da posição superior (`W`/`Up`), restrição da Fase 1 a Esquerda/Direita, seleção por tecla/mouse e execução do golpe somente após confirmação (`Enter` ou botão `✓ CONFIRMAR [ENTER]`).
- **Mapa, HUD, Revelação e Recuperação (`UI/CoastalControls.cs`, `UI/GameHud.cs`, `UI/MainForm.cs`)**: mapa náutico em grade `2×3` para 6 fases, cartões de revelação indicando explicitamente a **TECLA SEGURA / FUGA**, marcadores de até 16 ataques no HUD e repetição obrigatória da fase ao errar a recuperação de memória.

---

## 8. Artes, Áudio e Fontes

- **Artes (`src/FishermansMemory/Assets/Art/`)**: `beach-hd.png`, `rocks-hd.png`, `boss-clean-hd.png`, `leviathan-sprite.png`, `crab-idle-hd.png`, `crab-idle2-hd.png`, `crab-attack-hd.png`, `shark-idle-hd.png`, `shark-idle2-hd.png`, `shark-attack-hd.png`, `pelican-idle-hd.png`, `pelican-idle2-hd.png`, `pelican-attack-hd.png`, `stingray-idle-hd.png`, `stingray-idle2-hd.png`, `stingray-attack-hd.png`, `moray-idle-hd.png`, `moray-idle2-hd.png`, `moray-attack-hd.png`, `hero-idle-hd.png`, `hero-dodge-hd.png`, `hero-hurt-hd.png`, `hero-dead-hd.png`, `splash.png`, `logo.png`, `app.ico` e `atlas.json`.
- **Áudio (`src/FishermansMemory/Assets/Audio/`)**: `menu_calm.wav`, `ocean.wav`, `combat_battle.wav`, `boss_battle.wav`, `victory.wav`, `gameover.wav`, `hit.wav`, `combo.wav`, `damage.wav`, `ui_hover.wav` e `ui_select.wav`.
- **Fontes (`src/FishermansMemory/Assets/Fonts/`)**:
  - **Pixelify Sans** (`PixelifySans.ttf`, licença **SIL OFL 1.1** em `PixelifySans-OFL.txt`)
  - **Chakra Petch** (`ChakraPetch-Regular.ttf`, `ChakraPetch-Bold.ttf`, licença **SIL OFL 1.1** em `ChakraPetch-OFL.txt`)

---

## 9. Salvamento de Progresso e Configurações

Os arquivos são gravados em `%LOCALAPPDATA%\FishermansMemory\` por `SaveService`:
- `save-v3.json` (com backup `.bak` via escrita temporária + `File.Replace`): armazena versão, fase atual (`0` a `5`), HP, pontuação, combo, valores de início de fase (`LevelStartScore`, `LevelStartCombo`), padrão armazenado (`Pattern`), respostas já confirmadas (`Input`), contador `Resolved` e resumo de recuperação. `GameSession.Restore()` valida que fases normais coincidem exatamente com `FixedPattern` e que o Boss possui sequência válida de 8 ou 16 ataques consistente com `Resolved`.
- `settings.json`: persiste `AudioEnabled`, `MotionEnabled` e `BestScore`.

---

## 10. Explicação Técnica para Apresentação

- **Camada `Core` (`Levels.cs`, `GameSession.cs`)**: independente de WinForms/GDI+. Centraliza a tabela das 6 fases, validação de transições (`Require(Phase)`), normalização de texto (`FormD` sem acentos/maiúsculas/pontuação), aplicação das regras de vida/pontuação e geração aleatória isolada no método `Levels.GenerateBossSequence`.
- **Camada `Infrastructure` (`ArtAssets.cs`, `AudioService.cs`, `SaveService.cs`)**: carrega `atlas.json` validando existência de `scene.0..5`, `enemy.0..5`, `.idle` e `.attack`; reproduz música, mar e SFX simultaneamente via MCI (`winmm.dll`); realiza escrita atômica de progresso e configurações.
- **Camada `UI` (`MainForm.cs`, `Arena.cs`, `GameHud.cs`, `CoastalControls.cs`, `CoastalTheme.cs`)**: renderização 2D com `InterpolationMode.NearestNeighbor` para nitidez da pixel art, seleção prévia de posição (`SelectCampaignLane`) desacoplada da confirmação (`Enter` / `ConfirmRequested` → `MoveAndAttack`), exibição separada das duas ondas de 8 ataques no Boss e layout responsivo testado em `640×480`, `1280×800` e `1920×1080`.

---

## 11. Testes Realizados e Limitações Conhecidas

- **Compilação (`MSBuild 17.14`, `Debug` e `Release`)**: solução compilada com `0` erros e cópia automática de todos os novos PNGs e `atlas.json` para `bin\Debug\Assets\Art\` e `bin\Release\Assets\Art\`.
- **Suíte Automatizada de Validação das 6 Fases (`161/161` verificações aprovadas)**:
  - Auditoria de código-fonte confirmando `System.Random` restrito exclusivamente à geração das sequências do Boss (`Levels.cs` e `GameSession.cs`) e enum `Lane` contendo apenas `Left`, `Center` e `Right`.
  - Carregamento de `ArtAssets` para `scene.0..5` e `enemy.0..5`, confirmando frames distintos de ociosidade e ataque para os guardiões das fases 1 a 5.
  - Validação da progressão de 6 fases com `3, 4, 5, 6, 7` e `16` (`2×8`) ataques, 2 posições na Fase 1 (`Left`, `Right` com rejeição de `Center`) e 3 posições nas Fases 2 a 6, reprodutibilidade idêntica das sequências fixas ao repetir fases normais e cobrança no Boss apenas de informações apresentadas nas fases anteriores.
  - Validação da geração de duas sequências de 8 ataques do Boss armazenadas antes da revelação, apresentadas e executadas separadamente sem alteração após reveladas, e regeneração aleatória ao reiniciar com semente diferente.
  - Validação de HP (`100` inicial, `−20` normal, `−30` boss, `+50` a cada 3 esquivas limitando em `100`, `Game Over` em `0`), pontuação (`12.050` pontos na campanha perfeita), repetição obrigatória em `RepeatCurrentLevel()` e round-trip de `SaveService`.
  - Validação de interface em `MainForm`: seleção com `A`/`D`/`S` sem execução prematura, ignorar `S` na Fase 1 e `W` em todas as fases, confirmação exclusiva via `Enter`, e renderização completa via `DrawToBitmap` em `640×480`, `1280×800` e `1920×1080` (com inspeção visual de capturas para combate, efeito de golpe, revelação, mapa de 6 fases e recuperação de memória).
- **Limitações conhecidas**: não foi realizado teste interativo com usuário físico em monitores com escalas DPI diferentes de `100%` (`96 DPI`) nem auditoria subjetiva de áudio em alto-falantes neste ambiente automatizado.
