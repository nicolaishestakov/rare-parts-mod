# rare-parts-mod
A mod for Car Mechanic Simulator 2021 which removes certain parts from shops and adds smart transfer commands to manage inventory, warehouse and shopping list.
The mod is inspired by https://github.com/microup/realshop and https://github.com/mannly01/TransferAll mods. Inherits some features and default configurations from the mentioned mods, but heavily modified, so they are not recommented to run together with this one.

## Features

1. 'Rare parts' are removed from shops. Works similar to RealShop mod, you may define the list of rare parts and cars in the config file. The default list of rare parts, and their price modifiers are taken from RealShop mod with slight modifications.
2. To compensate for the rarity, rare parts are repairable starting from 85% condition (the level is configurable).
3. In addition, all non-repairable body shop items (windows, lights), interior items, and tires are repairable from 85% condition.
4. To compensate for the rare parts that are difficult to find in barns or junkyards, parts may be repaired to some degree from multiple items. For example, if you have two 10% identical items, they can be converted into one item with condition 19%.
5. Smart transfer modes:
 - Transfer all items from a barn or junkyard to shopping cart with a condition filter (inherited from TransferAll).
 - Transfer all items from a barn or junkyard to shopping cart including repairable rare parts only.
 - Transfer all items from a barn or junkyard to shopping cart including parts from shopping list (of any condition)
 - Find items in warehouse that are in shopping list and move them to inventory (with optionally removing the found items from the shopping list)

 ## Disclaimer

 Some features have 'hacky' or awkward implementation (no proper UI, menus, etc) due to game limitations (or my unawareness how to do it better).