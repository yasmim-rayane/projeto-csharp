# Guia visual — contraste, HUD e inimigos animados

## Campos de movimento

Plataformas WASD têm fundo escuro quase opaco, borda externa preta e contorno branco. A plataforma ocupada fica dourada. Teclas brancas aparecem em placas pretas. Após um input, os rótulos SEGURA e ATAQUE complementam verde/vermelho; nunca antecipam a próxima posição segura.

## HUD e informações

UI/GameHud.cs desenha coração, barra de HP com 20 segmentos, estrela/pontos e contador de combo em caixas separadas. Os valores também têm nome acessível. O HUD ocupa faixa própria acima do combate, independentemente da rolagem do painel textual.

Notas usam cartão cinza, corpo menor e espaçamento superior. Dicas, controles e resultado da última ação usam cartões azul-esverdeados e títulos distintos. Não foram acrescentados cronômetro, bombas ou inventário do anexo; a referência orienta o estilo visual.

## Animações

- Caranguejo: duas poses de ociosidade, pequena respiração; preparação, golpe e recuperação com cortes de pinças.
- Tubarão: duas poses de ociosidade, balanço leve; inclinação/investida e três cortes de água por área atingida.
- Leviatã: PNG transparente em camada separada, pulsação e inclinação; avanço e tentáculos desenhados por curvas em direção às posições atacadas. O céu e o chão não se movem junto com ele.
- Protagonista: continua sem caminhada automática ou deslizamento. Cada tecla muda diretamente sua posição.

Sprites reaproveitados da prancha têm recortes declarados no atlas; o Leviatã usa arte nova. As animações combinam troca de poses e transformações em código, não vídeos ou GIFs. O Boss usa uma pose-base deformada e efeitos procedurais, não uma sequência de quadros pintados à mão.

Cada input resolve as regras imediatamente e inicia uma apresentação de 650 ms. Novos pressionamentos podem substituir o efeito anterior; nenhuma ação aceita é perdida. O último efeito termina antes da recuperação de memória/Game Over. Sem limite de tempo para decidir.

Animações desligadas: pose fixa e feedback estático breve de 120 ms. Ociosidade não roda após derrota. Timer para fora do combate após transições e quando minimizado; fundos têm cache de até duas imagens escaladas e imagens não são lidas em cada quadro.

## Diário e compatibilidade

Passage em Core/Levels.cs contém um pequeno texto por fase. Informações relevantes estão entre detalhes narrativos e em posições diferentes das frases. As perguntas e respostas existentes continuam válidas, mantendo saves v3 compatíveis. Sequências 4/6/8, 1/2/3 respostas, HP e pontuação permanecem iguais.

## Arquivos de arte novos

Assets/Art/boss-clean-hd.png: cenário sem chefe, 1536×1024.
Assets/Art/leviathan-sprite.png: chefe isolado com transparência, 1774×887.
O boss-hd.png anterior permanece preservado como referência.

## Verificação no Windows

1. Compilar Release e executar FishermansMemory.Checks.
2. Testar 640×480, 1280×800, 1920×1080; DPI 100%, 150%, 200%.
3. Verificar HUD fora das plataformas e textos acessíveis por rolagem.
4. Conferir ociosidade e golpe de cada inimigo; alternar animações no menu.
5. Testar input rápido, tecla segurada, dano final, última esquiva e retomada do save.
6. Minimizar/restaurar durante efeito e alternar F11/Alt+Enter/Esc.
7. Ler os três textos, confirmar respostas sem acentos/maiúsculas e conferir que o texto fica oculto no combate.

Compilação e execução visual no Windows permanecem pendentes: este ambiente não dispõe de Windows nem compilador C#.
