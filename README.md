# Path the spire2

Path the spire2 is a Slay the Spire 2 mod project scaffold for drawing route guidance on the map.

Right now this repo is intentionally set up as a minimal starting point:

- Godot 4.5 C# project
- Harmony patch entry point
- `NMapScreen` hook that attaches a custom `MapPathSystem`
- automatic copy of the built DLL and `mod_manifest.json` into the game's `mods/PathTheSpire2` folder

## Current Structure

- `Core/MapPathMod.cs`: mod initialization entry point
- `Core/MapPathSystem.cs`: placeholder system node for future map path logic
- `Patches/Patch_MapScreenReady.cs`: injects `MapPathSystem` into the map screen
- `MapPathMod.csproj`: build, game DLL reference, and post-build deployment

## Build

The project is currently configured against this game DLL path:

```text
D:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\sts2.dll
```

The post-build deploy target currently copies the mod to:

```text
D:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\PathTheSpire2
```

If your game is installed elsewhere, update both paths in `MapPathMod.csproj`.

Build with:

```powershell
dotnet build .\MapPathMod.csproj
```

Skip auto-deployment when needed:

```powershell
dotnet build .\MapPathMod.csproj -p:SkipModDeployment=true
```

## Next Step

The next logical step is to decide how the route should be represented:

- hover-based predicted path
- selected full route overlay
- best-path recommendation based on node scoring

Once you pick that, we can keep this structure and add only the path calculation and rendering pieces.
