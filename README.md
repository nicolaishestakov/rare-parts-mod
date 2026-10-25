# rare-parts-mod

A mod for Car Mechanic Simulator 2021 which removes certain parts from shops, adds more repairement capabilities and adds smart transfer commands to manage inventory, warehouse and shopping list.
The mod is inspired by https://github.com/microup/realshop and https://github.com/mannly01/TransferAll mods. Inherits some features and default configurations from the mentioned mods (though heavily modified), so they are not recommented to run together with this one.

## Features

1. 'Rare parts' are removed from shops. Works similar to RealShop mod, you may define the list of rare parts and cars in the config file. The default list of rare parts, and their price modifiers are taken from RealShop mod with slight modifications.
2. Special repairable parts. To compensate for the rarity, non-repairable rare parts are repairable starting from 80% condition (the level is configurable) at repair skill level 6. In addition, all non-repairable body shop items (windows, lights), interior items, and tires are repairable from the same 80% condition at repair level 6. Since the game does not let setting minimum condition for repairement, the special items are displayed in the repairement window when their condition is >= 15%, but are disabled unless the condition is at least 80%. When disabled, the item will show that repair skill 6 is required, but it is just a hacky way to block the item.
3. Scrap-repair. To compensate for the rare parts that are difficult to find in barns or junkyards, parts may be repaired to some degree from multiple items. For example, if you have two 10% identical items, they can be converted into one item with condition 19%. Go to salvage container and press 'R', it will select some identical items and offer you to combine them into one with higher condition. Scrap-repairement works for the items that are below their repairability condition (15% for usual items, 80% by default for special parts), meaning that the ones that can be repaired the usual way, are not candidates for scrap-repairement. If some item is selected in salvage container window, the item will be taken as the first candidate for scrap-repair, and as a reference for the result (it will inherit subproperties, like upgrades or wheel size parameters).
4. Smart transfer modes to support the gameplay:
 - Open the whole barn/junkyard transfer panel with `F7` (configurable as `TransferSettingsKey`). It displays the live transfer mode, minimum condition, config category filters, and repair thresholds. Change mode/condition with the buttons or existing hotkeys, optionally select shopping-list-only, and click **Transfer whole location to shopping cart**. Close with F7, Escape, or Close. The button always collects from all piles, even if the inventory or a pile's collected tab is open; purchases still happen through the game's shopping cart. Existing K/L shortcuts retain their behavior.
 - Transfer all items from a barn or junkyard to shopping cart with a condition filter (inherited from TransferAll mod). Executed by 'L' by default, choose the mode via Keypad Multiply.
 - Transfer all items from a barn or junkyard to shopping cart including repairable rare parts only. Executed by 'L' by default, choose the mode via Keypad Multiply.
 - Transfer all items from a barn or junkyard to shopping cart including parts from shopping list (of any condition). Executed by 'Shift-L' by default.
 - Find items in warehouse that are in shopping list and move them to inventory. Executed by 'F6' by default. Finds new or repairable items, moves them to inventory and removes from shopping list. When pressed with 'Shift' finds non-repairanle items and moves to inventory, does not modify shopping list. The latter mode is for finding items for scrap-repair.
 - Add starred parts from the selected issue on the 'Issues to fix' tab to the shopping list with 'X' (configurable as `PartsToFixToShoppingListKey`). Includes all pages of that issue. Tops up each item/variant to the selected quantity (e.g. two pads selected and one already listed adds one), preserves tire/rim specifications, and shows the number added. Existing quantities above the selection are left unchanged; pressing X again adds nothing. Body and service rows have no stars in the game and are not included.

## Requirements

Works with Melon Loader 0.5.7 (so it can be run together with QoL mod).

Install `RareParts.dll` in the game's `Mods` directory and `RareParts.Core.dll` in its `UserLibs` directory. The `Release-Deploy` configuration copies both assemblies to these locations.

For manual installation, use the `RareParts.Core.dll` copied alongside `RareParts.dll` in the mod's build output. This is the .NET Framework 4.8 build; the .NET Standard build requires a `netstandard` assembly that the game's Mono runtime does not provide. When upgrading, replace both DLLs.

## Project structure

- `RareParts.Core` targets .NET Framework 4.8 for the mod and .NET Standard 2.0 for standalone tests, and contains the scrap-repair calculator, scene classification, part metadata, string helpers, and logging abstractions. It has no game, Unity, or MelonLoader dependencies.
- `RareParts` targets .NET Framework 4.8 and contains game integration, Harmony patches, MelonLoader callbacks, preferences, UI, and the MelonLogger implementation. Building it requires the game assemblies.
- `RareParts.Tests` references `RareParts.Core` and runs independently of the game.

## Unit tests

For a standalone Windows preview of the transfer panel, run `dotnet run --project RareParts.PanelPreview`. It uses the actual panel source with simulated game actions. See [preview setup and limitations](RareParts.PanelPreview/README.md).

With the .NET 10 SDK installed, run:

```powershell
dotnet test RareParts.Tests/RareParts.Tests.csproj
```

The tests reference the production `RareParts.Core` assembly, so no game installation or Unity runtime is needed. They cover combined condition, repair thresholds, optimal part selection, duplicate parts, and inventory preservation. An independent exhaustive search also checks selection results on small inventories, allowing for the algorithm's existing log-weight rounding.

## Disclaimer

Some features have 'hacky' or awkward implementation (no proper UI, menus, etc) due to game limitations (or my unawareness how to do it better).
