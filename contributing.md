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
