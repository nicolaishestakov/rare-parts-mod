# rare-parts-mod

A mod for Car Mechanic Simulator 2021 which removes certain parts from shops, adds more repairement capabilities and adds smart transfer commands to manage inventory, warehouse and shopping list.
The mod is inspired by https://github.com/microup/realshop and https://github.com/mannly01/TransferAll mods. Inherits some features and default configurations from the mentioned mods (though heavily modified), so they are not recommented to run together with this one.

## Features

1. 'Rare parts' are removed from shops. Works similar to RealShop mod, you may define the list of rare parts and cars in the config file. The default list of rare parts, and their price modifiers are taken from RealShop mod with slight modifications.
2. Special repairable parts. To compensate for the rarity, non-repairable rare parts are repairable starting from 80% condition (the level is configurable) at repair skill level 6. In addition, all non-repairable body shop items (windows, lights), interior items, and tires are repairable from the same 80% condition at repair level 6. Since the game does not let setting minimum condition for repairement, the special items are displayed in the repairement window when their condition is >= 15%, but are disabled unless the condition is at least 80%. When disabled, the item will show that repair skill 6 is required, but it is just a hacky way to block the item.
3. Scrap-repair. To compensate for the rare parts that are difficult to find in barns or junkyards, parts may be repaired to some degree from multiple items. For example, if you have two 10% identical items, they can be converted into one item with condition 19%. Go to salvage container and press 'R', it will select some identical items and offer you to combine them into one with higher condition. Scrap-repairement works for the items that are below their repairability condition (15% for usual items, 80% by default for special parts), meaning that the ones that can be repaired the usual way, are not candidates for scrap-repairement. If some item is selected in salvage container window, the item will be taken as the first candidate for scrap-repair, and as a reference for the result (it will inherit subproperties, like upgrades or wheel size parameters).
4. Smart transfer modes to support the gameplay:
 - Transfer all items from a barn or junkyard to shopping cart with a condition filter (inherited from TransferAll mod). Executed by 'L' by default, choose the mode via Keypad Multiply.
 - Transfer all items from a barn or junkyard to shopping cart including repairable rare parts only. Executed by 'L' by default, choose the mode via Keypad Multiply.
 - Transfer all items from a barn or junkyard to shopping cart including parts from shopping list (of any condition). Executed by 'Shift-L' by default.
 - Find items in warehouse that are in shopping list and move them to inventory. Executed by 'F6' by default. Finds new or repairable items, moves them to inventory and removes from shopping list. When pressed with 'Shift' finds non-repairanle items and moves to inventory, does not modify shopping list. The latter mode is for finding items for scrap-repair.

## Requirements

Works with Melon Loader 0.5.7 (so it can be run together with QoL mod).

## Disclaimer

Some features have 'hacky' or awkward implementation (no proper UI, menus, etc) due to game limitations (or my unawareness how to do it better).