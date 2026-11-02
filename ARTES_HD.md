# Origem dos cenários HD

Gerados com a ferramenta integrada de geração de imagens, usando a prancha campaign.png como referência de estilo. São recriações, não restauração exata dos pixels originais.

Arquivos finais dentro do projeto:
- FishermansMemory/Assets/Art/beach-hd.png — 1536 × 1024.
- FishermansMemory/Assets/Art/rocks-hd.png — 1536 × 1024.
- FishermansMemory/Assets/Art/boss-hd.png — 1536 × 1024.

## Prompts utilizados

### Praia
Create ONE high-resolution 1536x1024 or larger landscape game background asset. Reference sheet is style reference only. Recreate the FIRST BEACH environment as a full expansive landscape, detailed polished pixel art, azure tropical coastline, palm trees and small fisherman wooden hut to left, wooden jetty, rocks, water. No text, no labels, no panels, no UI, no characters. Full bleed landscape composition suitable for fullscreen C# game. Lower half spacious sandy foreground for sprites. Preserve original warm coastal pixel art aesthetic but much finer rich detail, sharp edges. This is a single beach scene, not a spritesheet.

### Ilha rochosa
Create ONE high resolution 1536x1024 or larger landscape pixel art game environment. Use the FOURTH ROCKY ISLAND environment in reference as stylistic guide. Expand into a full landscape with crisp highly detailed pixel art: dramatic rocky coastal cliffs, blue turquoise surf, rope bridge and little glowing lighthouse in distance. Broad dark rocky ground in lower half for gameplay sprites, no characters or enemies, no text, no labels, no panels or UI. Single immersive fullscreen landscape, preserve original coastal adventure style. More detail than tiny original, not blurry enlargement.

### Boss
Create ONE high resolution 1536x1024 or larger landscape pixel-art game background. Style reference is the LAST dark sea monster boss scene on reference sheet. Recreate as immersive widescreen environment, not spritesheet. Giant dark navy leviathan octopus sea monster with glowing red eyes and curling tentacles in stormy ocean, dramatic lightning, jagged coastal rocks. Place entire boss at horizontal 65 percent of frame, upper-middle, with all tentacle silhouette comfortably inside central frame, ample margins. Lower half an empty broad dark rocky shore/platform where player sprites can stand. Polished fine detailed crisp pixel art, preserve original ominous mood and design. No labels, no letters, no UI, no border, no player. Full bleed single scene.

## Revisão de animações

Novos assets gerados com a ferramenta integrada, usando boss-hd.png como referência:
- Assets/Art/boss-clean-hd.png: cenário sem chefe, 1536×1024.
- Assets/Art/leviathan-sprite.png: criatura isolada, 1774×887, PNG RGBA com transparência.

Prompt do cenário:
Edit this game background: remove the ENTIRE giant monster including every tentacle and glowing eye. Fill the removed area with continuous stormy ocean and dark clouded sky and lightning matching the original pixel art. Preserve the rocky shore/platform lower half and composition, lighting and all scenery. No creature, no tentacles, no text. High resolution landscape 1536x1024. This is the clean background plate behind a separately animated boss.

Prompt do sprite (transparent_background=true):
Extract/recreate ONLY the giant dark navy leviathan monster from reference as one isolated full-body game sprite on TRUE transparent background. Preserve its glowing red eyes, jagged broad head, dark navy detailed pixel art, several complete curling tentacles and menacing frontal design. No scenery, no rocks, no ocean, no lightning, no ground or frame. All tentacles inside canvas with generous transparent margin. Front facing, symmetric broadly horizontal silhouette, suitable for idle scaling and attack animation in a 2D game. One pose only, crisp pixel art.
