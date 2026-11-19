# RoundsRandom

RoundsRandom is a BepInEx mod for ROUNDS that chooses cards for you.

## How it works

Rounds play normally: players fight for points, and the losing player gets a
card between rounds. The game deals its usual five choices; once they have all
appeared, the mod displays them for two seconds and picks one automatically.
Manual selection is blocked while the mod is enabled. If another mod changes
the number of offered cards, RoundsRandom chooses from that offer instead.

In the mod settings, `Enabled` turns automatic picking on or off (default:
`true`), and `Mode` selects one of:

1. `Random` (default): every offered card is equally likely.
2. `Worst`: choose the lowest rarity.
3. `Best`: choose the highest rarity.

For vanilla cards the order is Common < Uncommon < Rare; ties are broken
randomly. "Best" and "worst" refer only to rarity, not actual card strength.
Custom rarities from other mods are not supported as a ranked rarity scale.

In multiplayer, each player who is picking needs the mod enabled. Settings are
local, not synchronized by the host: install the mod on every client and agree
on the same settings before starting. Normal game networking applies the pick.

## Install and configure

Install [BepInExPack for ROUNDS](https://thunderstore.io/c/rounds/p/BepInEx/BepInExPack_ROUNDS/)
first. Put the built `RoundsRandom.dll` in the active profile's
`BepInEx/plugins/RoundsRandom/` directory, then launch the game through that
profile. For a manual installation this is `<game directory>/BepInEx/plugins/RoundsRandom/`.

Run the game once to create `BepInEx/config/com.dandol328.rounds.random.cfg`.
Edit `Enabled` and `Mode` under `[General]` while the game is closed, or use
a compatible BepInEx Configuration Manager to change these settings in game.
Setting `Enabled = false` restores normal manual picking.

* **Windows:** Install BepInExPack with r2modman or manually into the ROUNDS
  directory. Copy the DLL into the profile or game plugin directory above.
* **Linux:** Run the Windows game with Steam Proton. Install BepInExPack in
  the game directory or use a ROUNDS-compatible mod manager profile, then
  copy the DLL into that profile's plugin directory. For a manual Proton
  installation, set Steam launch options to
  `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
* **macOS:** ROUNDS is a Windows game; building the DLL on macOS does not
  provide a native macOS game. Use a Windows installation or a working
  Windows compatibility environment, install BepInExPack there, and copy
  the DLL into that installation's plugin directory. Compatibility layers
  are not guaranteed to run the game or loader.

## Build from source

Install a .NET SDK (8 or newer), the .NET Framework 4.7.2 reference assemblies
(or allow the SDK to restore them from NuGet), ROUNDS, and BepInExPack for
ROUNDS. The game and BepInEx DLLs are referenced locally and are not shipped
with this project. In a terminal at the repository root, set `ROUNDS_PATH`
to the directory containing `ROUNDS_Data/Managed`. Set `BEPINEX_PATH` to the
active profile's `BepInEx` directory if it is not inside the game directory.
Adjust paths for your Steam library and profile:

```powershell
# Windows PowerShell
$env:ROUNDS_PATH = "C:\Program Files (x86)\Steam\steamapps\common\ROUNDS"
# Optional: $env:BEPINEX_PATH = "C:\path\to\profile\BepInEx"
dotnet build RoundsRandom.csproj -c Release
```

```sh
# Linux
export ROUNDS_PATH="$HOME/.steam/steam/steamapps/common/ROUNDS"
# Optional: export BEPINEX_PATH="$HOME/.config/r2modmanPlus-local/ROUNDS/profiles/Default/BepInEx"
dotnet build RoundsRandom.csproj -c Release

# macOS: use the game files from your Windows/compatibility installation
export ROUNDS_PATH="/path/to/ROUNDS"
# Optional: export BEPINEX_PATH="/path/to/profile/BepInEx"
dotnet build RoundsRandom.csproj -c Release
```

Copy `bin/Release/net472/RoundsRandom.dll` to the plugin directory as described
above. Private game APIs can change with game updates; test with the installed
game version and other card-pick mods.
