# 50 Appraisal Buttons (+50/-50)

Adds +50 and -50 buttons to the appraisal panel. The plus buttons sit in a row above the estimate field, the minus buttons in a row below it, and the field is bigger and easier to read.

## Building (Windows Command Prompt)

You need:
- The [.NET SDK](https://dotnet.microsoft.com/download), 6.0 or later. Check with `dotnet --version`.
- Probably Stolen installed. The build compiles against the game's own DLLs.

1. Open **Command Prompt** in this mod's folder inside the repository:

   ```bat
   cd /d "C:\path\to\EGibson-Probably-Stolen-Mods\50 - Appraisal Mod"
   ```

2. Tell the build where the game is: the folder that contains `Probably Stolen.exe`. Do this once, by writing it
   into a local `SENSITIVE.md`. That file is gitignored, so your path never gets committed.

   ```bat
   echo GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen> SENSITIVE.md
   ```

   - Use your own path if the game is in another Steam library, e.g. `D:\SteamLibrary\steamapps\common\Probably Stolen`.
   - There must be no space before the `>`, or the path gets a trailing space.

3. Build:

   ```bat
   dotnet build -c Release
   ```

   This compiles `50AppraisalButtons.dll` and installs it with `manifest.xml` into
   `<game>\Mods\50AppraisalButtons\`.

4. In the game, open the **Mods** menu, select **50 Appraisal Buttons (+50/-50)**, press **Activate**, then restart
   the game.

Other build commands:

```bat
:: Build without installing into the game
dotnet build -c Release -p:InstallMod=false

:: Use a game folder without SENSITIVE.md
dotnet build -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Probably Stolen"

:: Build the release packages (Steam Workshop folder and Nexus zip) into
:: "packages\50 - packages\" at the repository root, next to this folder
dotnet build -c Release -t:Package
```

If the build stops with "Game DLLs not found", the `GAME_DIR` path is wrong. Open `SENSITIVE.md` and check that the
line points at the folder with `Probably Stolen.exe` in it.
