# Shelf Mod for Data Center

Adds placeable shelves with per-slot item snapping.

## Features

- **4 shelf sizes**: Small (0.8m), Medium (1.2m), Large (1.8m), Wide (2.4m)
- **Grid snapping** — shelves snap to a configurable grid when placing
- **Wall snapping** — shelves align to walls automatically
- **Per-slot item snapping** — each tier has 3 snap zones where items lock into position
- **Translucent preview** — see exactly where the shelf will go before placing
- **Configurable** — all settings in MelonPreferences.cfg

## Installation

1. Build the mod (see below) or use the pre-built DLL
2. Copy `ShelfMod.dll` into `Data Center/Mods/`
3. Launch the game — MelonLoader will load the mod automatically

## Controls

| Key | Action |
|-----|--------|
| F7 | Toggle placement mode |
| LMB | Place shelf |
| RMB | Cancel placement |

## Configuration

Edit `UserData/MelonPreferences.cfg` under `[ShelfMod]`:

```ini
[ShelfMod]
PlacementKey = F7
ShelfWidth = 1.2
ShelfDepth = 0.4
ShelfTiers = 3
GridSize = 0.5
```

## Building

### Prerequisites
- .NET 6.0 SDK (or later)
- Game installed at default Steam location

### Build
```bash
dotnet build -c Release
```

Output DLL will be in `ShelfMod/bin/Release/ShelfMod.dll`.

### Deploy
```bash
copy ShelfMod\bin\Release\ShelfMod.dll "C:\Program Files (x86)\Steam\steamapps\common\Data Center\Mods\"
```

## Item Snapping

To use item snapping on your own objects, make them enter the shelf's snap zone
trigger collider. The `ShelfSnapZone` component detects `OnTriggerEnter` and
highlights available slots. Release the item while near a highlighted zone to snap.

Items are frozen (kinematic Rigidbody) when snapped and unfrozen on release.

## How It Works

```
ShelfMod/
├── ShelfMod.cs          Main MelonMod entry point + placement logic
├── ShelfBuilder.cs      Procedural shelf geometry + mesh generation
├── ShelfSnapZone.cs     Per-slot trigger zone + snap/release logic
├── SnapManager.cs       Singleton tracking all zones, nearest-zone lookup
├── ShelfConfig.cs       Shelf type presets + constants
└── ShelfSaveHandler.cs  JSON save/load for placed shelves
```

### Shelf hierarchy (runtime)
```
ModShelf
├── LeftSupport       Vertical bracket (left)
├── RightSupport      Vertical bracket (right)
├── Tier_0            Horizontal plank
│   ├── Slot_0_0      SnapZone trigger (left)
│   ├── Slot_0_1      SnapZone trigger (center)
│   └── Slot_0_2      SnapZone trigger (right)
├── Tier_1
│   └── ...
├── Tier_2
│   └── ...
└── TopPlank          Top surface
```
