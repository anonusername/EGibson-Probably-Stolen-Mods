# Jerma Rats

A mod for **Probably Stolen** that brings your rats to life.

- **Animated rats:** every live rat breathes and twitches, and wiggles while you drag it.
- **Rat sounds:** rats get their own pick-up and put-down sounds.
- **The Biggest Rat:** the heaviest rat you own is crowned the Biggest Rat. It gets its own animated look and its
  own sounds. The crown moves as rats grow, age and die.
- **Settings:** each feature can be turned off in Mods → Jerma Rats → Configure.

The mod needs no special permissions (empty `<Capabilities />`).

## Building (Windows Command Prompt)

You need the [.NET SDK](https://dotnet.microsoft.com/download) 6.0 or later, and the game installed.

1. Open Command Prompt in this folder.
2. Tell the build where the game is, once. The path is the folder that contains `Probably Stolen.exe`.
   `SENSITIVE.md` is gitignored, so your path is never committed:
   ```bat
   echo GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen> SENSITIVE.md
   ```
3. Build and install into `<game>\Mods\JermaRats\`:
   ```bat
   dotnet build -c Release
   ```
4. In the game, open **Mods**, select **Jerma Rats**, press **Activate**, then restart the game.

Other commands:
```bat
:: Build without installing
dotnet build -c Release -p:InstallMod=false

:: Release packages into ..\packages\JermaRats - packages\
dotnet build -c Release -t:Package

:: Regenerate placeholder art and sound (never overwrites files you replaced)
python tools\make_placeholders.py
```

## Art and sound

Images go in `Textures\Items\`: PNG with transparency, pixel art, at the exact size of the game's rat sprite for
that size class. The table below covers the Biggest Rat's frames, `big_<size>_<n>.png`.

| Size | Weight | Pixels |
|---|---|---|
| `s` | under 180 g | 32 × 16 |
| `m` | 180–399 g | 48 × 16 |
| `l` | 400–599 g | 48 × 16 |
| `xl` | 600–999 g | 48 × 32 |
| `xxl` | 1000 g and up | 64 × 32 |

Sounds go in `Sounds\`: `rat_pickup.wav`, `rat_drop.wav`, `big_pickup.wav`, `big_drop.wav`. Use 16-bit PCM WAV at
44.1 kHz, 1 second or less. Each sound needs exactly one file per name.

The files shipped now are placeholders: checkered shapes and test blips.

`game-reference\`, if present, holds art and sound extracted from the game for reference. It belongs to the game's
developers and is gitignored; never commit or share it.
