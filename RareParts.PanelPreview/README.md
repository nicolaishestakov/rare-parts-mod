# Transfer panel preview

A Windows desktop test application for `TransferSettingsPanel`. Requires the .NET 10 SDK to build, or the .NET 10 Windows Desktop Runtime to run the built executable. No game installation, Unity editor, or MelonLoader is required.

From the repository root:

```powershell
dotnet run --project RareParts.PanelPreview
```

Alternatively, build the project in Rider and run `RareParts.PanelPreview`, or launch `RareParts.PanelPreview/bin/Debug/net10.0-windows/RareParts.PanelPreview.exe` after building.

The panel opens immediately. Try its buttons, mode and condition shortcuts, L, Shift+L, Escape and F7. Shopping-list-only filtering is selected through the mode control; Shift+L returns collected items to junk piles. Move maps and Move cases toggle independently in every mode. Transfers are simulated and reported below the panel. The toolbar toggles whether the simulated scene is a barn/junkyard. Resize the window to check scaling. Closing the panel leaves the application running; F7 reopens it.

## What is shared

The project **links the production `TransferSettingsPanel.cs`, `ITransferAllConfig.cs`, and `PartsInfo.cs`**. Text, geometry, styles, cursor, panel state and action dispatch therefore come from the actual source; rebuild the preview after editing it. `UnityPreview.cs` implements only the IMGUI subset used by this panel using Windows drawing. `GameStandIns.cs` supplies simulated config and transfer operations. Neither adapter is compiled into the mod.

This is a layout and interaction tool, not a pixel-exact Unity emulator. Unity skin textures, font rasterization, game cursor layering, Harmony hooks, native scene lifecycle and real item transfers still require in-game testing. New Unity APIs used by the panel may need corresponding preview adapter support.

## Font

The game's font is selected from loaded game controls at runtime, so it is unavailable in this standalone process. The preview names its fallback font at the bottom. To use a local TTF/OTF file:

```powershell
dotnet run --project RareParts.PanelPreview -- --font "C:\path\to\font.ttf"
```

No game font is bundled or automatically extracted by this application.

## Automated smoke check and screenshots

```powershell
dotnet build RareParts.PanelPreview
New-Item -ItemType Directory -Force artifacts/panel-preview | Out-Null
dotnet RareParts.PanelPreview/bin/Debug/net10.0-windows/RareParts.PanelPreview.dll --self-test --screenshot artifacts/panel-preview/preview.png
```

The smoke check exercises the actual panel controls, deferred single-execution transfers, shopping-list selection, Shift+L, Close, Escape, scene gating and scaled click targeting at 640×480. A failed assertion exits with code 1. The screenshot is generated without opening a window; use `--screenshot` alone to capture the initial panel. `--font` can be combined with either option. Screenshots go only to the path you specify.
