# Refreshing the scrap window after scrap-repair

Investigated against the installed CMS 2021 assemblies on 2026-10-23.
This is a static investigation; the proposed change has not been run in-game.

## Cause

`RareParts/RepairScrap.cs:104` calls:

```csharp
scrapWindow.choosePartDownWindow?.Refresh();
```

This resolves to inherited `PagedWindowBase.Refresh(bool resetPage = true)`.
Its native implementation dispatches to `RedrawCurrentPage()` without fetching
inventory or replacing `ChoosePartPageManager.items`. Consequently, deleted
inventory items remain in the chooser's cached list. Passing `true` explicitly
does not fix this.

There is a separate `ChoosePartPageManager.Refresh(List<ChoosePartDownItem>)`
overload. It replaces the cached list, applies sorting when enabled,
recalculates page count, redraws the page, and updates grid navigation.
The scrap controllers already supply the appropriate fresh list to it.

## Recommended change

Replace the existing refresh call inside the confirmed repair callback, after
the condition change and all inventory deletions, with:

```csharp
if (scrapWindow.scrapProduction?.IsActive == true)
{
    scrapWindow.UpdateItemsForProduction();
}
else if (scrapWindow.scrapUpgrade?.IsActive == true)
{
    scrapWindow.scrapUpgrade.UpdateItems();
}
```

These APIs are public in the installed managed interop assembly. No reflection
or Harmony patch is needed. Update only the active controller: both controllers
use the same chooser, but supply different item lists.

`ScrapWindow.UpdateItemsForProduction()` delegates to
`scrapProduction.UpdateItems(true)`. That method:

1. Calls `GetItemsForScrap()`, which obtains inventory items through
   `Inventory.GetItemsForScrapProduction(...)`.
2. Resets the current page and dot navigation to zero when requested and the
   previous page count is positive.
3. Passes the new list to `choosePartDownWindow.Refresh(newItems)`.
4. Handles the no-items state if the new list is empty.

The game's own production result animation also calls
`ScrapProduction.UpdateItems(bool)` after consuming a part.

`ScrapUpgrade.UpdateItems()` similarly calls `GetItemsForUpgrade()` and feeds
that result into the list-taking refresh overload, with separate empty-state
handling. Unlike the production helper above, it does not explicitly reset
the page before refreshing.

For a production-only feature, `scrapWindow.UpdateItemsForProduction()` alone
is the minimal replacement. The current repair entry point checks only whether
the Scrap window is active, so handling both tabs is preferable.

## Runtime checks

- Combine multiple identical parts: consumed UIDs should disappear immediately
  and the surviving part should show its new condition.
- Start on the last page and consume enough parts to reduce the page count.
  Check both Production and Upgrade tabs.
- Check the selected part's details and action values after refreshing, including
  when the repaired group differs from the selected item. Production's
  `OnDownWindowItemChange` updates `currentItem`, inventory details, and scrap
  values; verify that grid refresh produces the expected selection event.
- Check sorting, repeated repairs, and a cancelled confirmation.
- The production helper resets pagination; preserving the surviving item's
  selection by UID would be an additional behavior, not guaranteed by this call.
- Before enabling repair during a production minigame or its result animation,
  check the controller's `isGameInProgress` / `isAnimationInProgress` flags.
  Refreshing alone does not make concurrent inventory changes safe for that flow.

## Evidence

The installed managed DLLs are IL2CPP interop wrappers. Method signatures were
read from `MelonLoader/Managed/Assembly-CSharp-firstpass.dll`; native method
bodies were inspected in `GameAssembly.dll` using the existing Cpp2IL address
metadata and MelonLoader's Iced decoder.

| Method | Native RVA |
| --- | --- |
| `PagedWindowBase.Refresh(bool)` | `0x8CD420` |
| `ChoosePartPageManager.Refresh(List<ChoosePartDownItem>)` | `0xF96FC0` |
| `ChoosePartPageManager.RedrawCurrentPage()` | `0xF96B70` |
| `ScrapWindow.UpdateItemsForProduction()` | `0xBCCF00` |
| `ScrapProduction.UpdateItems(bool)` | `0xBC61E0` |
| `ScrapProduction.GetItemsForScrap()` | `0xBC62F0` |
| `ScrapProduction.OnDownWindowItemChange(...)` | `0xBC72B0` |
| `ScrapProduction.<AnimateInfo>b__67_1()` | `0xBC8D60` |
| `ScrapUpgrade.UpdateItems()` | `0xBC9430` |

Addresses identify this installed binary only and should not be hard-coded.
