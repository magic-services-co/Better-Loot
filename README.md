# BetterLoot

BetterLoot lets you configure Rust loot, gathering, and resource spawning from one plugin. Use it to change a few crate drops, build reusable loot groups, adjust harvest amounts, or run a custom population of ores and collectables.
###### _This guide describes the configuration used by BetterLoot upto v4.5.4._

## The Editor
The [Looty editor](https://looty.cc/betterloot) provides a web interface for the settings. You _can_ also edit the JSON files directly but using the editor is HIGHLY recommended. 

## Contents

- [Setup and editing](#setup-and-editing)
- [Commands and permissions](#commands-and-permissions)
- [Configuration files](#configuration-files)
- [Plugin configuration](#plugin-configuration)
- [Loot tables](#loot-tables)
- [Loot groups](#loot-groups)
- [Item loot configuration](#item-loot-configuration)
  - [Item duplicates / variants](#duplicate-items--variants)
  - [Blueprint items](#blueprint-explicit-items)
  - [Mission rewards](#mission-rewards)
  - [Unwrapping items](#unwrapping-items)
- [Gather rates](#gather-rates)
  - [Collectable pickups](#collectable-pickups)
  - [NPC and animal harvesting](#npc-and-animal-harvesting)
  - [Quarries and Giant Excavator](#quarries-and-giant-excavator)
  - [Livestock production](#livestock-production)
  - [Ore nodes and custom spawning](#ore-nodes-and-custom-spawning)
- [Utilities](#utilities)
  - [Blacklist](#blacklist)
  - [Backups and upgrading](#backups-and-upgrading)


- [Troubleshooting](#troubleshooting)
- [Plugin integration](#plugin-integration)
- [Support and credits](#support-and-credits)

## Setup and Editing

1. Install `BetterLoot.cs` in your server's plugins directory. On uMod, this is `oxide/plugins/`.
2. Let the plugin load and generate its configuration and data files.
3. Grant yourself `betterloot.admin` using the server permission command:

```text
oxide.grant user YOUR_STEAM_ID betterloot.admin
OR
oxide.grant group GROUP_NAME betterloot.admin
```
> _For more info on permissions see [UMod Permissions Docs](https://umod.org/documentation/plugins/permissions)_.

To configure through Looty, open the [BetterLoot editor](https://looty.cc/betterloot), make your changes, and use **Download via Command** to obtain a configuration ID. Run these commands in chat:

```text
/bl-backup
/looty YOUR_LOOTY_ID
```

The RCON equivalents are `bl-backup` and `looty YOUR_LOOTY_ID` (without the slash). The import replaces the supplied loot tables and applies supplied group and gather settings. If groups or gather settings are omitted from the payload, their current settings are retained. Enabled imported tables also enable their watched entries.

For manual editing, make a copy of the files, unload BetterLoot, edit the JSON, and load the plugin again. Direct disk edits are read when the plugin loads; there is no file watcher. Loading, importing, or restoring settings also repopulates supported world containers, so changes can affect crates that already exist.

> **Using the examples:** Unless described as a full file, each JSON block is an excerpt to merge at the stated location. Keep the other entries in your generated files. Property names must match exactly, including older spellings such as `Scrap Multipler`. Use item shortnames, integer item amounts, and valid JSON without comments or trailing commas.

## Commands and Permissions

| Chat command | What it does | Permission |
| --- | --- | --- |
| `/looty <id>` | Download and apply an editor configuration. | `betterloot.admin` |
| `/bl-backup` | Back up the four editable data files. | `betterloot.admin` |
| `/bl-restore` | Restore the saved backup set and apply it. | `betterloot.admin` |
| `/blacklist` | List blacklisted item shortnames. | `betterloot.admin` |
| `/blacklist additem "rifle.ak"` | Add an item to the blacklist. | `betterloot.admin` |
| `/blacklist deleteitem "rifle.ak"` | Remove an item from the blacklist. | `betterloot.admin` |
| `/blshow ore` | Show ore nodes on the map. | `betterloot.oreshow` or `betterloot.admin` |
| `/blshow collectables` | Show ground pickups on the map. | `betterloot.oreshow` or `betterloot.admin` |
| `/blshow harvest` | Show wood piles, trees, and cactus resources. | `betterloot.oreshow` or `betterloot.admin` |
| `/blshow all` | Show all supported resource types. Also the default with no argument. | `betterloot.oreshow` or `betterloot.admin` |
| `/blhide` | Clear the resource markers. | `betterloot.oreshow` or `betterloot.admin` |
| `/blhelp` | Display resource command help. | `betterloot.oreshow` or `betterloot.admin` |

Only `looty`, `bl-backup`, and `bl-restore` have RCON versions. Their console handlers require RCON; use chat if the command is rejected from a player's console.

Aliases: `/betterlootshow` and `/oreshow` accept the same filters as `/blshow`. `/orehide` and `/betterloothide` clear markers. `/orehelp` and `/betterloothelp` show help.

Map pings last 30 seconds and show the nearest 48 matching resources. The plugin prefers tracked resources and can fall back to matching world resources when none are tracked for the requested category.

The hammer preview requires Rust administrator status (`IsAdmin`). Enable it in the main config, hit a loot container with a hammer, and its inventory will refresh until you close the panel. It does not preview corpse loot.

## Configuration Files

Paths below use the uMod directory layout. On other supported frameworks, use the corresponding config and data directories.

| File | Purpose |
| --- | --- |
| `oxide/config/BetterLoot.json` | Global plugin settings, watched sources, and conflict handling. |
| `oxide/data/BetterLoot/LootTables.json` | Loot for each container, NPC, unwrap item, or mission. Root key: `LootTables`. |
| `oxide/data/BetterLoot/LootGroups.json` | Reusable loot groups. Root key: `Loot Groups`. |
| `oxide/data/BetterLoot/GatherRates.json` | Gathering, pickups, machines, livestock, and resource spawning. Root key: `Gather Rates`. |
| `oxide/data/BetterLoot/Blacklist.json` | Blocked item shortnames. Root key: `ItemList`. |
| `oxide/lang/en/BetterLoot.json` | Registered English messages, including blacklist and permission messages. Some resource messages are written directly by the plugin. |

## Plugin Configuration

`oxide/config/BetterLoot.json` contains the following settings. This example uses the current defaults, with the generated watched-source list shortened to one entry.

```json
{
  "Chat Configuration": {
    "Chat Message Prefix": "[<color=#00ff00>BetterLoot</color>]",
    "Chat Message Icon SteamID (0 = None)": 0
  },
  "General Configuration": {
    "Blueprint Weight (0.0 = min bias, 1.0 = max bias, 0.5 = balanced)": 0.11,
    "Log Updates On Load": true,
    "Remove Stacked Containers": true,
    "Only update prefab list on wipe day": false,
    "Auto enable new prefabs found on wipe": true,
    "Watched Container Prefabs (true = monitor container loot, false = disabled)": {
      "assets/bundled/prefabs/radtown/crate_normal.prefab": true
    },
    "Stand down gather rates if other gather plugins are loaded": true,
    "Stand down livestock production if other gather plugins are loaded": false,
    "Other gather plugin names": [
      "GatherManager", "AdvancedGather", "DayNightGather",
      "ExtraGatherBonuses", "InstantGather", "Loottable"
    ]
  },
  "Loot Configuration": {
    "Enable Hammer Hit Loot Cycle": false,
    "Hammer Loot Cycle Time": 3.0,
    "Loot Multiplier": 1,
    "Scrap Multipler": 1,
    "Allow duplicate items": true,
    "Enable logging for item attachments auto balancing operations": false,
    "Always allow duplicate items from bonus items list (if set, will override 'Allow duplicate items option')": true,
    "Enable Blueprint Conversion": true,
    "Allow Duplicate Blueprints": false,
    "Enable Loot Pool Locking System": true,
    "New loot tables use Vanilla Rust Loot RNG": true
  },
  "Loot Groups Configuration": {
    "Enable creation of example loot group on load?": true,
    "Enable auto profile probability balancing?": true,
    "Always allow duplicate items from loot groups (if true overrides 'Allow duplicate items option')": true,
    "Allowed probablity difference to select neighbour during duplicate item resolution.": 10.0
  }
}
```

### Chat and General Settings

| Option | Explanation |
| --- | --- |
| `Chat Message Prefix` | Prefix for the plugin's configured chat replies. Rust color tags are supported. |
| `Chat Message Icon SteamID (0 = None)` | SteamID used for the reply icon, or `0` for none. |
| `Blueprint Weight ...` | Controls blueprint selection in custom RNG and biases the conversion target within the table's blueprint range. Lower values favor fewer blueprints; `0.5` gives a balanced conversion roll. The result also depends on eligible items and table limits. |
| `Log Updates On Load` | Log internal loot updates and populated-container counts. |
| `Remove Stacked Containers` | Remove overlapping loot containers at nearly the same position. This can delete duplicate world containers. |
| `Only update prefab list on wipe day` | Skip routine prefab discovery until a new save. Leave off to discover game additions on ordinary loads. |
| `Auto enable new prefabs found on wipe` | Enable newly discovered sources on wipe. Previously disabled entries stay disabled. |
| `Watched Container Prefabs ...` | Map of source keys to `true` or `false`. Used for container, NPC, and unwrap monitoring. Disable the table itself as well when you want to stop managing that source. |
| `Stand down gather rates if other gather plugins are loaded` | Yield gather, pickup, quarry, and excavator overrides to a loaded plugin from the names list. Custom spawning and independent bonus drops have their own behavior. |
| `Stand down livestock production if other gather plugins are loaded` | Apply the same conflict policy to livestock production. Defaults to `false`. |
| `Other gather plugin names` | Plugin names checked by both stand-down options. |

### Loot and Group Settings

| Option | Explanation |
| --- | --- |
| `Enable Hammer Hit Loot Cycle` / `Hammer Loot Cycle Time` | Enable the administrator preview and set its refresh interval in seconds. |
| `Loot Multiplier` | Multiply custom ungrouped amounts and bonus-item amounts. In vanilla RNG, multiply regular non-scrap stacks up to their stack limit. Group primary items and guaranteed items use their own configured amounts. |
| `Scrap Multipler` | Multiply the separate scrap amount from a table's `Item Settings`. It is not a multiplier for every item named `scrap` in another list. |
| `Allow duplicate items` | Control repeated item types during custom random generation. Per-entry `Allow Duplicates` also applies. Guaranteed entries are added separately. |
| `Always allow duplicate items from bonus items list ...` | Control the bonus-item duplicate check. Other applicable duplicate checks can still reject an entry. |
| `Enable Blueprint Conversion` | Allow eligible generated items to be replaced with blueprints in custom RNG. Explicit blueprint entries are separate from conversion. |
| `Allow Duplicate Blueprints` | Allow repeated blueprint targets during the conversion pass. |
| `Enable Loot Pool Locking System` | Global switch for pool locking. A table must also have `Enable Loot Pool Locking` enabled. |
| `New loot tables use Vanilla Rust Loot RNG` | Default mode for newly generated tables. Does not change the mode of existing tables. |
| `Enable logging for item attachments auto balancing operations` | Log attachment probability adjustments. |
| `Enable creation of example loot group on load?` | Allow the disabled example group reference to be added to the first suitable table. |
| `Enable auto profile probability balancing?` | Rescale a group's positive item probabilities to total 100 when it loads. |
| `Always allow duplicate items from loot groups ...` | Control the group-specific duplicate check. Per-entry restrictions and other applicable checks still matter. |
| `Allowed probablity difference ...` | Tolerance used when trying a neighboring entry after a duplicate selection. Usually leave at `10`. |

## Loot Tables

**File:** `oxide/data/BetterLoot/LootTables.json`

Each key inside `LootTables` identifies a loot source. Containers and NPCs use full prefab paths. Other supported sources use `unwrap/<item-shortname>` or `mission/<mission-name>` keys.

BetterLoot discovers supported container and NPC sources from the game, including crates, barrels, supply drops, supported boat loot, natural beehives, and NPC corpses. Use the keys generated by your server. The watched-source setting and `Is Prefab Enabled?` serve different purposes: keep both enabled for a managed container or unwrap item, and disable the table to stop BetterLoot filling it.

### Custom loot example

This excerpt creates a table that rolls two to four random entries, gives cloth, and adds 5 to 10 scrap. A roll has a 35% chance to select `basic_supplies` from the group example below. Other rolls use the ungrouped list.

```json
{
  "LootTables": {
    "assets/bundled/prefabs/radtown/crate_normal.prefab": {
      "Is Prefab Enabled?": true,
      "Use Vanilla Rust Loot RNG": false,
      "Enable Loot Pool Locking": false,
      "Select ungrouped items ignoring rarity bias": true,
      "Vanilla Loot Slots": [],
      "Loot Profiles": [
        {
          "Group Enabled?": true,
          "Loot Profile Name": "basic_supplies",
          "Loot Profile Probability (1% - 100%)": 35.0,
          "Max Items From Profile (0 = unlimited)": 2
        }
      ],
      "Guaranteed Items": {
        "cloth": { "Item Minimum": 5, "Item Maximum": 10 }
      },
      "Ungrouped Items": {
        "metal.fragments": { "Item Minimum": 25, "Item Maximum": 75, "Allow Duplicates": true },
        "stones": { "Item Minimum": 50, "Item Maximum": 100, "Allow Duplicates": true }
      },
      "Item Settings": {
        "Minimum Amount of Items": 2,
        "Maximum Amount of Items": 4,
        "Minimum Scrap Amount": 5,
        "Maximum Scrap Amount": 10,
        "Minimum Blueprints": 0,
        "Maximum Blueprints": 0,
        "Bonus Items Contribute to Item Count": false,
        "Guaranteed Items Contribute to Item Count": false
      }
    }
  }
}
```

Ungrouped items contain their amount settings directly. They do not use the `Item Probability` and `Item Amount` wrapper found in a loot group.

| Table option | Explanation |
| --- | --- |
| `Is Prefab Enabled?` | Enable this table. A disabled table leaves the source to its normal behavior. |
| `Use Vanilla Rust Loot RNG` | Use the saved/native loot-slot tree instead of the custom group and ungrouped selection path. |
| `Loot Profiles` | References to named groups. Probabilities share a 100-point roll; keep enabled imports at a combined total of 100 or less. Unused probability selects ungrouped items. |
| `Enable Loot Pool Locking` | Choose a group or the ungrouped pool once for the fill, instead of choosing a group on each roll. Requires the global switch. After a locked group reaches its item cap, remaining rolls use ungrouped items. |
| `Select ungrouped items ignoring rarity bias` | `true` selects uniformly from eligible ungrouped entries. `false` uses Rust's item rarity bias. Use groups for explicit per-item probabilities. |
| `Guaranteed Items` | Entries added each fill. Their quantities are separate from the random selection. |
| `Ungrouped Items` | Pool used when no group supplies the random entry. |
| `Minimum/Maximum Amount of Items` | Custom random-entry target, clamped to 1 through 36. This counts entries, not units within a stack. Selection failures, duplicate limits, and inventory space can reduce the result. |
| `Minimum/Maximum Scrap Amount` | Separate scrap range, multiplied by `Scrap Multipler`. Use `0` for both to add no separate scrap. |
| `Minimum/Maximum Blueprints` | Target range for custom blueprint conversion. Enough eligible items must exist to reach it. |
| `Bonus Items Contribute to Item Count` | Make bonus entries consume part of the custom item target. |
| `Guaranteed Items Contribute to Item Count` | Count guaranteed entries toward the custom item target. Otherwise they are additional. |

### Vanilla Loot RNG

New tables default to this mode. `Vanilla Loot Slots` preserves Rust's nested loot structure and can be edited without flattening it into a custom item pool. Its slot probabilities and branch weights determine the roll count and contents; custom group imports and blueprint conversion are not used on a successful vanilla fill.

The following excerpt belongs inside one table. It rolls twice; each roll has a 50% chance to produce 10 to 20 metal fragments.

```json
{
  "Use Vanilla Rust Loot RNG": true,
  "Vanilla Loot Slots": [
    {
      "Name": "Example materials",
      "Number To Spawn": 2,
      "Probability": 0.5,
      "Loot": {
        "Weight": 100,
        "Items": [
          { "Shortname": "metal.fragments", "Item Minimum": 10, "Item Maximum": 20 }
        ]
      }
    }
  ]
}
```

`Probability` is between `0.0` and `1.0`. A node can contain `SubSpawn`, an array of child nodes selected by their relative `Weight`, or an `Items` list which supplies all listed items when that leaf is selected. `Extra Spawns` repeats a node's selection. `Name` is a label.

Vanilla fills also apply regular-item multipliers, blacklist filtering, additional guaranteed entries, and the separate scrap range. Generated guaranteed entries marked `Included in Vanilla Loot` are skipped as additions because they are already part of the native loot tree. If no saved slots exist, the plugin tries the source's native definition, then falls back to custom generation if it cannot obtain one.

## Loot Groups

**File:** `oxide/data/BetterLoot/LootGroups.json`

Groups let several tables share the same selection of items. The table chooses a group, then the group chooses one entry from `Item List`.

```json
{
  "Loot Groups": {
    "basic_supplies": {
      "Enabled?": true,
      "Guaranteed Items": {},
      "Item List": {
        "metal.fragments": {
          "Item Probability (1-100)": 70.0,
          "Item Amount": { "Item Minimum": 20, "Item Maximum": 60 }
        },
        "stones": {
          "Item Probability (1-100)": 30.0,
          "Item Amount": { "Item Minimum": 50, "Item Maximum": 100 }
        }
      }
    }
  }
}
```

In this group, a successful selection gives metal fragments 70% of the time and stones 30% of the time. These are item-selection chances within the group, separate from the table's chance to choose the group.

| Group setting | Explanation |
| --- | --- |
| Group name, such as `basic_supplies` | Exact name referenced by a table's `Loot Profile Name`. |
| `Enabled?` | Enable the group's use by table imports. |
| `Guaranteed Items` | Added when the group successfully supplies a random item. These can be added again if the group is selected repeatedly in one fill. |
| `Item List` | Item keys with an `Item Probability (1-100)` and an `Item Amount` object. Fractional probabilities are supported. |
| `Item Amount` | The item's quantity, skin, name, durability, ammunition, attachments, duplicate rule, and bonus items. See the next section. |
| Table import: `Group Enabled?` | Enable that reference without changing the group for other tables. |
| Table import: `Max Items From Profile (0 = unlimited)` | Limit successful primary items from that group per fill. Bonus and guaranteed additions are separate. |

Automatic balancing rescales positive group probabilities to total 100. With balancing off, an unused part of the 100-point roll can produce no group item, which may trigger fallback or retry behavior. Keep totals at 100 for a straightforward distribution. Duplicate restrictions and item caps can change observed drop frequencies.

## Item Loot Configuration

Item settings appear directly under an item key in `Ungrouped Items`, under `Item Amount` in a group, or as a simpler entry in `Guaranteed Items`.

This example belongs inside `Ungrouped Items`. It produces one named rifle at 60% to 100% condition, loads 10 to 20 rifle rounds, attaches one selected weapon mod, and adds a separate stack of rifle ammunition.

```json
{
  "rifle.ak": {
    "Skin ID (0 = default)": 0,
    "Display Name (empty = none)": "Supply Rifle",
    "Item Minimum": 1,
    "Item Maximum": 1,
    "Allow Duplicates": false,
    "Can Convert To Blueprint": false,
    "Item Durability": {
      "Minimum Durability": 60,
      "Maximum Durability": 100
    },
    "Item Properties": {
      "Ammunition Settings": {
        "Ammo Item Shortname": "ammo.rifle",
        "Minimum Amount": 10,
        "Maximum Amount": 20
      },
      "Weapon Attachments": {
        "Minimum Mod Amount": 1,
        "Maximum Mod Amount": 1,
        "Available Attachments": {
          "weapon.mod.holosight": { "Spawn Probability (0%-100%)": 50.0 },
          "weapon.mod.lasersight": { "Spawn Probability (0%-100%)": 50.0 }
        }
      }
    },
    "Bonus Items": {
      "ammo.rifle": { "Item Minimum": 20, "Item Maximum": 40 }
    }
  }
}
```

| Item setting | Explanation |
| --- | --- |
| `Item Minimum` / `Item Maximum` | Inclusive quantity range. Use equal values for a fixed quantity. |
| `Skin ID (0 = default)` | Skin ID to apply. `0` uses the normal item skin. |
| `Display Name (empty = none)` | Custom item name. An empty string keeps the normal name. |
| `Allow Duplicates` | Allow the same random entry to be selected again. Global duplicate rules also apply. |
| `Can Convert To Blueprint` | Permit blueprint conversion for a researchable item. Set `false` to keep a configured weapon as a weapon. |
| `Item Durability` | Minimum and maximum condition percentages, normally 0 through 100. Only use on items that have condition. |
| `Ammunition Settings` | Loaded ammunition or fuel, using a compatible shortname. Magazine weapons use minimum/maximum amounts; single-unit weapons use `Spawn Probability` instead. Capacity is taken from the game. |
| `Weapon Attachments` | Minimum/maximum selection attempts and the available compatible attachments. Each attachment can also have a `Durability` object with minimum/maximum durability. |
| `Bonus Items` | Additional items created with a selected random item. These are inventory items, separate from installed attachments. They use basic item settings and cannot contain another nested bonus list. |
| `Rarity` | Generated game-rarity metadata. Custom rarity-biased selection uses Rust's item definition; use groups to control selection percentages. |
| `Included in Vanilla Loot` | Marks an extracted guaranteed entry already supplied by vanilla slots. Leave generated markers intact when using vanilla RNG. |

Attachment probabilities share a 100-point roll. Totals above 100 are scaled down; a total below 100 leaves a chance for no attachment on an attempt. Incompatible attachments are removed during validation. For no attachments, use an empty `Available Attachments` object.

### Duplicate Items / Variants
To configure two variants of one shortname, give the keys numeric suffixes such as `rifle.ak{1}` and `rifle.ak{2}`. The suffix identifies the entry and is stripped when creating the item. Use global duplicate restrictions if the container should contain only one actual item of that type.

_**Example**_ _(Loot Tables / Loot Groups)_
```json
{
  "rifle.ak": { },
  "rifle.ak{0}": { }
}
```

### Blueprint Explicit Items
For an explicit blueprint in a table, use a key such as `rifle.ak.blueprint` in `Guaranteed Items` or `Ungrouped Items`. Random blueprint entries also depend on the table's blueprint limit and selection settings. Blueprint conversion requires both the global conversion switch and eligible item entries; it does not create extra items beyond the generated selection.

Vanilla slot items use `Shortname` instead of an enclosing item key and support their own skin, name, durability, item properties, and duplicate setting. Put those edits in `Vanilla Loot Slots` when that is the active RNG mode.

## Mission Rewards

Mission rewards are configured in `LootTables.json` using `mission/<mission-name>` keys. BetterLoot changes the completion reward; it does not create mission objectives, NPC dialogue, or new missions.

The key must match the mission's name, shortname, or title. The resolver accepts normalized names and tolerates differences in spaces, hyphens, and underscores. Use the mission key supplied by your editor or server configuration when available.

This excerpt gives a fixed reward. Replace `mission/your-mission-name` with the real mission key.

```json
{
  "LootTables": {
    "mission/your-mission-name": {
      "Is Prefab Enabled?": true,
      "Use Vanilla Rust Loot RNG": false,
      "Loot Profiles": [],
      "Guaranteed Items": {
        "scrap": { "Item Minimum": 100, "Item Maximum": 100 },
        "metal.fragments": { "Item Minimum": 250, "Item Maximum": 250 }
      },
      "Ungrouped Items": {},
      "Item Settings": {
        "Minimum Amount of Items": 1,
        "Maximum Amount of Items": 1,
        "Minimum Scrap Amount": 0,
        "Maximum Scrap Amount": 0,
        "Minimum Blueprints": 0,
        "Maximum Blueprints": 0,
        "Guaranteed Items Contribute to Item Count": true
      }
    }
  }
}
```

Mission tables can use the same groups, ungrouped items, guaranteed entries, and item customization as other tables. For a fixed reward, place the reward in `Guaranteed Items`. A separate scrap range alone does not qualify as a configured mission reward; include at least one actual item source.

When a matching enabled table delivers items, BetterLoot's reward patch replaces the normal reward. An unmatched, disabled, or unconfigured mission keeps its normal reward path. Rewards go to the main inventory, then the belt, with overflow dropped at the player. Existing inventory contents are retained. Mission matching uses the table's enable switch directly; the watched-container map is not its activation switch.

## Unwrapping items

Presents, crack-open eggs, loot bags, and other supported unwrap items appear as `unwrap/<item-shortname>` entries. They use the same table format as containers. Both the watched key and the table must be enabled.

For example, edit the generated `unwrap/gift.small` table to change small-present rewards if that item exists on your server. On a successful custom unwrap, BetterLoot consumes one source item and gives the configured rewards to the player's main inventory, belt, or ground overflow. If no custom reward is delivered, normal unwrap behavior is allowed to continue.

## Gather rates
___

**File:** `oxide/data/BetterLoot/GatherRates.json`

Everything in this file sits inside its `Gather Rates` object. It covers resource amounts and the optional spawning system. You can change amounts on existing world resources with custom spawning disabled.

The file is populated from game prefabs on first load. Generated totals are starting values; existing nonempty output lists are retained. Node sections currently initialize with gather overrides enabled and generated totals, while pickup, NPC harvest, machine, and livestock overrides start disabled. Check the actual switches in your saved file after an upgrade or import.

### Resource controls

| Setting | What it controls |
| --- | --- |
| `Enabled` | Custom spawning for that resource type. It does not disable its gather or pickup amount settings. |
| `Override Gather Amounts` | Use the nonempty `Gather Outputs` list for resource yields. |
| `Gather Outputs` | Items and total amounts for a complete harvest with each tool. |
| `Gather Multiplier` | Scale the native amount when gather override is off, or the configured totals when it is on. Use `1.0` for no scaling. Positive values are clamped to 0.01 through 100; zero is treated as 1, not as suppression. |
| `Bonus Loot` | Independent chance-based drops. See the spawning section for when they roll. |

Node sections are `Stone Settings`, `Metal Settings`, `Sulfur Settings`, `HQM Settings`, `Wood Pile Settings`, `Cactus Settings`, and `Tree Settings`.

For a simple rate change, set `Override Gather Amounts` to `false` and `Gather Multiplier` to `2.0`. To set exact base totals per tool, enable the override and edit `Gather Outputs`. This excerpt sets a full stone node to 2,000 stones with a pickaxe or jackhammer and 750 with a rock:

```json
{
  "Gather Rates": {
    "Stone Settings": {
      "Override Gather Amounts": true,
      "Gather Multiplier": 1.0,
      "Gather Outputs": [
        {
          "Item Shortname": "stones",
          "Amounts By Tool": {
            "jackhammer": 2000,
            "icepick.salvaged": 2000,
            "pickaxe": 2000,
            "stone.pickaxe": 1500,
            "hammer.salvaged": 1000,
            "bone.club": 900,
            "rock": 750
          }
        }
      ]
    }
  }
}
```

Amounts are totals for a fully harvested node, including the final payout. The plugin distributes them across harvest progress. They are not amounts per swing. Rich-vein multipliers and applicable Rust gathering bonuses can increase the result. Tea bonuses retain Rust's resource eligibility, so an ore tea does not boost an unrelated replacement product such as an apple.

Add more output objects to give multiple products. With a nonempty override active, omitted native products are suppressed for ordinary nodes. Use explicit zero amounts to suppress an output; empty node lists can be repopulated from game defaults when loaded. To restore ordinary gathering, turn the override off and set the multiplier to `1.0`.

The held tool's shortname selects its total. If it is missing, the fallback order is `pickaxe`, `hatchet`, `knife.skinning`, `knife.bone`, then the first configured value. Include every tool you want to tune explicitly. Chopping tools include `chainsaw`, `axe.salvaged`, `hatchet`, and `stonehatchet`.

Harvest progress is held in memory. Reloading during a partially harvested resource resets that accounting; switching tools uses the new tool's total while retaining amounts already paid in the current session.

## Collectable Pickups

**Location:** `Gather Rates` > `Collectable Settings`

Supported sections include `Stone`, `Metal`, `Sulfur`, `Wood`, `Hemp`, six `Berry` colors, `Mushroom`, `Corn`, `Potato`, `Pumpkin`, `Orchid`, `Rose`, `Sunflower`, `Wheat`, `HQM`, `Coconut`, `Diesel`, and the `Halloween Bone`, `Halloween Metal`, `Halloween Stone`, `Halloween Sulfur`, and `Halloween Wood` variants.

This excerpt changes a hemp pickup to give 20 to 30 cloth and one hemp seed:

```json
{
  "Collectable Settings": {
    "Hemp": {
      "Override Pickup Amounts": true,
      "Pickup Outputs": [
        { "Item Shortname": "cloth", "Min Amount": 20, "Max Amount": 30 },
        { "Item Shortname": "seed.hemp", "Min Amount": 1, "Max Amount": 1 }
      ]
    }
  }
}
```

`Override Pickup Amounts` activates a nonempty `Pickup Outputs` list. Each output gets an inclusive quantity roll. Omitted native outputs are replaced by the list. Zero maximum amounts suppress that output; an empty list can be filled with defaults on load. The resource's `Enabled` setting only controls its custom spawning.

Legacy `Pickup Min Amount` and `Pickup Max Amount` can still adjust each native pickup product when the output-list override is inactive. A positive legacy maximum activates that behavior, even if `Override Pickup Amounts` is false. Clear those legacy bounds as well when returning to native amounts.

Pickup-and-eat uses the food action for configured food and gives non-food outputs normally. Rust's collectable double-yield effect is respected. Ore and wood gathering teas do not apply to ground pickups.

## NPC and Animal Harvesting

**Location:** `Gather Rates` > `NPC Harvest`

These settings control resources obtained by harvesting a corpse. Inventory loot on an NPC is configured separately in `LootTables.json`.

Entries use NPC or animal prefab paths and contain `Override Gather Amounts` plus `Gather Outputs`. The same per-tool total rules apply as for nodes. Use the generated keys for the animals, scientists, or player corpses on your server. A bear entry, for example, can be edited as follows:

```json
{
  "NPC Harvest": {
    "assets/rust.ai/agents/bear/bear.prefab": {
      "Override Gather Amounts": true,
      "Gather Outputs": [
        {
          "Item Shortname": "leather",
          "Amounts By Tool": { "knife.skinning": 100, "knife.bone": 50, "rock": 20 }
        },
        {
          "Item Shortname": "fat.animal",
          "Amounts By Tool": { "knife.skinning": 80, "knife.bone": 40, "rock": 15 }
        }
      ]
    }
  }
}
```

This nonempty list replaces ordinary bear harvest products with the listed outputs. For livestock corpses, unmatched native products are preserved, including remaining sheep fleece where applicable. Use `Livestock Production` for living-animal milk, wool regrowth, and dung.

## Quarries and Giant Excavator

**Location:** `Gather Rates` > `Quarry Harvest`

Machine `Amount` values are total outputs per diesel, paid over the normal production batches. They are not per-tick amounts. **Fuel use and machine operation remain governed by Rust.**

Quarry keys are `HQM Quarry`, `Stone Quarry`, and `Sulfur Quarry`. `Giant Excavator` has a `Mining Modes` object with `Sulfur`, `Metal Fragments`, `HQM Ore`, and `Stone` sections. Each mode has its own switch and complete output list.

```json
{
  "Quarry Harvest": {
    "Sulfur Quarry": {
      "Override Gather Amounts": true,
      "Outputs": [
        { "Item Shortname": "sulfur.ore", "Amount": 2000 },
        { "Item Shortname": "scrap", "Amount": 10 }
      ]
    },
    "Giant Excavator": {
      "Mining Modes": {
        "Metal Fragments": {
          "Override Gather Amounts": true,
          "Outputs": [
            { "Item Shortname": "metal.fragments", "Amount": 10000 }
          ]
        }
      }
    }
  }
}
```

The quarry example gives 2,000 sulfur ore and 10 scrap per diesel. The excavator example gives 10,000 metal fragments per diesel across its output piles while mining in that mode. Preserve the other generated modes when editing this excerpt into your file.

With an override enabled, only its listed products are produced. Custom shortnames are supported, and empty or all-zero lists deliberately suppress machine output. Duplicate shortnames use the first entry. Turn the matching override off to restore native production.

## Livestock Production

**Location:** `Gather Rates` > `Livestock Production`

Cow, bull, and sheep entries use these keys:

| Animal | Key | Production settings |
| --- | --- | --- |
| Cow | `assets/rust.ai/agents/cow/cow.prefab` | Milk and dung. |
| Bull | `assets/rust.ai/agents/bull/bull.prefab` | Dung. |
| Sheep | `assets/rust.ai/agents/sheep/sheep.prefab` | Wool and dung. |

Enable `Override Production` for the animal, then edit its `Bad`, `Ok`, and `Good` gene tiers. Yield genes choose milk/wool settings; dung genes choose dung settings. Animals with genes disabled use the `Ok` tier.

This cow example increases milk amounts while retaining the generated cooldown and dung values:

```json
{
  "Livestock Production": {
    "assets/rust.ai/agents/cow/cow.prefab": {
      "Override Production": true,
      "Bad": {
        "Milk Amount": 2,
        "Milk Cooldown Seconds": 500.0,
        "Dung Amount": 1,
        "Dung Interval Seconds": 2000.0
      },
      "Ok": {
        "Milk Amount": 3,
        "Milk Cooldown Seconds": 300.0,
        "Dung Amount": 1,
        "Dung Interval Seconds": 1200.0
      },
      "Good": {
        "Milk Amount": 4,
        "Milk Cooldown Seconds": 187.5,
        "Dung Amount": 1,
        "Dung Interval Seconds": 750.0
      }
    }
  }
}
```

`Milk Amount` is per successful milking. Sheep use `Wool Amount` for a full fleece, divided across four shearings, and `Wool Cooldown Seconds` for regrowth. Previously sheared wool is accounted for when harvesting the corpse. `Dung Amount` is per production event; `Dung Interval Seconds` still works with native condition and random timing effects.

Set a product amount to zero to suppress it. Rust's food, trust, age, tool, and interaction requirements still apply. Loading, importing, or restoring new cooldowns rescales running timers while preserving their progress. Ready animals remain ready, and unloading restores native timing.

## Ore Nodes and Custom Spawning

**Location:** `Gather Rates`, using the node sections and `Collectable Settings`

Custom spawning places supported ore, wood, tree, cactus, and collectable prefabs across valid terrain. It is optional and works independently of the gather and pickup overrides described above.

### Enabling resources

| Mode | Required settings |
| --- | --- |
| Global mode | Set `Custom Spawning Per Resource` to `false` and `Enable Custom Ore Spawning` to `true`. Only resource sections with `Enabled: true` are spawned. |
| Per-resource mode | Set `Custom Spawning Per Resource` to `true`. Each resource's `Enabled` switch controls it; the global enable switch is ignored. |
| No custom spawning | Set both global switches to `false`. Gather, pickup, machine, and livestock overrides can still operate. |

Many resource sections have `Enabled: true` in the generated file even though global spawning is off. Review both node and collectable entries before turning spawning on.

This excerpt configures the global spawn controls and a stone spawn profile. Other enabled types in your file will also spawn.

```json
{
  "Gather Rates": {
    "Enable Custom Ore Spawning": true,
    "Custom Spawning Per Resource": false,
    "Stone Settings": {
      "Enabled": true,
      "Grid Size": 150.0,
      "Prefab Path": "assets/bundled/prefabs/autospawn/resource/ores/stone-ore.prefab",
      "Biome Probabilities": {
        "Arid Biome": 0.3,
        "Temperate Biome": 0.4,
        "Arctic Biome": 0.2,
        "Tundra Biome": 0.35,
        "Jungle Biome": 0.3
      }
    },
    "Disable Native Ore Spawning": false,
    "Global Spawn Multiplier": 1.0,
    "Node Density (0.0 - 1.0)": 1.0,
    "Respawn Interval (minutes)": 1.0,
    "Spawn Delay (seconds)": 0.05,
    "Batch Size": 10,
    "Min Distance From Players": 50.0,
    "Min Distance From Buildings": 25.0,
    "Water Buffer Height": 1.5,
    "Max Slope Angle": 35.0,
    "Wipe Ores On Plugin Unload": false,
    "Debug Mode": false,
    "Population Scaling Settings": {
      "Enable Population Scaling": true,
      "Minimum Players Threshold": 50,
      "Maximum Players Threshold": 200,
      "Grid Scalar At Min Players (sparse)": 1.5,
      "Grid Scalar At Max Players (dense)": 0.8
    },
    "Rich Vein Settings": {
      "Enable Rich Veins": true,
      "Rich Vein Chance (0.0 - 1.0)": 0.05,
      "Rich Vein Gather Multiplier": 2.0,
      "Play Effect On Rich Vein Gather": true
    }
  }
}
```

### Placement and population options

| Option | Explanation |
| --- | --- |
| `Prefab Path` | Resource prefab to place. Use a valid resource prefab for the section. The generated path is a useful starting point. |
| `Grid Size` | Base spacing in world units. Smaller values create more candidate positions. |
| `Biome Probabilities` | Placement chance in each biome, from 0 to 1. These are independent values and do not need to total 1. Zero excludes that biome. |
| `Global Spawn Multiplier` | Adjust density by changing grid spacing. Higher values give more candidate positions, not larger harvest amounts. |
| `Node Density (0.0 - 1.0)` | Additional chance to keep each otherwise valid candidate. `1` keeps all valid candidates. |
| `Spawn Delay (seconds)` / `Batch Size` | Delay between creation batches and number of queued resources processed per batch. |
| `Min Distance From Players` / `Min Distance From Buildings` | Required clearance when choosing spawn positions. |
| `Water Buffer Height` | Clearance above water. |
| `Max Slope Angle` | Steepest accepted terrain slope, in degrees. |
| `Respawn Interval (minutes)` | Base delay before a tracked depleted resource is replaced, with timing variation. The base is at least one minute. |
| `Disable Native Ore Spawning` | Suppress and clean up native resources for successfully replaced types. Defaults to `true`; the example keeps it off so both populations can coexist. |
| `Wipe Ores On Plugin Unload` | Remove tracked custom resources on unload. Otherwise their positions are saved. |
| `Debug Mode` | Additional resource-system logging. |

Population scaling interpolates grid spacing between the minimum and maximum player thresholds. A scalar above `1` spreads candidates farther apart; a scalar below `1` makes them denser. It affects generated positions rather than continuously rebuilding the existing world layout. Terrain, biome chances, roads, monuments, water, buildings, and player clearance determine how many candidates actually spawn.

Native suppression starts separately for each type after successful custom spawning or restoration. Failed or empty types keep their native resources, and mixed native spawn populations stay active while they contain unreplaced types. Existing custom positions are restored where possible, with missing enabled types filled in. Changing spacing does not guarantee that an existing saved layout is regenerated from scratch.

### Rich veins and bonus drops

Rich veins assign an extra gather multiplier to a chance-selected portion of newly created custom nodes. `Rich Vein Chance (0.0 - 1.0)` of `0.05` means a 5% chance; `Rich Vein Gather Multiplier` of `2.0` doubles that node's gather scale. It combines with the resource's normal multiplier. The effect setting enables an effect while gathering rich nodes, and map markers identify them as rich.

`Bonus Loot` is a separate per-resource list. Place this excerpt inside a node or collectable section:

```json
{
  "Bonus Loot": [
    {
      "Item Shortname": "scrap",
      "Drop Chance (0.0 - 1.0)": 0.05,
      "Min Amount": 1,
      "Max Amount": 3
    }
  ]
}
```

For tracked nodes, bonuses roll on normal gather hits, not only when the node is depleted. For tracked collectables, they roll on pickup; the configured pickup-override path also applies them to matching world pickups. Bonus lists alone do not add a general bonus to every untracked native resource. Each list entry rolls independently. These drops are separate from gather-rate overrides and their conflict/veto handling.


## Utilities
___
## Blacklist

**File:** `oxide/data/BetterLoot/Blacklist.json`

```json
{
  "ItemList": ["rifle.ak", "ammo.rocket.basic"]
}
```

Use `/blacklist` to manage item shortnames excluded by the loot-generation filters. It does not remove items already owned by players or block the separate gather, pickup, and production settings. For a strict exclusion from a custom table, remove the item from guaranteed and bonus lists as well; those additions are handled separately from the random-item filter. Remove explicit `.blueprint` entries too if that item's blueprint should be unavailable.

## Backups and upgrading

Run `/bl-backup` before imports or large edits. It writes these four files beside their originals:

```text
LootTables.json.bak
LootGroups.json.bak
Blacklist.json.bak
GatherRates.json.bak
```

Each backup replaces the previous `.bak` copy. `/bl-restore` validates and restores the complete set, then applies the loot and gather settings. Keep separate copies of `oxide/config/BetterLoot.json` and any world-state backups you need; they are not part of this four-file command.

Looty imports validate supplied data and attempt to restore previous files and active settings if applying the import fails. They do not create a persistent `.bak` set in this version. A data-file load error can also back up the unreadable file and create defaults, so keep your own known-good copy before manual edits.

For older installations:

- Use the [Looty converter](https://looty.cc/converter) for pre-v4 loot tables.
- Supported `OreSpawn.json` data and embedded gather settings migrate to `GatherRates.json`. The old file is removed after successful migration.
- Old `Watched Prefabs` lists migrate to the watched-source map, and `Blueprint Probability` migrates to `Blueprint Weight`.
- Legacy flat livestock settings migrate to the `Bad`, `Ok`, and `Good` sections. Old excavator outputs migrate to mining modes.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| A crate still has ordinary loot | Confirm the source key, watched switch, and table enable switch. Check `Use Vanilla Rust Loot RNG`; changing `Ungrouped Items` does not change a successful vanilla-slot fill. |
| A group never appears | Match its name exactly, enable the group and import, use custom RNG, and check its import probability and item cap. |
| Too few items appear | Check available entries, duplicate restrictions, invalid shortnames, blueprint eligibility, and inventory capacity. Item counts are generation targets. |
| Changes disappear or defaults return | Check JSON syntax and nesting. Keep the correct root key and use direct item settings for ungrouped entries. Read load errors in the console. |
| Gather amounts do not change | Check the matching override, tool key, multiplier, and gather-plugin stand-down message. Resource `Enabled` only controls spawning. |
| Pickup amounts still change with the override off | Check for positive legacy `Pickup Max Amount` settings. |
| Quarry or excavator products are wrong | Check the exact machine/mode key and use amounts per diesel. An active override replaces the whole list. |
| A mission keeps its normal reward | Check the mission key, table enable switch, and presence of actual item entries. Check plugin startup errors if the reward patch could not initialize. |
| No custom resources appear | Check the spawning mode, resource enable switches, prefab path, grid size, biome chances, and placement restrictions. Use `Debug Mode` and `/blshow`. |
| Reloading does not change node spacing | Existing saved positions may be restored. Density settings apply when positions are generated. |

## Plugin Integration / API

BetterLoot skips loot containers identified by `CustomLootSpawns` as its own boxes. The configured gather-plugin names control the stand-down policies described above.

The following hooks allow another plugin to veto a BetterLoot operation. Any non-null result, including `false`, cancels that BetterLoot operation. Return `null` to allow it.

```csharp
// npcSteamId is the NPC corpse's playerSteamID.
private object ShouldBLPopulate_NPC(ulong npcSteamId)
{
    return null;
}

// Called with the loot container's network identifier.
private object ShouldBLPopulate_Container(ulong lootContainerId)
{
    return null;
}

// Skip BetterLoot's gather-rate override for this payout.
private object ShouldBLGather(ResourceDispenser dispenser, BasePlayer player, Item item)
{
    return null;
}

// Skip BetterLoot's pickup-amount override for this collectable.
private object ShouldBLCollectablePickup(CollectibleEntity collectible, BasePlayer player)
{
    return null;
}
```

A gather or pickup veto skips BetterLoot's amount override; it does not cancel Rust's own action or the independent bonus-drop logic. The container hook also serves the supported RHIB loot path, which passes its network ID object.

The `isSupplyDropActive` plugin call reports whether the supply-drop table is enabled:

```csharp
bool managesSupplyDrops = BetterLoot?.Call<bool>("isSupplyDropActive") ?? false;
```

## Support / Resources

- [Looty Editor](https://looty.cc/betterloot)
- [Legacy Loot Table Converter](https://looty.cc/converter)
- [Support Discord](https://discord.com/invite/ArTgweeM6q)
- [SRT Bull's setup tutorial](https://www.youtube.com/watch?v=I9L4EDGh5gk)

## Credit
- dcode - Original author.
- Fujicura - Maintainer.
- Misticos - Maintainer.
- Tryhard - Maintainer.
- TGWA - Author of Version 4 & Current Maintainer.