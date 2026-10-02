using System;
using System.IO;
using System.Text;
using Oxide.Core;
using System.Data;
using System.Linq;
using UnityEngine;
using Rust.Ai.Gen2;
using HarmonyLib;
using Newtonsoft.Json;
using Facepunch;
using Facepunch.Extend;
using Oxide.Core.Plugins;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using static ConsoleSystem;
using static RpcTarget;
using Pool = Facepunch.Pool;
using UnityEngine.Networking;
using Random = System.Random;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Oxide.Plugins.BetterLootExtensions;
using Rust;

namespace Oxide.Plugins
{
    [Info("BetterLoot", "MagicServices.co // TGWA", "4.5.2")]
    [Description("Loot container editor with rarity support, plus optional ore and collectable spawning | Previously maintained and updated by Khan & Tryhard")]
    public class BetterLoot : RustPlugin
    {
        #region Fields
        [PluginReference] Plugin? CustomLootSpawns;

        // Static Instance
        private static BetterLoot? _instance;
        private static PluginConfig? _config;

        // System States
        private bool Changed = true;
        private static bool NewConfigGenerated;
        private static bool NewSave;
        private static bool Initialized;

        private static Random? RNG;
        private static Regex? UniqueTagREGEX;
        private const string UNWRAP_PREFIX = "unwrap/";
        private const string MISSION_PREFIX = "mission/";
        private ulong _missionRewardStampPlayer;
        private float _missionRewardStampTime;

        // Data Instances
        private Dictionary<string, List<string>[]> Items = new Dictionary<string, List<string>[]>(); // Cached Item Data for each container
        private Dictionary<string, List<string>[]> Blueprints = new Dictionary<string, List<string>[]>(); // Cached Blueprint Data for each container
        private Dictionary<string, int[]> ItemWeights = new Dictionary<string, int[]>(); // Item weights for each container
        private Dictionary<string, int[]> BlueprintWeights = new Dictionary<string, int[]>(); // Blueprint weights for each container
        private Dictionary<string, int> TotalItemWeights = new Dictionary<string, int>(); // Total sum of item weights for each container
        private Dictionary<string, int> TotalBlueprintWeights = new Dictionary<string, int>(); // Total sum of blueprint weights for each container
        
        #region Info Caching
        private Dictionary<string, WI_Cache>? WeaponInfoCache; // Item info for building table 
        private Dictionary<string, ItemSlot>? WeaponModInfoCache; // Weapon mod shortname to enum mapping.
        private List<string>? DurabilityItems;  // Items that need the durability property 
        private static ItemDefinition? BlueprintBaseDef;
        private List<string> ItemManagerItemNamesList = new List<string>();

        #region Ore Spawn Fields
        private static bool _gatherRatesNeedsVanillaFill;
        private bool _loggedGatherStandDown;
        private OreSpawnConfiguration? _legacyConfigOreSpawn;
        private OreSpawnConfiguration Ore
        {
            get
            {
                oreSpawnData ??= new OreSpawnData();
                return oreSpawnData.OreSpawn ??= new OreSpawnConfiguration();
            }
        }
        private OreSpawnStoredData _oreStoredData;
        private readonly Dictionary<BaseEntity, OreNodeData> spawnedOres = new Dictionary<BaseEntity, OreNodeData>();
        private readonly Dictionary<BaseEntity, OreGatherSession> _oreGatherSessions = new Dictionary<BaseEntity, OreGatherSession>();
        private readonly Dictionary<ulong, string> _corpseNpcPrefabs = new Dictionary<ulong, string>();
        private const string PlayerHarvestPrefab = "assets/prefabs/player/player.prefab";
        private static readonly string[][] HarvestPrefabAliases =
        {
            new[]
            {
                PlayerHarvestPrefab,
                "assets/prefabs/player/player_corpse.prefab",
                "assets/prefabs/player/player_corpse_new.prefab"
            },
            new[]
            {
                "assets/rust.ai/agents/bear/bear.prefab",
                "assets/rust.ai/agents/bear/bear.corpse.prefab",
                "assets/rust.ai/agents/bear/bear_tutorial.prefab",
                "assets/rust.ai/agents/bear/bear_tutorial.corpse.prefab"
            },
            new[]
            {
                "assets/rust.ai/agents/bear/polarbear.prefab",
                "assets/rust.ai/agents/bear/polarbear.corpse.prefab"
            },
            new[]
            {
                "assets/rust.ai/agents/boar/boar.prefab",
                "assets/rust.ai/agents/boar/boar.corpse.prefab"
            },
            new[]
            {
                "assets/rust.ai/agents/chicken/chicken.prefab",
                "assets/rust.ai/agents/chicken/chicken.corpse.prefab",
                "assets/rust.ai/agents/chicken/chicken.corpse.tutorial.prefab",
                "assets/rust.ai/agents/chicken/chicken.tutorial.prefab"
            },
            new[]
            {
                "assets/rust.ai/agents/crocodile/crocodile.prefab",
                "assets/rust.ai/agents/crocodile/crocodile.corpse.prefab"
            },
            new[]
            {
                "assets/content/vehicles/horse/ridablehorse.prefab",
                "assets/content/vehicles/horse/horse.corpse.prefab"
            },
            new[]
            {
                "assets/rust.ai/agents/panther/panther.prefab",
                "assets/rust.ai/agents/panther/panther.corpse.prefab"
            },
            new[]
            {
                "assets/rust.ai/agents/fish/simpleshark.prefab",
                "assets/rust.ai/agents/fish/shark.corpse.prefab"
            },
            new[]
            {
                "assets/rust.ai/agents/snake/snake.entity.prefab",
                "assets/rust.ai/agents/snake/snake.corpse.prefab"
            },
            new[]
            {
                "assets/rust.ai/agents/stag/stag.prefab",
                "assets/rust.ai/agents/stag/stag.corpse.prefab"
            },
            new[]
            {
                "assets/rust.ai/agents/tiger/tiger.prefab",
                "assets/rust.ai/agents/tiger/tiger.corpse.prefab"
            },
            new[]
            {
                "assets/rust.ai/agents/wolf/wolf2.prefab",
                "assets/rust.ai/agents/wolf/wolf.prefab",
                "assets/rust.ai/agents/wolf/wolf.corpse.prefab"
            },
            new[]
            {
                "assets/prefabs/npc/gingerbread/gingerbread_dungeon.prefab",
                "assets/prefabs/npc/gingerbread/gingerbread_meleedungeon.prefab",
                "assets/prefabs/npc/gingerbread/gingerbread_corpse_female.prefab",
                "assets/prefabs/npc/gingerbread/gingerbread_corpse_male.prefab"
            },
            new[]
            {
                "assets/prefabs/npc/scarecrow/scarecrow.prefab",
                "assets/prefabs/npc/scarecrow/scarecrow_dungeon.prefab",
                "assets/prefabs/npc/scarecrow/scarecrow_dungeonnoroam.prefab"
            },
            new[]
            {
                "assets/content/structures/monumentblockers/monumentblocker_barrels_100x450.prefab",
                "monumentblocker_barrels_100x450.prefab",
                "monumentblocker-barrels-100x450"
            },
            new[]
            {
                "assets/content/structures/monumentblockers/monumentblocker_barrels_100x300.prefab",
                "monumentblocker_barrels_100x300.prefab",
                "monumentblocker-barrels-100x300"
            },
            new[]
            {
                "assets/content/structures/monumentblockers/monumentblocker_barricades.prefab",
                "monumentblocker_barricades.prefab",
                "monumentblocker-barricades"
            },
            new[]
            {
                "assets/content/structures/monumentblockers/monumentblocker_desks.prefab",
                "monumentblocker_desks.prefab",
                "monumentblocker-desks"
            },
            new[]
            {
                "assets/content/structures/monumentblockers/monumentblocker_lockers.prefab",
                "monumentblocker_lockers.prefab",
                "monumentblocker-lockers"
            },
            new[]
            {
                "assets/content/structures/monumentblockers/monumentblocker_terminals.prefab",
                "monumentblocker_terminals.prefab",
                "monumentblocker-terminals"
            },
            new[]
            {
                "assets/content/structures/monumentblockers/monumentblocker_trolley.prefab",
                "monumentblocker_trolley.prefab",
                "monumentblocker-trolley"
            },
            new[]
            {
                "assets/content/structures/monumentblockers/monumentblocker_ventcage.prefab",
                "monumentblocker_ventcage.prefab",
                "monumentblocker-ventcage"
            },
            new[]
            {
                "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab",
                "assets/prefabs/npc/patrol helicopter/servergibs_patrolhelicopter.prefab",
                "assets/prefabs/npc/patrol helicopter/patrol_helicopter_gibs_2.prefab",
                "servergibs_patrolhelicopter"
            },
            new[]
            {
                "assets/prefabs/npc/m2bradley/bradleyapc.prefab",
                "assets/prefabs/npc/m2bradley/m2bradley.prefab",
                "assets/prefabs/npc/m2bradley/servergibs_bradley.prefab",
                "servergibs_bradley"
            },
            new[]
            {
                "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",
                "assets/content/vehicles/scrap heli carrier/servergibs_scraptransport.prefab",
                "assets/content/vehicles/scrap heli carrier/scrapheli_gibs.prefab",
                "servergibs_scraptransport",
                "scrapheli_gibs"
            },
            new[]
            {
                "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab",
                "assets/content/vehicles/attackhelicopter/servergibs_attackhelicopter.prefab",
                "servergibs_attackhelicopter",
                "attackhelicopter.entity",
                "attackhelicopter"
            }
        };
        private readonly Queue<OreSpawnPosition> spawnQueue = new Queue<OreSpawnPosition>();
        private readonly Dictionary<ulong, List<ProtoBuf.MapNote>> playerMarkers = new Dictionary<ulong, List<ProtoBuf.MapNote>>();
        private readonly Dictionary<int, HashSet<ulong>> playerSpatialGrid = new Dictionary<int, HashSet<ulong>>();
        private readonly HashSet<uint> orePrefabIds = new HashSet<uint>();
        private readonly HashSet<uint> collectablePrefabIds = new HashSet<uint>();
        private readonly Dictionary<ulong, Coroutine> activeShowCoroutines = new Dictionary<ulong, Coroutine>();
        private readonly Dictionary<string, NpcHarvestSettings> _npcHarvestHitCache = new Dictionary<string, NpcHarvestSettings>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _npcHarvestMissCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, OreType> _oreTypeCache = new Dictionary<string, OreType>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _oreTypeMissCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Coroutine spawnCoroutine;
        private Coroutine cleanupCoroutine;
        private bool _harmonyPatchesApplied;
        private bool _oreSystemStarted;
        private bool _hasShouldBLPopulateContainer;
        private bool _hasShouldBLPopulateNpc;
        private bool _hasShouldBLGather;
        private bool _hasShouldBLCollectablePickup;
        private bool _apiHookRefreshQueued;
        private bool _gatherConflictCacheValid;
        private bool _cachedHasGatherConflict;
        private List<string>? _cachedGatherConflictNames;
        private float mapSize;
        private float halfMapSize;
        private float currentGridScalar = 1f;
        private float _cachedPlayerDistSqr;
        private float lastPopulationCheck;
        private int cachedPlayerCount;
        private int _trackedStoneCount, _trackedMetalCount, _trackedSulfurCount, _trackedHqmCount, _trackedCollectableCount, _trackedHarvestCount, _trackedRichCount;
        private const float PLAYER_GRID_SIZE = 100f;
        private const float POPULATION_CHECK_INTERVAL = 30f;
        #endregion
        
        /// <summary>
        /// Info for each weapon type item
        /// Contains info for building config data for if a item is a weapon (it will be in this dict) as well as how many mods and how much ammo it is allowed to have.
        /// </summary>
        private sealed class WI_Cache
        {
            public bool IsLiquidWeapon { get; } // is a watergun.
            public int MaxMods { get; } // if > 0 can have mods + max mods property.
            public int MaxAmmo { get; } // vanilla max ammo.
            public ItemSlot ModTypes { get; } // The types of mods that can be applied to this weapon.

            public WI_Cache(bool isLiquidWeapon, int maxMods, int maxAmmo, ItemSlot modTypes)
            {
                //IsLiquidWeapon = isLiquidWeapon;
                MaxMods = maxMods;
                MaxAmmo = maxAmmo;
                ModTypes = modTypes;
            }
        }
        #endregion
        #endregion

        #region Instance Constants
        private const double BASE_ITEM_RARITY = 2;
        private const string ADMIN_PERM = "betterloot.admin";
        private const string ORE_PERM_SHOW = "betterloot.oreshow";
        private const int MAX_SHOW_PINGS = 48;
        private const string ORE_DATA_FILE = "BetterLoot_OreSpawn";
        private const string STONE_ORE_PREFAB = "assets/bundled/prefabs/autospawn/resource/ores/stone-ore.prefab";
        private const string METAL_ORE_PREFAB = "assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab";
        private const string SULFUR_ORE_PREFAB = "assets/bundled/prefabs/autospawn/resource/ores/sulfur-ore.prefab";
        private const string STONE_COLLECTABLE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/stone/stone-collectable.prefab";
        private const string METAL_COLLECTABLE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/stone/metal-collectable.prefab";
        private const string SULFUR_COLLECTABLE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/stone/sulfur-collectable.prefab";
        private const string WOOD_COLLECTABLE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/wood/wood-collectable.prefab";
        private const string HEMP_COLLECTABLE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/hemp/hemp-collectable.prefab";
        private const string BERRY_BLACK_PREFAB = "assets/bundled/prefabs/autospawn/collectable/berry-black/berry-black-collectable.prefab";
        private const string BERRY_BLUE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/berry-blue/berry-blue-collectable.prefab";
        private const string BERRY_GREEN_PREFAB = "assets/bundled/prefabs/autospawn/collectable/berry-green/berry-green-collectable.prefab";
        private const string BERRY_RED_PREFAB = "assets/bundled/prefabs/autospawn/collectable/berry-red/berry-red-collectable.prefab";
        private const string BERRY_WHITE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/berry-white/berry-white-collectable.prefab";
        private const string BERRY_YELLOW_PREFAB = "assets/bundled/prefabs/autospawn/collectable/berry-yellow/berry-yellow-collectable.prefab";
        private const string MUSHROOM_CLUSTER_5_PREFAB = "assets/bundled/prefabs/autospawn/collectable/mushrooms/mushroom-cluster-5.prefab";
        private const string MUSHROOM_CLUSTER_6_PREFAB = "assets/bundled/prefabs/autospawn/collectable/mushrooms/mushroom-cluster-6.prefab";
        private const string CORN_COLLECTABLE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/corn/corn-collectable.prefab";
        private const string POTATO_COLLECTABLE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/potato/potato-collectable.prefab";
        private const string PUMPKIN_COLLECTABLE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/pumpkin/pumpkin-collectable.prefab";
        private const string ORCHID_COLLECTABLE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/orchid/orchid-collectable.prefab";
        private const string ROSE_COLLECTABLE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/rose/rose-collectable.prefab";
        private const string SUNFLOWER_COLLECTABLE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/sunflower/sunflower-collectable.prefab";
        private const string WHEAT_COLLECTABLE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/wheat/wheat-collectable.prefab";
        private const string HQM_ORE_PREFAB = "assets/bundled/prefabs/autospawn/resource/ores/hqm-ore.prefab";
        private const string WOOD_PILE_PREFAB = "assets/bundled/prefabs/autospawn/resource/wood_log_pile/wood-pile.prefab";
        private const string CACTUS_PREFAB = "assets/bundled/prefabs/autospawn/resource/v3_arid_cactus/cactus-1.prefab";
        private const string TREE_PREFAB = "assets/bundled/prefabs/autospawn/resource/v3_tundra_forestside/douglas_fir_d_small.prefab";
        private const string HQM_COLLECTABLE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/stone/hqm-collectable.prefab";
        private const string COCONUT_COLLECTABLE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/coconut/coconut-collectable.prefab";
        private const string DIESEL_COLLECTABLE_PREFAB = "assets/content/structures/excavator/prefabs/diesel_collectable.prefab";
        private const string NATURAL_BEEHIVE_PREFAB = "assets/prefabs/resource/natural beehive/beehive.natural.prefab";
        private const string HALLOWEEN_BONE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-bone-collectable.prefab";
        private const string HALLOWEEN_METAL_PREFAB = "assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-metal-collectable.prefab";
        private const string HALLOWEEN_STONE_PREFAB = "assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-stone-collectable.prefab";
        private const string HALLOWEEN_SULFUR_PREFAB = "assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-sulfur-collectible.prefab";
        private const string HALLOWEEN_WOOD_PREFAB = "assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-wood-collectable.prefab";
        private const string HQM_ORE_SHORTNAME = "hq.metal.ore";
        private const string METAL_ORE_SHORTNAME = "metal.ore";
        private const string RICH_VEIN_EFFECT = "assets/bundled/prefabs/fx/impacts/additive/explosion.prefab";
        private static readonly string[] MineGatherTools = { "jackhammer", "icepick.salvaged", "pickaxe", "stone.pickaxe", "hammer.salvaged", "bone.club", "rock" };
        private static readonly string[] ChopGatherTools = { "chainsaw", "axe.salvaged", "hatchet", "stonehatchet", "hammer.salvaged", "bone.club", "rock" };
        private static readonly string[] FleshGatherTools = { "knife.skinning", "knife.butcher", "knife.combat", "machete", "knife.bone", "pitchfork", "hatchet", "salvaged.sword", "rock" };
        private const string QUARRY_HQM_KEY = "HQM Quarry";
        private const string QUARRY_STONE_KEY = "Stone Quarry";
        private const string QUARRY_SULFUR_KEY = "Sulfur Quarry";
        private const string QUARRY_EXCAVATOR_KEY = "Giant Excavator";
        #endregion

        #region Lang
        private string BLLang(string key, string? id = null) => lang.GetMessage(key, this, id);
        private string BLLang(string key, string? id, params object[] args) => string.Format(BLLang(key, id), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "initialized", "Plugin not enabled" },
                { "perm", "You are not authorized to use this command" },
                { "syntax", "Usage: /blacklist [additem|deleteitem] \"ITEMNAME\"" },
                { "none", "There are no blacklisted items" },
                { "blocked", "Blacklisted items: {0}" },
                { "notvalid", "Not a valid item: {0}" },
                { "blockedpass", "The item '{0}' is now blacklisted" },
                { "blockedtrue", "The item '{0}' is already blacklisted}" },
                { "unblacklisted", "The item '{0}' has been unblacklisted" },
                { "blockedfalse", "The item '{0}' is not blacklisted" },
                { "lootycmdformat", "Usage: /looty \"looty-id\"" }, // Blank code provided.
                { "lootynotfound", "The requested table id was not found. Please ensure youve got the right code." } // 404 Looty api
            }, this); //en
        }
        #endregion

        #region Config
        private class PluginConfig : SerializableConfiguration
        {
            [JsonProperty("Chat Configuration")]
            public ChatConfiguration ChatConfig = new ChatConfiguration();
            [JsonProperty("General Configuration")]
            public GenericConfiguration Generic = new GenericConfiguration();
            [JsonProperty("Loot Configuration")]
            public LootConfiguration Loot = new LootConfiguration();
            [JsonProperty("Loot Groups Configuration")]
            public LootGroupsConfiguration LootGroupsConfig = new LootGroupsConfiguration();
        }

        private class GenericConfiguration
        {
            [JsonProperty("Blueprint Weight (0.0 = min bias, 1.0 = max bias, 0.5 = balanced)")]
            public double BlueprintWeight = 0.11;

            #region v4.2.1 Configuration Migration
            [JsonProperty("Blueprint Probability")]
            private double? LegacyBlueprintProbability { set => LegacyMigrate_BlueprintProbability(value); }

            private void LegacyMigrate_BlueprintProbability(double? value)
            {
                if (value.HasValue)
                    BlueprintWeight = value.Value;
            }
            #endregion

            [JsonProperty("Log Updates On Load")]
            public bool ListUpdatesOnLoad = true;
            [JsonProperty("Remove Stacked Containers")]
            public bool RemoveStackedContainers = true;
            [JsonProperty("Only update prefab list on wipe day")]
            public bool OnlyUpdatePrefabListOnWipe = false; // Slightly faster start time. (Doesnt check to regenerate prefab watch list from game manifest every startup).
            [JsonProperty("Auto enable new prefabs found on wipe")]
            public bool AutoEnableNewContainers = true;
            [JsonProperty("Watched Container Prefabs (true = monitor container loot, false = disabled)")]
            public Dictionary<string, bool> WatchedPrefabs = new();
            [JsonProperty("Stand down gather rates if other gather plugins are loaded")]
            public bool StandDownGatherRatesIfOtherPlugins = true;
            [JsonProperty("Other gather plugin names")]
            public List<string> OtherGatherPluginNames = new List<string>
            {
                "GatherManager",
                "InstantGather",
                "GatherControl",
                "BetterGather",
                "GUIGather",
                "GatherMultiplier"
            };

            #region v4.1.7 Configuration Migration
            [JsonProperty("Watched Prefabs")]
            private HashSet<string>? LegacyWatchedPrefabs { set => LegacyMerge_WatchedPrefabs(value); }

            private void LegacyMerge_WatchedPrefabs(IEnumerable<string>? values)
            {
                if (values == null)
                    return;

                foreach (string prefab in values)
                    if (!string.IsNullOrWhiteSpace(prefab))
                        WatchedPrefabs.TryAdd(prefab, true);
            }
            #endregion
        }

        private class LootConfiguration
        {
            [JsonProperty("Enable Hammer Hit Loot Cycle")]
            public bool EnableHammerLootCycle = false;
            [JsonProperty("Hammer Loot Cycle Time")]
            public double HammerLootCycleTime = 3.0;
            [JsonProperty("Loot Multiplier")]
            public int LootMultiplier = 1;
            [JsonProperty("Scrap Multipler")]
            public int ScrapMultiplier = 1;
            [JsonProperty("Allow duplicate items")]
            public bool AllowDuplicateItems = true;
            [JsonProperty("Enable logging for item attachments auto balancing operations")]
            public bool EnableBonusItemsAutoBalanceLogging = false;
            [JsonProperty("Always allow duplicate items from bonus items list (if set, will override 'Allow duplicate items option')")]
            public bool AllowBonusItemsDuplicateItems = true;
            [JsonProperty("Enable Blueprint Conversion")]
            public bool EnableBlueprintConversion = true;
            [JsonProperty("Allow Duplicate Blueprints")]
            public bool AllowDuplicateBlueprints = false;
            [JsonProperty("Enable Loot Pool Locking System")]
            public bool EnableLootPoolLocking = true;
            [JsonProperty("New loot tables use Vanilla Rust Loot RNG")]
            public bool DefaultToVanillaLootRng = true;
        }

        private class ChatConfiguration
        {
            [JsonProperty("Chat Message Prefix")]
            public string Prefix = $"[<color=#00ff00>{nameof(BetterLoot)}</color>]";
            [JsonProperty("Chat Message Icon SteamID (0 = None)")]
            public ulong MessageIcon = 0;
        }

        private class LootGroupsConfiguration
        {
            [JsonProperty("Enable creation of example loot group on load?")]
            public bool EnableExampleGroupCreation = true;
            [JsonProperty("Enable auto profile probability balancing?")]
            public bool EnableProbabilityBalancing = true;
            [JsonProperty("Always allow duplicate items from loot groups (if true overrides 'Allow duplicate items option')")]
            public bool AllowLootGroupDuplicateItems = true;
            [JsonProperty("Allowed probablity difference to select neighbour during duplicate item resolution.")]
            public double AllowedDuplicateNudgeDifference = 10;
        }

        public class OreSpawnConfiguration
        {
            [JsonProperty("Enable Custom Ore Spawning")]
            public bool Enabled = false;
            [JsonProperty("Custom Spawning Per Resource")]
            public bool PerResourceSpawning;
            [JsonProperty("Stone Settings")]
            public OreTypeSettings Stone = WithGather(new OreTypeSettings
            {
                GridSize = 150f,
                Prefab = "assets/bundled/prefabs/autospawn/resource/ores/stone-ore.prefab",
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.3f, TemperateBiome = 0.4f, ArcticBiome = 0.2f, TundraBiome = 0.35f, JungleBiome = 0.3f }
            }, MineGather("stones", 1000, 794, 536, 450, 375));
            [JsonProperty("Metal Settings")]
            public OreTypeSettings Metal = WithGather(new OreTypeSettings
            {
                GridSize = 200f,
                Prefab = "assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab",
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.25f, TemperateBiome = 0.3f, ArcticBiome = 0.35f, TundraBiome = 0.25f, JungleBiome = 0.2f }
            }, MineGather("metal.ore", 600, 485, 358, 286, 250), MineGather("hq.metal.ore", 2, 2, 0, 0, 0));
            [JsonProperty("Sulfur Settings")]
            public OreTypeSettings Sulfur = WithGather(new OreTypeSettings
            {
                GridSize = 180f,
                Prefab = "assets/bundled/prefabs/autospawn/resource/ores/sulfur-ore.prefab",
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.4f, TemperateBiome = 0.25f, ArcticBiome = 0.15f, TundraBiome = 0.3f, JungleBiome = 0.35f }
            }, MineGather("sulfur.ore", 300, 257, 146, 134, 100));
            [JsonProperty("HQM Settings")]
            public OreTypeSettings HQM = WithGather(new OreTypeSettings
            {
                GridSize = 320f,
                Prefab = HQM_ORE_PREFAB,
                Enabled = false,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.08f, TemperateBiome = 0.06f, ArcticBiome = 0.12f, TundraBiome = 0.1f, JungleBiome = 0.15f }
            }, MineGather("metal.ore", 400, 325, 291, 240, 200), MineGather("hq.metal.ore", 20, 17, 9, 8, 6));
            [JsonProperty("Wood Pile Settings")]
            public OreTypeSettings WoodPile = WithGather(new OreTypeSettings
            {
                GridSize = 220f,
                Prefab = WOOD_PILE_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.05f, TemperateBiome = 0.4f, ArcticBiome = 0.25f, TundraBiome = 0.35f, JungleBiome = 0.1f }
            }, ChopGather("wood", 1000, 1000, 848, 787, 735, 447, 400));
            [JsonProperty("Cactus Settings")]
            public OreTypeSettings Cactus = WithGather(new OreTypeSettings
            {
                GridSize = 120f,
                Prefab = CACTUS_PREFAB,
                Enabled = false,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.55f, TemperateBiome = 0f, ArcticBiome = 0f, TundraBiome = 0f, JungleBiome = 0.05f }
            }, ChopGather("cloth", 15, 15, 12, 10, 10, 6, 5), ChopGather("cactusflesh", 3, 3, 2, 2, 2, 1, 1));
            [JsonProperty("Tree Settings")]
            public OreTypeSettings Tree = WithGather(new OreTypeSettings
            {
                GridSize = 80f,
                Prefab = TREE_PREFAB,
                Enabled = false,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.1f, TemperateBiome = 0.45f, ArcticBiome = 0.25f, TundraBiome = 0.4f, JungleBiome = 0.4f }
            }, ChopGather("wood", 1000, 1000, 850, 750, 700, 450, 400));
            [JsonProperty("Collectable Settings")]
            public OreCollectableConfiguration Collectables = new OreCollectableConfiguration();
            [JsonProperty("Debug Mode")]
            public bool Debug;
            [JsonProperty("Spawn Delay (seconds)")]
            public float SpawnDelay = 0.05f;
            [JsonProperty("Min Distance From Players")]
            public float MinPlayerDistance = 50f;
            [JsonProperty("Min Distance From Buildings")]
            public float MinBuildingDistance = 25f;
            [JsonProperty("Water Buffer Height")]
            public float WaterBuffer = 1.5f;
            [JsonProperty("Max Slope Angle")]
            public float MaxSlope = 35f;
            [JsonProperty("Batch Size")]
            public int BatchSize = 10;
            [JsonProperty("Population Scaling Settings")]
            public OrePopulationScalingSettings PopulationScaling = new OrePopulationScalingSettings();
            [JsonProperty("Disable Native Ore Spawning")]
            public bool DisableNativeSpawning = true;
            [JsonProperty("Rich Vein Settings")]
            public OreRichVeinSettings RichVein = new OreRichVeinSettings();
            [JsonProperty("Global Spawn Multiplier")]
            public float GlobalSpawnMultiplier = 1.0f;
            [JsonProperty("Respawn Interval (minutes)")]
            public float RespawnIntervalMinutes = 1.0f;
            [JsonProperty("Node Density (0.0 - 1.0)")]
            public float NodeDensity = 1.0f;
            [JsonProperty("Wipe Ores On Plugin Unload")]
            public bool WipeOnUnload;
            [JsonProperty("NPC Harvest")]
            public Dictionary<string, NpcHarvestSettings> NpcHarvest = new Dictionary<string, NpcHarvestSettings>(StringComparer.OrdinalIgnoreCase);
            [JsonProperty("Quarry Harvest")]
            public Dictionary<string, QuarryHarvestSettings> QuarryHarvest = new Dictionary<string, QuarryHarvestSettings>(StringComparer.OrdinalIgnoreCase);

            private static OreTypeSettings WithGather(OreTypeSettings settings, params OreGatherOutput[] outputs)
            {
                settings.OverrideGatherAmounts = true;
                settings.GatherOutputs = new List<OreGatherOutput>(outputs);
                return settings;
            }

            private static OreGatherOutput MineGather(string shortname, int best, int stonePick, int hammer, int club, int rock)
            {
                return new OreGatherOutput
                {
                    Shortname = shortname,
                    AmountsByTool = new Dictionary<string, int>
                    {
                        ["jackhammer"] = best,
                        ["icepick.salvaged"] = best,
                        ["pickaxe"] = best,
                        ["stone.pickaxe"] = stonePick,
                        ["hammer.salvaged"] = hammer,
                        ["bone.club"] = club,
                        ["rock"] = rock
                    }
                };
            }

            private static OreGatherOutput ChopGather(string shortname, int chainsaw, int salvagedAxe, int hatchet, int stoneHatchet, int hammer, int club, int rock)
            {
                return new OreGatherOutput
                {
                    Shortname = shortname,
                    AmountsByTool = new Dictionary<string, int>
                    {
                        ["chainsaw"] = chainsaw,
                        ["axe.salvaged"] = salvagedAxe,
                        ["hatchet"] = hatchet,
                        ["stonehatchet"] = stoneHatchet,
                        ["hammer.salvaged"] = hammer,
                        ["bone.club"] = club,
                        ["rock"] = rock
                    }
                };
            }
        }

        public class OrePopulationScalingSettings
        {
            [JsonProperty("Enable Population Scaling")]
            public bool Enabled = true;
            [JsonProperty("Minimum Players Threshold")]
            public int MinPlayers = 50;
            [JsonProperty("Maximum Players Threshold")]
            public int MaxPlayers = 200;
            [JsonProperty("Grid Scalar At Min Players (sparse)")]
            public float MinPlayerScalar = 1.5f;
            [JsonProperty("Grid Scalar At Max Players (dense)")]
            public float MaxPlayerScalar = 0.8f;
        }

        public class OreRichVeinSettings
        {
            [JsonProperty("Enable Rich Veins")]
            public bool Enabled = true;
            [JsonProperty("Rich Vein Chance (0.0 - 1.0)")]
            public float Chance = 0.05f;
            [JsonProperty("Rich Vein Gather Multiplier")]
            public float Multiplier = 2.0f;
            [JsonProperty("Play Effect On Rich Vein Gather")]
            public bool PlayEffect = true;
        }

        public class OreCollectableConfiguration
        {
            [JsonProperty("Stone")]
            public OreTypeSettings Stone = new OreTypeSettings
            {
                GridSize = 90f,
                Prefab = STONE_COLLECTABLE_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.3f, TemperateBiome = 0.35f, ArcticBiome = 0.25f, TundraBiome = 0.3f, JungleBiome = 0.3f }
            };
            [JsonProperty("Metal")]
            public OreTypeSettings Metal = new OreTypeSettings
            {
                GridSize = 110f,
                Prefab = METAL_COLLECTABLE_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.25f, TemperateBiome = 0.3f, ArcticBiome = 0.3f, TundraBiome = 0.25f, JungleBiome = 0.2f }
            };
            [JsonProperty("Sulfur")]
            public OreTypeSettings Sulfur = new OreTypeSettings
            {
                GridSize = 110f,
                Prefab = SULFUR_COLLECTABLE_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.4f, TemperateBiome = 0.25f, ArcticBiome = 0.15f, TundraBiome = 0.3f, JungleBiome = 0.35f }
            };
            [JsonProperty("Wood")]
            public OreTypeSettings Wood = new OreTypeSettings
            {
                GridSize = 80f,
                Prefab = WOOD_COLLECTABLE_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.3f, TemperateBiome = 0.35f, ArcticBiome = 0.25f, TundraBiome = 0.3f, JungleBiome = 0.3f }
            };
            [JsonProperty("Hemp")]
            public OreTypeSettings Hemp = new OreTypeSettings
            {
                GridSize = 70f,
                Prefab = HEMP_COLLECTABLE_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.15f, TemperateBiome = 0.45f, ArcticBiome = 0.05f, TundraBiome = 0.4f, JungleBiome = 0.5f }
            };
            [JsonProperty("Berry Black")]
            public OreTypeSettings BerryBlack = new OreTypeSettings
            {
                GridSize = 140f,
                Prefab = BERRY_BLACK_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.1f, TemperateBiome = 0.35f, ArcticBiome = 0.05f, TundraBiome = 0.3f, JungleBiome = 0.25f }
            };
            [JsonProperty("Berry Blue")]
            public OreTypeSettings BerryBlue = new OreTypeSettings
            {
                GridSize = 140f,
                Prefab = BERRY_BLUE_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.1f, TemperateBiome = 0.35f, ArcticBiome = 0.05f, TundraBiome = 0.3f, JungleBiome = 0.25f }
            };
            [JsonProperty("Berry Green")]
            public OreTypeSettings BerryGreen = new OreTypeSettings
            {
                GridSize = 140f,
                Prefab = BERRY_GREEN_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.1f, TemperateBiome = 0.35f, ArcticBiome = 0.05f, TundraBiome = 0.3f, JungleBiome = 0.25f }
            };
            [JsonProperty("Berry Red")]
            public OreTypeSettings BerryRed = new OreTypeSettings
            {
                GridSize = 140f,
                Prefab = BERRY_RED_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.1f, TemperateBiome = 0.35f, ArcticBiome = 0.05f, TundraBiome = 0.3f, JungleBiome = 0.25f }
            };
            [JsonProperty("Berry White")]
            public OreTypeSettings BerryWhite = new OreTypeSettings
            {
                GridSize = 140f,
                Prefab = BERRY_WHITE_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.1f, TemperateBiome = 0.35f, ArcticBiome = 0.05f, TundraBiome = 0.3f, JungleBiome = 0.25f }
            };
            [JsonProperty("Berry Yellow")]
            public OreTypeSettings BerryYellow = new OreTypeSettings
            {
                GridSize = 140f,
                Prefab = BERRY_YELLOW_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.1f, TemperateBiome = 0.35f, ArcticBiome = 0.05f, TundraBiome = 0.3f, JungleBiome = 0.25f }
            };
            [JsonProperty("Mushroom")]
            public OreTypeSettings Mushroom = new OreTypeSettings
            {
                GridSize = 120f,
                Prefab = MUSHROOM_CLUSTER_5_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.08f, TemperateBiome = 0.4f, ArcticBiome = 0.2f, TundraBiome = 0.4f, JungleBiome = 0.35f }
            };
            [JsonProperty("Corn")]
            public OreTypeSettings Corn = new OreTypeSettings
            {
                GridSize = 160f,
                Prefab = CORN_COLLECTABLE_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.35f, TemperateBiome = 0.4f, ArcticBiome = 0.05f, TundraBiome = 0.15f, JungleBiome = 0.25f }
            };
            [JsonProperty("Potato")]
            public OreTypeSettings Potato = new OreTypeSettings
            {
                GridSize = 160f,
                Prefab = POTATO_COLLECTABLE_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.1f, TemperateBiome = 0.35f, ArcticBiome = 0.25f, TundraBiome = 0.4f, JungleBiome = 0.2f }
            };
            [JsonProperty("Pumpkin")]
            public OreTypeSettings Pumpkin = new OreTypeSettings
            {
                GridSize = 160f,
                Prefab = PUMPKIN_COLLECTABLE_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.3f, TemperateBiome = 0.4f, ArcticBiome = 0.05f, TundraBiome = 0.2f, JungleBiome = 0.25f }
            };
            [JsonProperty("Orchid")]
            public OreTypeSettings Orchid = new OreTypeSettings
            {
                GridSize = 150f,
                Prefab = ORCHID_COLLECTABLE_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.1f, TemperateBiome = 0.35f, ArcticBiome = 0.05f, TundraBiome = 0.2f, JungleBiome = 0.45f }
            };
            [JsonProperty("Rose")]
            public OreTypeSettings Rose = new OreTypeSettings
            {
                GridSize = 150f,
                Prefab = ROSE_COLLECTABLE_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.15f, TemperateBiome = 0.4f, ArcticBiome = 0.05f, TundraBiome = 0.25f, JungleBiome = 0.3f }
            };
            [JsonProperty("Sunflower")]
            public OreTypeSettings Sunflower = new OreTypeSettings
            {
                GridSize = 150f,
                Prefab = SUNFLOWER_COLLECTABLE_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.35f, TemperateBiome = 0.4f, ArcticBiome = 0.05f, TundraBiome = 0.2f, JungleBiome = 0.25f }
            };
            [JsonProperty("Wheat")]
            public OreTypeSettings Wheat = new OreTypeSettings
            {
                GridSize = 150f,
                Prefab = WHEAT_COLLECTABLE_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.35f, TemperateBiome = 0.4f, ArcticBiome = 0.05f, TundraBiome = 0.25f, JungleBiome = 0.15f }
            };
            [JsonProperty("HQM")]
            public OreTypeSettings HQM = new OreTypeSettings
            {
                GridSize = 280f,
                Prefab = HQM_COLLECTABLE_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.08f, TemperateBiome = 0.06f, ArcticBiome = 0.04f, TundraBiome = 0.08f, JungleBiome = 0.12f }
            };
            [JsonProperty("Coconut")]
            public OreTypeSettings Coconut = new OreTypeSettings
            {
                GridSize = 160f,
                Prefab = COCONUT_COLLECTABLE_PREFAB,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.15f, TemperateBiome = 0.05f, ArcticBiome = 0f, TundraBiome = 0.05f, JungleBiome = 0.45f }
            };
            [JsonProperty("Diesel")]
            public OreTypeSettings Diesel = new OreTypeSettings
            {
                GridSize = 400f,
                Prefab = DIESEL_COLLECTABLE_PREFAB,
                Enabled = false,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.3f, TemperateBiome = 0.35f, ArcticBiome = 0.25f, TundraBiome = 0.3f, JungleBiome = 0.3f }
            };
            [JsonProperty("Halloween Bone")]
            public OreTypeSettings HalloweenBone = new OreTypeSettings
            {
                GridSize = 160f,
                Prefab = HALLOWEEN_BONE_PREFAB,
                Enabled = false,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.3f, TemperateBiome = 0.35f, ArcticBiome = 0.25f, TundraBiome = 0.3f, JungleBiome = 0.3f }
            };
            [JsonProperty("Halloween Metal")]
            public OreTypeSettings HalloweenMetal = new OreTypeSettings
            {
                GridSize = 160f,
                Prefab = HALLOWEEN_METAL_PREFAB,
                Enabled = false,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.3f, TemperateBiome = 0.35f, ArcticBiome = 0.25f, TundraBiome = 0.3f, JungleBiome = 0.3f }
            };
            [JsonProperty("Halloween Stone")]
            public OreTypeSettings HalloweenStone = new OreTypeSettings
            {
                GridSize = 160f,
                Prefab = HALLOWEEN_STONE_PREFAB,
                Enabled = false,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.3f, TemperateBiome = 0.35f, ArcticBiome = 0.25f, TundraBiome = 0.3f, JungleBiome = 0.3f }
            };
            [JsonProperty("Halloween Sulfur")]
            public OreTypeSettings HalloweenSulfur = new OreTypeSettings
            {
                GridSize = 160f,
                Prefab = HALLOWEEN_SULFUR_PREFAB,
                Enabled = false,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.3f, TemperateBiome = 0.35f, ArcticBiome = 0.25f, TundraBiome = 0.3f, JungleBiome = 0.3f }
            };
            [JsonProperty("Halloween Wood")]
            public OreTypeSettings HalloweenWood = new OreTypeSettings
            {
                GridSize = 160f,
                Prefab = HALLOWEEN_WOOD_PREFAB,
                Enabled = false,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.3f, TemperateBiome = 0.35f, ArcticBiome = 0.25f, TundraBiome = 0.3f, JungleBiome = 0.3f }
            };
        }

        public class OreTypeSettings
        {
            [JsonProperty("Grid Size")]
            public float GridSize;
            [JsonProperty("Prefab Path")]
            public string Prefab;
            [JsonProperty("Biome Probabilities")]
            public OreBiomeProbabilities BiomeProbabilities = new OreBiomeProbabilities();
            [JsonProperty("Enabled")]
            public bool Enabled = true;
            [JsonProperty("Bonus Loot")]
            public List<OreBonusLootEntry> BonusLoot = new List<OreBonusLootEntry>();
            [JsonProperty("Override Gather Amounts")]
            public bool OverrideGatherAmounts;
            [JsonProperty("Gather Multiplier")]
            public float GatherMultiplier = 1f;
            [JsonProperty("Gather Outputs")]
            public List<OreGatherOutput> GatherOutputs = new List<OreGatherOutput>();
            [JsonProperty("Override Pickup Amounts")]
            public bool OverridePickupAmounts;
            [JsonProperty("Pickup Outputs")]
            public List<OrePickupOutput> PickupOutputs = new List<OrePickupOutput>();
            [JsonProperty("Pickup Min Amount")]
            public int PickupMinAmount;
            [JsonProperty("Pickup Max Amount")]
            public int PickupMaxAmount;
        }

        public class NpcHarvestSettings
        {
            [JsonProperty("Override Gather Amounts")]
            public bool OverrideGatherAmounts;
            [JsonProperty("Gather Outputs")]
            public List<OreGatherOutput> GatherOutputs = new List<OreGatherOutput>();
        }

        public class QuarryHarvestSettings
        {
            [JsonProperty("Override Gather Amounts")]
            public bool OverrideGatherAmounts;
            [JsonProperty("Outputs")]
            public List<QuarryHarvestOutput> Outputs = new List<QuarryHarvestOutput>();
        }

        public class QuarryHarvestOutput
        {
            [JsonProperty("Item Shortname")]
            public string Shortname = string.Empty;
            [JsonProperty("Amount")]
            public int Amount;
        }

        public class OreGatherOutput
        {
            [JsonProperty("Item Shortname")]
            public string Shortname = string.Empty;
            [JsonProperty("Amounts By Tool")]
            public Dictionary<string, int> AmountsByTool = new Dictionary<string, int>();
        }

        public class OrePickupOutput
        {
            [JsonProperty("Item Shortname")]
            public string Shortname = string.Empty;
            [JsonProperty("Min Amount")]
            public int MinAmount = 1;
            [JsonProperty("Max Amount")]
            public int MaxAmount = 1;
        }

        public class OreBonusLootEntry
        {
            [JsonProperty("Item Shortname")]
            public string Shortname = "scrap";
            [JsonProperty("Drop Chance (0.0 - 1.0)")]
            public float Chance = 0.01f;
            [JsonProperty("Min Amount")]
            public int MinAmount = 1;
            [JsonProperty("Max Amount")]
            public int MaxAmount = 1;
        }

        public class OreBiomeProbabilities
        {
            [JsonProperty("Arid Biome")]
            public float AridBiome;
            [JsonProperty("Temperate Biome")]
            public float TemperateBiome;
            [JsonProperty("Arctic Biome")]
            public float ArcticBiome;
            [JsonProperty("Tundra Biome")]
            public float TundraBiome;
            [JsonProperty("Jungle Biome")]
            public float JungleBiome;
        }

        /// <summary>
        /// As of v4.1.7 this system runs everytime to check for new prefabs as well as maintain the integrity of the current prefab list that is available so it can be enabled / disabled easily at any time.
        /// This behaviour can optionally be disabled in the config for it to only run on wipe day (to update for any new prefab types after a update)
        /// </summary>
        private void CheckWatchedPrefabs()
        {
            /* Watched Prefabs Auto-Population */
            NewConfigGenerated = _config.Generic.WatchedPrefabs.Count == 0;
            EnsureNaturalBeehiveWatched();

            if (_config.Generic.OnlyUpdatePrefabListOnWipe && !NewSave) // If it is a new wipe new found prefabs will be auto enabled.
                return;

            if (NewConfigGenerated)
                Log("Checking for missing viable loot containers in prefab watch list. (Currently disabled containers will stay disabled).");

            // Name filtering
            List<string> negativePartialNames = Pool.Get<List<string>>();
            List<string> partialNames = Pool.Get<List<string>>();

            // If does not contain, skip
            negativePartialNames.AddRange(new[]
            {
                "resource/loot",
                "misc/supply drop/supply_drop",
                "/npc/m2bradley/bradley_crate",
                "/npc/patrol helicopter/heli_crate",
                "/deployable/chinooklockedcrate/chinooklocked",
                "/deployable/chinooklockedcrate/codelocked",
                "prefabs/radtown",
                "props/roadsigns",
                "humannpc/scientist",
                "humannpc/tunneldweller",
                "humannpc/underwaterdweller",
                "/npc/gingerbread",
                "/npc/scarecrow",
                "ptboat.deepsea",
                "rhib.deepsea",
                "cache/food",
                "assets/prefabs/satellitecrash/crates",
                "natural beehive/beehive.natural"
            });

            // If does contain, skip
            partialNames.AddRange(new[]
            {
                "radtown/ore",
                "static",
                "/spawners",
                "radtown/desk",
                "radtown/loot_component_test",
                "chinooklockedcrate/chinooklockedcrate", // Specific crate with no spawnable
                "water_puddles_border_fix" // Weird container prefab from radtown update??
            });

            // Adding default values
            foreach (GameManifest.PrefabProperties category in GameManifest.Current.prefabProperties)
            {
                string name = category.name;

                if (!negativePartialNames.ContainsPartial(name) || partialNames.ContainsPartial(name))
                    continue;

                // Add false by default, may have been disabled by user if was not in list from previous version. If is a new wipe and auto-enable is set, containers will be added as enabled prefabs.
                _config.Generic.WatchedPrefabs.TryAdd(name, NewConfigGenerated || (NewSave && _config.Generic.AutoEnableNewContainers));
            }

            // Presents, crack-open eggs, loot bags, and other ItemModUnwrap-derived items are
            // inventory items rather than prefabs, so expose them through stable synthetic keys.
            bool addedUnwrapSource = false;
            foreach (ItemDefinition itemDefinition in ItemManager.itemList)
            {
                if (itemDefinition.GetComponentInChildren<ItemModUnwrap>() is not null &&
                    _config.Generic.WatchedPrefabs.TryAdd(ToUnwrapKey(itemDefinition),
                        NewConfigGenerated || (NewSave && _config.Generic.AutoEnableNewContainers)))
                    addedUnwrapSource = true;
            }

            if (addedUnwrapSource)
                Changed = true;

            SyncWatchedPrefabsFromLootTables(enableIfTableEnabled: false);

            if (NewConfigGenerated)
            {
                Log("Updated configuration with manifest values.");
                AttemptSendLootyLink();
            }

            Pool.FreeUnmanaged(ref negativePartialNames);
            Pool.FreeUnmanaged(ref partialNames);
        }

        private void EnsureNaturalBeehiveWatched()
        {
            if (_config?.Generic?.WatchedPrefabs == null)
                return;

            bool enable = NewConfigGenerated || (NewSave && _config.Generic.AutoEnableNewContainers);
            if (lootTables?.LootTables != null
                && lootTables.LootTables.TryGetValue(NATURAL_BEEHIVE_PREFAB, out PrefabLoot? table)
                && table?.Enabled == true)
                enable = true;

            if (!_config.Generic.WatchedPrefabs.TryGetValue(NATURAL_BEEHIVE_PREFAB, out bool current))
            {
                _config.Generic.WatchedPrefabs[NATURAL_BEEHIVE_PREFAB] = enable;
                Changed = true;
                return;
            }

            if (enable && !current)
            {
                _config.Generic.WatchedPrefabs[NATURAL_BEEHIVE_PREFAB] = true;
                Changed = true;
            }
        }

        private void SyncWatchedPrefabsFromLootTables(bool enableIfTableEnabled)
        {
            if (lootTables?.LootTables == null || _config?.Generic?.WatchedPrefabs == null)
                return;

            foreach (var kv in lootTables.LootTables)
            {
                bool enabled = kv.Value?.Enabled ?? false;
                if (enableIfTableEnabled && enabled)
                {
                    if (!_config.Generic.WatchedPrefabs.TryGetValue(kv.Key, out bool current) || !current)
                    {
                        _config.Generic.WatchedPrefabs[kv.Key] = true;
                        Changed = true;
                    }
                    continue;
                }

                if (_config.Generic.WatchedPrefabs.TryAdd(kv.Key, enabled))
                    Changed = true;
            }
        }

        private static string ToUnwrapKey(ItemDefinition itemDefinition)
            => UNWRAP_PREFIX + itemDefinition.shortname;

        private static bool IsUnwrapKey(string key)
            => key.StartsWith(UNWRAP_PREFIX, StringComparison.OrdinalIgnoreCase);

        private static bool IsMissionKey(string key)
            => key.StartsWith(MISSION_PREFIX, StringComparison.OrdinalIgnoreCase);

        private static string CompactMissionToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var chars = new char[value.Length];
            int n = 0;
            foreach (char c in value)
            {
                if (!char.IsLetterOrDigit(c))
                    continue;
                chars[n++] = char.ToLowerInvariant(c);
            }

            string compact = n == 0 ? string.Empty : new string(chars, 0, n);
            if (compact.StartsWith("mission", StringComparison.Ordinal) && compact.Length > 7)
                compact = compact.Substring(7);
            return compact;
        }

        private static void AddMissionToken(List<string> tokens, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            tokens.Add(value.Trim());
            string compact = CompactMissionToken(value);
            if (!string.IsNullOrEmpty(compact))
                tokens.Add(compact);
        }

        private static IEnumerable<string> EnumerateMissionTokens(BaseMission mission)
        {
            var tokens = new List<string>();
            AddMissionToken(tokens, mission.name);

            Type type = mission.GetType();
            foreach (string name in new[] { "shortname", "Shortname", "shortName" })
            {
                FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field?.GetValue(mission) is string fieldValue)
                    AddMissionToken(tokens, fieldValue);

                PropertyInfo? prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop?.GetValue(mission) is string propValue)
                    AddMissionToken(tokens, propValue);
            }

            foreach (string name in new[] { "missionName", "missionTitle", "title" })
            {
                object? raw = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(mission)
                              ?? type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(mission);
                if (raw is string text)
                    AddMissionToken(tokens, text);
                else if (raw is Translate.Phrase phrase)
                    AddMissionToken(tokens, phrase.english);
            }

            return tokens;
        }

        private string? ResolveMissionLootKey(BaseMission mission)
        {
            if (mission is null || lootTables?.LootTables is null)
                return null;

            foreach (string token in EnumerateMissionTokens(mission))
            {
                if (IsMissionKey(token) && lootTables.LootTables.ContainsKey(token))
                    return token;

                string hyphenated = MISSION_PREFIX + token.Trim().Replace('_', '-').Replace(' ', '-').ToLowerInvariant();
                if (lootTables.LootTables.ContainsKey(hyphenated))
                    return hyphenated;

                string compact = CompactMissionToken(token);
                if (string.IsNullOrEmpty(compact))
                    continue;

                foreach (string key in lootTables.LootTables.Keys)
                {
                    if (!IsMissionKey(key))
                        continue;
                    if (CompactMissionToken(key.Substring(MISSION_PREFIX.Length)) == compact)
                        return key;
                }
            }

            return null;
        }

        private static BasePlayer? TryPlayerFromMissionObject(object? obj)
        {
            if (obj is BasePlayer player)
                return player;
            if (obj is null)
                return null;

            Type type = obj.GetType();
            foreach (string name in new[] { "player", "Player", "ownerPlayer", "OwnerPlayer", "initiator", "Initiator" })
            {
                if (type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(obj) is BasePlayer fromField)
                    return fromField;
                if (type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(obj) is BasePlayer fromProp)
                    return fromProp;
            }

            return null;
        }

        private static bool PrefabLootHasMissionItems(PrefabLoot profile)
        {
            if (profile.GuaranteedItems is { Count: > 0 })
                return true;
            if (profile.UngroupedItems is { Count: > 0 })
                return true;
            if (profile.LootProfiles is { Count: > 0 } && profile.LootProfiles.Exists(p => p.Enabled))
                return true;
            return profile.UseVanillaLootRng && profile.VanillaLootSlots is { Count: > 0 };
        }

        private bool TryGiveCustomMissionRewards(BaseMission mission, BasePlayer? player)
        {
            if (!Initialized || mission is null || player is null || !player.IsConnected)
                return false;

            if (player.userID == _missionRewardStampPlayer &&
                UnityEngine.Time.realtimeSinceStartup - _missionRewardStampTime < 1f)
                return true;

            string? key = ResolveMissionLootKey(mission);
            if (key is null ||
                !(lootTables?.LootTables?.TryGetValue(key, out PrefabLoot? profile) ?? false) ||
                profile is null ||
                !profile.Enabled ||
                !PrefabLootHasMissionItems(profile))
                return false;

            if (!PopulateContainer(player.inventory.containerMain, key, false, player.inventory.containerBelt, player, out int deliveredItems) ||
                deliveredItems == 0)
                return false;

            _missionRewardStampPlayer = player.userID;
            _missionRewardStampTime = UnityEngine.Time.realtimeSinceStartup;
            return true;
        }

        private object? OnMissionSucceeded(BaseMission mission, object arg1, object arg2)
        {
            BasePlayer? player = TryPlayerFromMissionObject(arg1) ?? TryPlayerFromMissionObject(arg2);
            TryGiveCustomMissionRewards(mission, player);
            return null;
        }

        private static ItemModUnwrap? FindUnwrapMod(string key)
        {
            if (!IsUnwrapKey(key))
                return null;

            ItemDefinition? itemDefinition = ItemManager.FindItemDefinition(key.Substring(UNWRAP_PREFIX.Length));
            return itemDefinition?.GetComponentInChildren<ItemModUnwrap>();
        }

        protected override void LoadDefaultConfig() => _config = new PluginConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                var raw = Config.ReadObject<JObject>() ?? new JObject();
                if (raw["Ore Spawn Configuration"] is JObject oreToken)
                {
                    try
                    {
                        _legacyConfigOreSpawn = oreToken.ToObject<OreSpawnConfiguration>();
                    }
                    catch (Exception ex)
                    {
                        Log($"Could not migrate Ore Spawn Configuration from BetterLoot.json: {ex.Message}");
                    }

                    raw.Remove("Ore Spawn Configuration");
                    Config.WriteObject(raw, true);
                    Log("Moved Ore Spawn settings out of BetterLoot.json. They now live in oxide/data/BetterLoot/GatherRates.json");
                }

                _config = raw.ToObject<PluginConfig>() ?? new PluginConfig();

                if (MaybeUpdateConfig(_config))
                {
                    Log("Configuration appears to be outdated; updating and saving Better Loot");
                    SaveConfig();
                }

                Log("Loaded configuration!");
            }
            catch (Exception ex)
            {
                Log("Failed to load Better Loot config file (is the config file corrupt?) (" + ex.Message + ")");
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {nameof(BetterLoot)}.json");
            Config.WriteObject(_config, true);
        }

        #region Configuration Updater
        internal class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>().ToDictionary(prop => prop.Name, prop => ToObject(prop.Value));
                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                if (currentRaw.TryGetValue(key, out object? currentRawValue) && currentRawValue is not null)
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }
        #endregion
        #endregion

        #region Oxide Loaded / Unload / Server Load / New Save
        private void Loaded()
        {
            _instance = this;
            RNG = new Random();
            UniqueTagREGEX = new Regex(@"\{\d+\}", RegexOptions.Compiled);
            
            DataSystem.LoadBlacklist();
            DataSystem.LoadLootTables();
            DataSystem.LoadLootGroups();
            DataSystem.LoadOreSpawn();
        }

        private void OnServerInitialized()
        {
            try
            {
                ItemManager.Initialize();
                BlueprintBaseDef = ItemManager.FindItemDefinition("blueprintbase");
                CheckWatchedPrefabs(); // ItemManager must be initialized before unwrap sources can be discovered.
                PatchNestedClasses();
                BuildWeaponInfoCache();

                permission.RegisterPermission(ADMIN_PERM, this);
                permission.RegisterPermission(ORE_PERM_SHOW, this);
                Unsubscribe(nameof(OnDispenserGather));
                Unsubscribe(nameof(OnDispenserBonus));
                Unsubscribe(nameof(OnCollectiblePickup));
                Unsubscribe(nameof(OnEntityKill));
                Unsubscribe(nameof(OnEntitySpawned));
                Unsubscribe(nameof(OnMeleeAttack));
                if (_config?.Loot?.EnableHammerLootCycle == true)
                    Subscribe(nameof(OnMeleeAttack));
                InitLootSystem();
                InitOreSpawnSystem();
                RefreshOptionalApiHooks();
            }
            catch (Exception ex)
            {
                Puts($"Error initializing plugin. Please ensure you configuration and data files are current and that you have used the looty editor to confirm. EX: \n{ex.Message}");
                Server.Command($"o.unload {Name}");
            }
        }

        private void InitLootSystem(bool newData = false)
        {
            // Ensure empty, is called when looty tables are loaded.
            if (newData)
            {
                Items.Clear();
                Blueprints.Clear();
                ItemWeights.Clear();
                BlueprintWeights.Clear();
                TotalItemWeights.Clear();
                TotalBlueprintWeights.Clear();

                BuildWeaponInfoCache();
            }

            // Load container data
            LoadAllContainers();

            Pool.FreeUnmanaged(ref WeaponInfoCache);
            Pool.FreeUnmanaged(ref WeaponModInfoCache);
            Pool.FreeUnmanaged(ref DurabilityItems);

            UpdateInternals(_config.Generic.ListUpdatesOnLoad);
        }

        private void Unload()
        {
            // Static variable instances
            UniqueTagREGEX = null;

            storedBlacklist = null;
            lootTables = null;
            lootGroups = null;
            RNG = null;
            BlueprintBaseDef = null;

            // Reset Static Flags
            NewConfigGenerated = false;
            NewSave = false;
            Initialized = false;
            _gatherRatesNeedsVanillaFill = false;
            _loggedGatherStandDown = false;

            // Static BetterLoot instance
            _instance = null;
            _config = null;

            ShutdownOreSpawnSystem();

            foreach (HammerHitLootCycle hhlc in UnityEngine.Object.FindObjectsByType<HammerHitLootCycle>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(i => i is not null))
                UnityEngine.Object.Destroy(hhlc);
        }

        // Set flag so new prefabs can be autoenabled
        private void OnNewSave(string _)
            => NewSave = true;
        #endregion

        #region DataFile
        private static LootTableData? lootTables = null;
        private static StoredBlacklist? storedBlacklist = null;
        private static LootGroupsData? lootGroups = null;
        private static OreSpawnData? oreSpawnData = null;

        // Looty API Schema
        private sealed record LootyResponse
        {
            public LootyTarget looty = new ();
            [JsonProperty("LootTable")]
            public Dictionary<string, PrefabLoot> LootTables = new();
            [JsonProperty("Loot Groups")]
            public Dictionary<string, LootProfile>? LootGroups = new();
            [JsonProperty("Ore Spawn")]
            public OreSpawnConfiguration? OreSpawn;
            [JsonProperty("Gather Rates")]
            public OreSpawnConfiguration? GatherRatesPayload
            {
                get => null;
                set
                {
                    if (value != null)
                        OreSpawn = value;
                }
            }
            public bool ShouldSerializeGatherRatesPayload() => false;

            public LootyResponse() { }

            public sealed record LootyTarget
            {
                public string target;
                public string id;
            }
        }

        // LootTables.json structure
        private class LootTableData
        {
            public Dictionary<string, PrefabLoot> LootTables = new Dictionary<string, PrefabLoot>();

            public LootTableData() { }
        }

        // GatherRates.json structure
        private class OreSpawnData
        {
            [JsonProperty("Gather Rates")]
            public OreSpawnConfiguration OreSpawn = new OreSpawnConfiguration();

            [JsonProperty("Ore Spawn")]
            public OreSpawnConfiguration LegacyOreSpawn
            {
                get => OreSpawn;
                set
                {
                    if (value != null)
                        OreSpawn = value;
                }
            }

            public bool ShouldSerializeLegacyOreSpawn() => false;

            public OreSpawnData() { }
        }

        // Blacklist.json structure
        private class StoredBlacklist
        {
            public HashSet<string> ItemList = new HashSet<string>();

            public StoredBlacklist() { }
        }

        private class LootGroupsData
        {
            [JsonProperty("Loot Groups", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, LootProfile> LootGroups = new Dictionary<string, LootProfile>
            {
                ["example_group"] = new LootProfile(new Dictionary<string, LootProfile.LootRNG> { ["lmg.m249"] = new LootProfile.LootRNG(10, new LootEntry(1, 2)) }, false)
            };

            public LootGroupsData() { }

            public static void ValidateGroups(LootGroupsData? Data)
            {
                ItemManager.Initialize();

                if (ItemManager.itemDictionaryByName is null)
                {
                    Log("Error: Failed to initialize ItemDictionary. Unloading");
                    _instance.Server.Command($"o.unload BetterLoot");
                    return;
                }

                if (Data is null || Data.LootGroups is null)
                {
                    Log($"Error: Invalid data was provided to the {nameof(LootGroupsData)} validator!");
                    return;
                }

                // Attempt to create an example group in the LootGroups file
                TryCreateExampleGroup();

                foreach ((string profileName, LootProfile? profileData) in Data.LootGroups)
                {
                    Log($"Validating LootGroup: \"{profileName}\"");

                    // NRE Data Check
                    if (profileData.ItemList is null)
                    {
                        Log("- Error: Profile item list is null. Skipping...");
                        continue;
                    }

                    // Ensure items are valid
                    List<string> invalidItemPrefabs = Pool.Get<List<string>>();
                    invalidItemPrefabs.AddRange(profileData.ItemList.Keys.Where(key => !ItemManager.itemDictionaryByName.ContainsKey(UniqueTagREGEX.Replace(key.ToLower(), string.Empty))));

                    if (profileData.ItemList.RemoveAll(invalidItemPrefabs.Contains) is int removeCount && removeCount > 0)
                        Log($"Error - Removing {removeCount} invalid entries. Please check item names that were not found in the games item dictionary: ({string.Join(", ", invalidItemPrefabs)})");

                    Pool.FreeUnmanaged(ref invalidItemPrefabs);

                    string extraReason = string.Empty;
                    if (profileData.ItemList.Count > 0) // Ensure we still have items
                    {
                        // Balance loot percentages
                        if (_config.LootGroupsConfig.EnableProbabilityBalancing)
                        {
                            double GetSum() => profileData.ItemList.Sum(x => x.Value.Probability);
                            double Round(double x) => Math.Round(x, 2);

                            const double target = 100;
                            double sum = GetSum();

                            if (Math.Abs(target - sum) > 1e-3)
                            {
                                Log($"- Profile probability sum ({sum}) != 100. Balancing profile!");

                                double _ratio = target / sum;

                                // Set first key as largest by default for empty string edgecase
                                string largestKey = profileData.ItemList.Keys.First();
                                double largestValue = profileData.ItemList[largestKey].Probability;

                                foreach (var item in profileData.ItemList)
                                {
                                    double probability = item.Value.Probability;
                                    if (probability > largestValue)
                                    {
                                        largestValue = probability;
                                        largestKey = item.Key;
                                    }

                                    item.Value.Probability = Round(probability * _ratio);
                                }

                                var largestEntry = profileData.ItemList[largestKey];
                                largestEntry.Probability = Round(largestEntry.Probability - Round(target - GetSum()));
                            }
                        }
                        else
                        {
                            extraReason = "Probability balance skipped.";
                        }
                    }
                    else
                    {
                        extraReason = "No remaining / valid items in profile, skipped.";
                    }

                    Log($"Profile \"{profileName}\" validation complete. {extraReason}");
                }
            }

            public static void TryCreateExampleGroup()
            {
                if (!_config.LootGroupsConfig.EnableExampleGroupCreation)
                    return;

                // Create a default group in the first item of the loot table for reference if none exists
                var firstLootTable = lootTables.LootTables.FirstOrDefault();
                if (!firstLootTable.IsDefault() && firstLootTable.Value.LootProfiles.Count == 0)
                {
                    firstLootTable.Value.LootProfiles.Add(new PrefabLoot.LootProfileImport("example_group", 30, false));
                    Log($"Added LootGroup Import example to \"{firstLootTable.Key}\"");
                    DataSystem.SaveLootTables();
                }
            }
        }

        private static class DataSystem
        {
            #region Public Methods
            #region Blacklist
            private const string BL_FN = "Blacklist";

            public static void LoadBlacklist()
                => LoadFile(BL_FN, (blacklistData) => CheckNull(ref blacklistData, ref storedBlacklist, blacklistData?.ItemList), ref storedBlacklist);

            public static void SaveBlacklist()
                => SaveFile(BL_FN, (blacklistData) => CheckNull(ref blacklistData, ref storedBlacklist, blacklistData?.ItemList), ref storedBlacklist);
            #endregion

            #region Loot Tables
            private const string LT_FN = "LootTables";

            public static void LoadLootTables()
                => LoadFile(LT_FN, (tableData) => CheckNull(ref tableData, ref lootTables, tableData?.LootTables), ref lootTables);

            public static void SaveLootTables()
                => SaveFile(LT_FN, (tableData) => CheckNull(ref tableData, ref lootTables, tableData?.LootTables), ref lootTables);
            #endregion

            #region Loot Groups
            private const string LG_FN = "LootGroups";

            public static void LoadLootGroups()
                => LoadFile(LG_FN, (groupsData) => CheckNull(ref groupsData, ref lootGroups), ref lootGroups, LootGroupsData.ValidateGroups);

            public static void SaveLootGroups()
                => SaveFile(LG_FN, (groupsData) => CheckNull(ref groupsData, ref lootGroups), ref lootGroups);
            #endregion

            #region Ore Spawn
            private const string OS_FN = "GatherRates";
            private const string OS_FN_LEGACY = "OreSpawn";

            private static string OreDataPath(string filename) => $"{nameof(BetterLoot)}\\{filename}";

            public static void LoadOreSpawn()
            {
                bool existedNew = Interface.Oxide.DataFileSystem.ExistsDatafile(OreDataPath(OS_FN));
                if (existedNew)
                {
                    LoadFile(OS_FN, (spawnData) => CheckNull(ref spawnData, ref oreSpawnData, spawnData?.OreSpawn), ref oreSpawnData);
                    return;
                }

                bool existedLegacy = Interface.Oxide.DataFileSystem.ExistsDatafile(OreDataPath(OS_FN_LEGACY));
                if (existedLegacy)
                {
                    LoadFile(OS_FN_LEGACY, (spawnData) => CheckNull(ref spawnData, ref oreSpawnData, spawnData?.OreSpawn), ref oreSpawnData);
                    SaveOreSpawn();
                    TryDeleteDataFile(OS_FN_LEGACY);
                    Log("Renamed oxide/data/BetterLoot/OreSpawn.json to GatherRates.json");
                    return;
                }

                var migrated = _instance?._legacyConfigOreSpawn ?? TryReadOreSpawnFromLootTablesFile();
                if (migrated is not null)
                {
                    oreSpawnData = new OreSpawnData { OreSpawn = migrated };
                    SaveOreSpawn();
                    Log("Migrated Ore Spawn settings into GatherRates.json");
                    return;
                }

                _gatherRatesNeedsVanillaFill = true;
                LoadFile(OS_FN, (spawnData) => CheckNull(ref spawnData, ref oreSpawnData, spawnData?.OreSpawn), ref oreSpawnData);
                Log("Created GatherRates.json. Vanilla gather defaults will be loaded from in-game prefabs.");
            }

            public static void SaveOreSpawn()
                => SaveFile(OS_FN, (spawnData) => CheckNull(ref spawnData, ref oreSpawnData, spawnData?.OreSpawn), ref oreSpawnData);

            private static void TryDeleteDataFile(string filename)
            {
                try
                {
                    string dataFilePath = Path.Combine(Interface.Oxide.DataFileSystem.Directory, $"{nameof(BetterLoot)}/{filename}.json");
                    if (File.Exists(dataFilePath))
                        File.Delete(dataFilePath);
                }
                catch (Exception ex)
                {
                    Log($"Could not remove leftover {filename}.json: {ex.Message}");
                }
            }

            private static OreSpawnConfiguration? TryReadOreSpawnFromLootTablesFile()
            {
                try
                {
                    string path = $"{nameof(BetterLoot)}\\{LT_FN}";
                    if (!Interface.Oxide.DataFileSystem.ExistsDatafile(path))
                        return null;

                    var raw = Interface.Oxide.DataFileSystem.ReadObject<JObject>(path);
                    var token = raw?["Gather Rates"] ?? raw?["Ore Spawn"] ?? raw?["Ore Spawn Configuration"];
                    return token?.Type == JTokenType.Object ? token.ToObject<OreSpawnConfiguration>() : null;
                }
                catch (Exception ex)
                {
                    Log($"Could not read leftover Ore Spawn settings from LootTables.json: {ex.Message}");
                    return null;
                }
            }
            #endregion
            #endregion

            #region DataFile Error Backup
            public static void BakDataFile(string filename, bool isRestoring = false, BasePlayer? msgPlayer = null)
            {
                // Move these to lang
                const string NoRestoreFileFound = "No backup file to restore.";
                const string NoMainFileFound = "No file found to move to backup, skipping file.";
                const string FileRestored = "Backup restored.";
                const string BackupTaken = "File backed up to bak file.";

                bool sendPlayer = msgPlayer is not null;
                string notifyMessage = string.Format(isRestoring ? "Restoring backup of {0}" : "Attempting to create backup of datafile {0}", $"{filename}.json");

                void Respond(string message)
                {
                    if (sendPlayer)
                        _instance?.SendMessage(msgPlayer, message);
                    else
                        Log(message);
                }
                
                Respond(notifyMessage);
                
                // Rename specified file to *.bak before regenerating a file in place of it
                string dataFilePath = Path.Combine(Interface.Oxide.DataFileSystem.Directory, $"{nameof(BetterLoot)}/{filename}.json");
                string bakFilePath = $"{dataFilePath}.bak";
                
                if (!isRestoring)
                {
                    if (!File.Exists(dataFilePath))
                    {
                        Respond(NoMainFileFound);
                        return;
                    }
                        
                    if (File.Exists(bakFilePath))
                        File.Delete(bakFilePath);
                    
                    File.Copy(dataFilePath, bakFilePath);
                } else
                {
                    if (!File.Exists(bakFilePath))
                    {
                        Respond(NoRestoreFileFound);
                        return;
                    }
                    
                    if (File.Exists(dataFilePath))
                        File.Delete(dataFilePath);
                    
                    File.Copy(bakFilePath, dataFilePath);
                }
                
                if (isRestoring)
                {
                    Respond(FileRestored);
                    _instance.InitLootSystem(true);
                } else
                {
                    Respond(BackupTaken);
                }
            }
            #endregion

            #region Save / Load Methods
            /// <summary>
            /// Load a data from a file within the plugin data directory.
            /// </summary>
            /// <typeparam name="T">The structure of the data being read from the file.</typeparam>
            /// <param name="fileName">The name of the file within the plugin data directory</param>
            /// <param name="validator">A custom data validator. Is nullable.</param>
            /// <param name="loadVar">The variable where the loaded data should be stored.</param>
            private static void LoadFile<T>(string fileName, Func<T, T>? validator, ref T loadVar, Action<T>? postLoadMethod = null) where T : class?
            {
                // If no validator was provided, set to check if instance is null, if it is create new instance.
                if (validator is null)
                    validator = (data) => data ?? Activator.CreateInstance<T>();

                try
                {
                    loadVar = validator(Interface.Oxide.DataFileSystem.ReadObject<T>($"{nameof(BetterLoot)}\\{fileName}"));
                    Log($"Loaded file \"{fileName}\" datafile successfully!");
                }
                catch (Exception e)
                {
                    Log($"ERROR: There was an issue loading your \"{fileName}.json\" datafile, a new one has been created.\n{e.Message}");

                    BakDataFile(fileName);
                    loadVar = Activator.CreateInstance<T>();
                }

                postLoadMethod?.Invoke(loadVar);

                SaveFile(fileName, validator, ref loadVar);
            }

            /// <summary>
            /// Save plugin data to the plugin data directory.
            /// </summary>
            /// <typeparam name="T">The type of the datafile structure</typeparam>
            /// <param name="fileName">The name of the datafile within the plugin data directory.</param>
            /// <param name="validator">A custom data validator. Is nullable.</param>
            /// <param name="saveVar">The variable where this data is currently stored.</param>
            private static void SaveFile<T>(string fileName, Func<T, T>? validator, ref T saveVar) where T : class?
            {
                if (validator is null)
                    validator = (data) => data ?? Activator.CreateInstance<T>();

                Interface.Oxide.DataFileSystem.WriteObject($"{nameof(BetterLoot)}\\{fileName}", validator(saveVar));

                Log($"Saved {fileName}.json");
            }
            #endregion

            #region Data Validator
            /// <summary>
            /// Checks if the provided datafile's data is null, if so create a new instance and optionally write it to file.
            /// </summary>
            /// <typeparam name="T">The type of the data structure that is being checked</typeparam>
            /// <param name="obj">The local instance of the data to check</param>
            /// <param name="target">The global instance of where the data is held</param>
            /// <param name="additional">Additional objects to check if null aside from arguement 'obj'</param>
            /// <returns>>Non null type of provided object type</returns>
            private static T CheckNull<T>(ref T? obj, ref T? target, params object?[] additional) where T : class?
            {
                if (obj is null || additional.Any(x => x is null))
                {
                    target = Activator.CreateInstance<T>();
                    obj = target;
                }

                return obj;
            }
            #endregion
        }

        private static void AttemptSendLootyLink()
        {
            Log("--------------------------------------------------------------------------");
            Log("Use the Looty Editor to easily edit and create loot tables for BetterLoot!");
            Log("Find it here -> https://looty.cc/betterloot");
            Log("--------------------------------------------------------------------------");
        }
        #endregion

        #region Culminative Probabilities Class
        public class ProbalisticRNG
        {
            [JsonIgnore]
            private List<double> _culminativeProbabilities = new List<double>();

            [JsonIgnore]
            public bool DoProbabilitiesExist
                => _culminativeProbabilities.Count > 0;

            public void UpdateProbabilities(IEnumerable<double> probabilities)
            {
                double _culminative = 0;
                foreach (int item in probabilities)
                {
                    _culminative += item;
                    _culminativeProbabilities.Add(_culminative);
                }
            }

            public int GetRandomIndex()
            {
                double randomSelect = RNG.NextDouble() * 1e2;
                int elementIndex = _culminativeProbabilities.BinarySearch(randomSelect);

                if (elementIndex < 0)
                    elementIndex = ~elementIndex;

                return elementIndex;
            }
        }
        #endregion

        #region Loot Classes
        public record ItemConvertInfo(Item Item, bool CanBeBp);
        
        private class VanillaLootItem
        {
            [JsonProperty("Shortname")]
            public string Shortname = string.Empty;

            [JsonProperty("Item Minimum")]
            public int Min = 1;

            [JsonProperty("Item Maximum")]
            public int Max = 1;

            [JsonProperty("Rarity")]
            public string Rarity = string.Empty;

            [JsonProperty("Allow Duplicates", NullValueHandling = NullValueHandling.Ignore)]
            public bool? AllowDuplicates;

            [JsonProperty("Skin ID (0 = default)", NullValueHandling = NullValueHandling.Ignore)]
            public ulong SkinId;

            [JsonProperty("Display Name (empty = none)", NullValueHandling = NullValueHandling.Ignore)]
            public string? DisplayName;

            [JsonProperty("Item Durability", NullValueHandling = NullValueHandling.Ignore)]
            public LootEntryDurability? DurabilitySettings;

            [JsonProperty("Item Properties", NullValueHandling = NullValueHandling.Ignore)]
            public ItemEntrySettings? ItemEntryModifications;

            [JsonIgnore]
            public LootEntrySettings? AppliedProperties;

            public bool ShouldSerializeAllowDuplicates() => AllowDuplicates == false;
            public bool ShouldSerializeSkinId() => SkinId != 0;
            public bool ShouldSerializeDisplayName() => !string.IsNullOrWhiteSpace(DisplayName);
            public bool ShouldSerializeDurabilitySettings() => DurabilitySettings is not null;
            public bool ShouldSerializeItemEntryModifications() => ItemEntryModifications is not null;
        }

        private class VanillaLootNode
        {
            [JsonProperty("Name", NullValueHandling = NullValueHandling.Ignore)]
            public string? Name;

            [JsonProperty("Weight")]
            public int Weight = 100;

            [JsonProperty("Extra Spawns")]
            public int ExtraSpawns;

            [JsonProperty("Items", NullValueHandling = NullValueHandling.Ignore)]
            public List<VanillaLootItem>? Items;

            [JsonProperty("SubSpawn", NullValueHandling = NullValueHandling.Ignore)]
            public List<VanillaLootNode>? SubSpawn;

            public bool ShouldSerializeName() => !string.IsNullOrWhiteSpace(Name);
            public bool ShouldSerializeExtraSpawns() => ExtraSpawns > 0;
        }

        private class VanillaLootSlot
        {
            [JsonProperty("Name", NullValueHandling = NullValueHandling.Ignore)]
            public string? Name;

            [JsonProperty("Number To Spawn")]
            public int NumberToSpawn = 1;

            [JsonProperty("Probability")]
            public float Probability = 1f;

            [JsonProperty("Loot")]
            public VanillaLootNode Loot = new();

            public bool ShouldSerializeName() => !string.IsNullOrWhiteSpace(Name);
        }

        /// <summary>
        /// Prefab Loot system will be contained in a list. This is the new loot class for loot containers that will
        /// allow the import of custom loot groups allowing for RNG on groups as well as individual items
        /// </summary>
        private class PrefabLoot : ProbalisticRNG
        {
            [JsonProperty("Is Prefab Enabled?", Order = 0)]
            public bool Enabled;

            [JsonProperty("Loot Profiles", Order = 1, ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<LootProfileImport> LootProfiles;

            [JsonProperty("Enable Loot Pool Locking", Order = 2)]
            public bool LootPoolLocking;

            [JsonProperty("Select ungrouped items ignoring rarity bias", Order = 3)]
            public bool IgnoreRarityBias;

            /// <summary>
            /// When true, fill this crate from Facepunch LootSpawn slots instead of MightyRNG / loot profiles.
            /// Guaranteed items, scrap, blacklist, and loot multiplier still apply.
            /// </summary>
            [JsonProperty("Use Vanilla Rust Loot RNG", Order = 4)]
            public bool UseVanillaLootRng;

            [JsonProperty("Vanilla Loot Slots", Order = 5, ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<VanillaLootSlot> VanillaLootSlots = new();

            [JsonProperty("Guaranteed Items", Order = 6)]
            public Dictionary<string, LootEntrySettings> GuaranteedItems = new Dictionary<string, LootEntrySettings>();

            [JsonProperty("Ungrouped Items", Order = 7)]
            public Dictionary<string, LootEntry> UngroupedItems;

            [JsonProperty("Item Settings", Order = 8)]
            public ItemProperties ItemSettings;

            public PrefabLoot()
            {
                LootProfiles = new List<LootProfileImport>();
                UngroupedItems = new Dictionary<string, LootEntry>();
                VanillaLootSlots = new List<VanillaLootSlot>();
                ItemSettings = new ItemProperties();
            }

            internal class LootProfileImport
            {
                [JsonProperty("Group Enabled?")]
                public bool Enabled = true;
                [JsonProperty("Loot Profile Name")]
                public string LootProfileName = string.Empty;
                [JsonProperty("Loot Profile Probability (1% - 100%)")]
                public double LootProfileProbability;
                [JsonProperty("Max Items From Profile (0 = unlimited)")]
                public int MaxItemsFromProfile = 0;

                internal LootProfileImport() { }

                internal LootProfileImport(string LootProfileName, double LootProfileProbability, bool Enabled = true)
                {
                    this.LootProfileName = LootProfileName;
                    this.LootProfileProbability = LootProfileProbability;
                    this.Enabled = Enabled;
                }
            }

            internal class ItemProperties
            {
                [JsonProperty("Minimum Amount of Items")]
                public int ItemsMin;

                [JsonProperty("Maximum Amount of Items")]
                public int ItemsMax;

                [JsonProperty("Minimum Scrap Amount")]
                public int MinScrap;

                [JsonProperty("Maximum Scrap Amount")]
                public int MaxScrap;

                [JsonProperty("Minimum Blueprints")]
                public int MinBlueprints = 0;

                [JsonProperty("Maximum Blueprints")]
                public int MaxBlueprints = 0;

                [JsonProperty("Bonus Items Contribute to Item Count")]
                public bool bonusItemsAddCount;

                [JsonProperty("Guaranteed Items Contribute to Item Count")]
                public bool guaranteedItemsAddCount;

                internal ItemProperties() { }

                #region v4.0.6  / v4.2.1 Configuration Migration

                [JsonProperty("Scrap Amount", NullValueHandling = NullValueHandling.Ignore)]
                private int? LegacyScrap { get; set; }

                [JsonProperty("Max Blueprints", NullValueHandling = NullValueHandling.Ignore)]
                private int? LegacyMaxBPs { get; set; }

                [OnDeserialized]
                private void OnDeserialized(StreamingContext _)
                {
                    if (LegacyScrap.HasValue)
                    {
                        if (MaxScrap == 0)
                        {
                            MaxScrap = LegacyScrap.Value;
                            MinScrap = LegacyScrap.Value;
                        }
                    }

                    if (LegacyMaxBPs.HasValue && MaxBlueprints == 0)
                    {
                        MaxBlueprints = LegacyMaxBPs.Value;
                    }

                    LegacyScrap = null;
                    LegacyMaxBPs = null;
                }

                [OnSerializing]
                private void OnSerializing(StreamingContext _) => LegacyScrap = null; // force null, no omit
                #endregion
            }

            #region Random Profile Selector
            [JsonIgnore]
            private List<int> _enabledProfiles = new (); // Map position to index
            [JsonIgnore]
            private static readonly List<LootProfileImport> _profileImportScratch = new List<LootProfileImport>(16);
            [JsonIgnore]
            private static readonly List<double> _cumulativeScratch = new List<double>(16);

            internal bool IsProfileAtItemLimit(string? profileName, Dictionary<string, int>? itemsTakenFromProfile)
            {
                if (string.IsNullOrEmpty(profileName) || LootProfiles is null || itemsTakenFromProfile is null)
                    return false;

                for (int i = 0; i < LootProfiles.Count; i++)
                {
                    LootProfileImport import = LootProfiles[i];
                    if (!import.Enabled || import.LootProfileName != profileName)
                        continue;

                    if (import.MaxItemsFromProfile <= 0)
                        return false;

                    itemsTakenFromProfile.TryGetValue(profileName, out int taken);
                    return taken >= import.MaxItemsFromProfile;
                }

                return false;
            }

            [JsonIgnore]
            private bool? _cachedHasProfileItemLimits;

            internal bool HasEnabledProfileItemLimits()
            {
                if (_cachedHasProfileItemLimits.HasValue)
                    return _cachedHasProfileItemLimits.Value;

                if (LootProfiles is null)
                    return (_cachedHasProfileItemLimits = false).Value;

                for (int i = 0; i < LootProfiles.Count; i++)
                {
                    LootProfileImport import = LootProfiles[i];
                    if (import.Enabled && import.MaxItemsFromProfile > 0)
                        return (_cachedHasProfileItemLimits = true).Value;
                }

                return (_cachedHasProfileItemLimits = false).Value;
            }

            private LootProfile? ResolveImportedProfile(LootProfileImport importProfile, string? tableReference, out string? selectedProfileName)
            {
                selectedProfileName = importProfile.LootProfileName;

                if (lootGroups is null || !lootGroups.LootGroups.TryGetValue(importProfile.LootProfileName, out LootProfile? profile))
                {
                    Log($"WARNING: prefab \"{tableReference}\" requested a loot group import with name \"{importProfile.LootProfileName}\". Group does not exist or is disabled in the LootGroups.json!");
                    selectedProfileName = null;
                    return null;
                }

                if (!profile.Enabled)
                {
                    selectedProfileName = null;
                    return null;
                }

                return profile;
            }

            private LootProfileImport? SelectRandomImport(List<LootProfileImport> imports)
            {
                if (imports.Count == 0)
                    return null;

                double cumulative = 0;
                var cumulatives = _cumulativeScratch;
                cumulatives.Clear();
                for (int i = 0; i < imports.Count; i++)
                {
                    cumulative += imports[i].LootProfileProbability;
                    cumulatives.Add(cumulative);
                }

                double randomSelect = RNG.NextDouble() * 1e2;
                int elementIndex = cumulatives.BinarySearch(randomSelect);
                if (elementIndex < 0)
                    elementIndex = ~elementIndex;

                if (elementIndex >= imports.Count)
                    return null;

                return imports[elementIndex];
            }

            /// <summary>
            /// Implemented binary search to select random loot import profile quickly. Returns null if should select from ungrouped items.
            /// </summary>
            /// <param name="tableReference">for reference to use in error message if there is a problem with the selection or config</param>
            /// <returns></returns>
            public LootProfile? GetRandomProfile(string? tableReference)
                => GetRandomProfile(tableReference, null, out _);

            public LootProfile? GetRandomProfile(string? tableReference, Dictionary<string, int>? itemsTakenFromProfile, out string? selectedProfileName)
            {
                selectedProfileName = null;

                if (LootProfiles is null)
                    return null;

                bool filterByItemLimit = itemsTakenFromProfile is not null && HasEnabledProfileItemLimits();
                if (filterByItemLimit)
                {
                    var remainingImports = _profileImportScratch;
                    remainingImports.Clear();
                    for (int i = 0; i < LootProfiles.Count; i++)
                    {
                        LootProfileImport import = LootProfiles[i];
                        if (!import.Enabled)
                            continue;

                        if (IsProfileAtItemLimit(import.LootProfileName, itemsTakenFromProfile))
                            continue;

                        remainingImports.Add(import);
                    }

                    LootProfileImport? limitedImport = SelectRandomImport(remainingImports);
                    if (limitedImport is null)
                        return null;

                    return ResolveImportedProfile(limitedImport, tableReference, out selectedProfileName);
                }

                if (!DoProbabilitiesExist)
                { // Updates and uses probalistic probability based off of only enabled profiles
                    List<LootProfileImport> enabledProfileImports = new List<LootProfileImport>();
                    for (int i = 0; i < LootProfiles.Count; i++)
                    {
                        var _profile = LootProfiles[i];
                        if (_profile.Enabled)
                        {
                            enabledProfileImports.Add(_profile);
                            this._enabledProfiles.Add(i);
                        }
                    }

                    UpdateProbabilities(enabledProfileImports.Select(x => x.LootProfileProbability));
                }

                int randomProfileIndex = GetRandomIndex();
                if (randomProfileIndex >= _enabledProfiles.Count)
                    return null;

                var importProfile = LootProfiles[_enabledProfiles[randomProfileIndex]];
                return ResolveImportedProfile(importProfile, tableReference, out selectedProfileName);
            }
            #endregion
        }

        /// <summary>
        /// LootProfile for containing all items that will be part of a certain profile
        /// Will be referenced by the specified profile name that the user creates within the LootGroups.json
        /// </summary>
        public class LootProfile : ProbalisticRNG
        {
            [JsonProperty("Enabled?")]
            public bool Enabled = true;

            [JsonProperty("Guaranteed Items")]
            public Dictionary<string, LootEntrySettings> GuaranteedItems = new ();

            [JsonProperty("Item List")]
            public Dictionary<string, LootRNG> ItemList;

            [JsonIgnore]
            private List<KeyValuePair<string, LootRNG>>? _orderedItems;

            public LootProfile(Dictionary<string, LootRNG> ItemList, bool Enabled = true)
            {
                this.ItemList = ItemList;
                this.Enabled = Enabled;
            }

            private List<KeyValuePair<string, LootRNG>> GetOrderedItems()
            {
                if (ItemList == null || ItemList.Count == 0)
                    return _orderedItems ??= new List<KeyValuePair<string, LootRNG>>();

                if (_orderedItems == null || _orderedItems.Count != ItemList.Count)
                {
                    _orderedItems = new List<KeyValuePair<string, LootRNG>>(ItemList.Count);
                    foreach (var kv in ItemList)
                        _orderedItems.Add(kv);
                }

                return _orderedItems;
            }

            public class LootRNG
            {
                [JsonProperty("Item Probability (1-100)")]
                public double Probability;

                [JsonProperty("Item Amount")]
                public LootEntry Amount;

                public LootRNG(double Probability, LootEntry Amount)
                {
                    this.Probability = Probability;
                    this.Amount = Amount;
                }
            }

            #region Probalistic Selector Methods
            /// <summary>
            /// Get a random item from this loot group based off of items probabilities
            /// </summary>
            public (ItemConvertInfo?, List<ItemConvertInfo>?) GetItem(HashSet<string> currentItemEntries)
            {
                var orderedItems = GetOrderedItems();
                int itemIndex = GetRandomIndex();

                // No item found, out of index
                if (itemIndex >= orderedItems.Count)
                    return (null, null);

                List<ItemConvertInfo>? bonusItems = null;
                var entry = orderedItems[itemIndex];

                if (!entry.Value.Amount.allowDuplicates && currentItemEntries.Contains(entry.Key))
                {
                    // Only item in the list and it's a duplicate, else all hope is lost :(
                    if (orderedItems.Count == 1)
                        return (null, null);

                    // Prefer the larger-index neighbour, fall back to smaller
                    if (itemIndex < orderedItems.Count - 1) itemIndex++;
                    else if (itemIndex > 0 && (entry.Value.Probability - orderedItems[itemIndex - 1].Value.Probability) <= _config.LootGroupsConfig.AllowedDuplicateNudgeDifference) itemIndex--;
                    else return (null, null);

                    // Set entry to the entry of the nudged index
                    entry = orderedItems[itemIndex];
                }

                entry.Value.Amount.CreateBonusItems(ref bonusItems);

                // Create Item
                string sanitizedName = StripUniqueTag(entry.Key);
                Item item = ItemManager.CreateByPartialName(sanitizedName, GetRNG(entry.Value.Amount.Min, entry.Value.Amount.Max), entry.Value.Amount.SkinId);
                
                // Apply custom properties
                entry.Value.Amount.ApplyAllProperties(item);

                if (item is null)
                    Log($"ERROR: item \"{entry.Key}\" could not be created! System returned null entry!");

                // Add for future duplicate checking.
                currentItemEntries.Add(entry.Key);

                return (new ItemConvertInfo(item, entry.Value.Amount.CanConvertToBlueprint ?? false), bonusItems);
            }
            #endregion
        }

        #region Loot Entry
        public class LootEntrySettings
        {
            [JsonProperty("Skin ID (0 = default)")]
            public ulong SkinId = 0;

            [JsonProperty("Display Name (empty = none)")]
            public string? DisplayName = string.Empty;

            [JsonProperty("Item Minimum")]
            public int Min;

            [JsonProperty("Item Maximum")]
            public int Max;

            [JsonProperty("Can Convert To Blueprint", NullValueHandling = NullValueHandling.Ignore)]
            public bool? CanConvertToBlueprint;

            [JsonProperty("Included in Vanilla Loot", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool IncludedInVanillaLoot;

            [JsonProperty("Item Durability", NullValueHandling = NullValueHandling.Ignore)]
            public LootEntryDurability? DurabilitySettings;

            // By default will not exist in item entries. System will add it during table scan.
            [JsonProperty("Item Properties", NullValueHandling = NullValueHandling.Ignore)]
            public ItemEntrySettings? ItemEntryModifications;

            [JsonProperty("Rarity", NullValueHandling = NullValueHandling.Ignore)]
            public string? Rarity;

            public void ApplyAllProperties(Item item)
            {
                ApplyAmmo(item);
                ApplyAttachments(item);
                
                if (!string.IsNullOrWhiteSpace(DisplayName))
                    item.name = DisplayName;
                
                if (DurabilitySettings is {})
                    item.ChangeConditionPercentage(GetRNG(DurabilitySettings.MinDurability, DurabilitySettings.MaxDurability));
                
                item.MarkDirty();
            }
            
            public void ApplyAttachments(Item item)
            {
                // No mods to apply
                if (!(ItemEntryModifications?.AttachmentSettings?.itemMods?.Count > 0))
                    return;

                // Recursively apply
                int total = Math.Clamp(GetRNG(ItemEntryModifications.AttachmentSettings.minModAmount, ItemEntryModifications.AttachmentSettings.maxModAmount), 1, ItemEntryModifications.maxMods);
                for (int i = 0; i < total; i++)
                {
                    for (int r = 0; r < 5; r++)
                    {
                        Item? itemMod = ItemEntryModifications?.GetRandomItemMod();
                        if (itemMod is null) // No item selected! This is ok.
                            break;

                        // Attempt to regenerate if there is a duplicate
                        if (itemMod.MoveToContainer(item.contents))
                            break;
                    }
                }
            }

            public void ApplyAmmo(Item item)
            {
                if (ItemEntryModifications?.AmmunitionSettings is not ItemEntrySettings.AmmoSettings ammoSettings || string.IsNullOrWhiteSpace(ammoSettings.AmmoItemShortname))
                    return;

                ItemDefinition? ammoDef = null;
                int ammoAmount = 1;
                bool hasExtendedMags = false;
                var contents = item.contents?.itemList;
                if (contents != null)
                {
                    for (int i = 0; i < contents.Count; i++)
                    {
                        if (contents[i]?.info?.shortname == "weapon.mod.extendedmags")
                        {
                            hasExtendedMags = true;
                            break;
                        }
                    }
                }

                if (ammoSettings.CanHoldMultipleAmmoUnits)
                {
                    ammoDef = ItemManager.FindDefinitionByPartialName(ammoSettings.AmmoItemShortname);
                    ammoAmount = Math.Clamp(GetRNG(ammoSettings.Min, ammoSettings.Max), 0, ammoSettings.MaxAmmo);

                    if (hasExtendedMags)
                        ammoAmount = (int)Math.Ceiling(ammoAmount * 1.25);
                }
                else if (GetRNG(0, 100) <= ammoSettings.Probability)
                {
                    ammoDef = ItemManager.FindDefinitionByPartialName(ammoSettings.AmmoItemShortname);
                }

                // No ammo defined
                if (ammoDef is null)
                    return;

                // Type check of how should apply
                var heldEntity = item.GetHeldEntity();
                if (heldEntity is BaseProjectile bp)
                {
                    bp.primaryMagazine.ammoType = ammoDef;
                    bp.primaryMagazine.contents = ammoAmount;
                    bp.SendNetworkUpdateImmediate();
                }
                else if (heldEntity is FlameThrower ft)
                {
                    // Vanilla rust spawns this with full fuel
                    // By default should only be lowgrade fuel but leave the definition open for allowing custom fuel types
                    ft.fuelType = ammoDef;
                    ft.ammo = ammoAmount;
                    ft.SendNetworkUpdateImmediate();
                }
                else if (heldEntity is LiquidWeapon lw)
                {
                    if (lw.GetContents() is Item _liquid)
                    {
                        _liquid.amount = ammoAmount;
                        _liquid.MarkDirty();
                    }
                    else
                    {
                        Item liquid = ItemManager.Create(ammoDef, ammoAmount);
                        liquid.MoveToContainer(item.contents);
                        item.MarkDirty();
                    }
                }
            }
        }

        public class LootEntryDurability
        {
            [JsonProperty("Minimum Durability")]
            public int MinDurability = 100;

            [JsonProperty("Maximum Durability")]
            public int MaxDurability = 100;
        }

        #region Attachment System
        /* 
            Added v4.1.1
        */
        public class ItemEntrySettings
        {
            // Should add deserialzier value to set min / max bounds (min <= max ALWAYS)
            public class AmmoSettings  // default state
            {
                [JsonIgnore]
                public bool CanHoldMultipleAmmoUnits = true; // Should be set to false when its a single shot weapon (no magazine. Also declares to not serialize the ItemMods list)

                [JsonProperty("Ammo Item Shortname")]
                public string AmmoItemShortname = string.Empty;  // Item shortname of ammo item

                [JsonIgnore]
                public int MaxAmmo; // Maximum ammo the item can take.

                // These should only be added in config if the weapon can hold more than 1 unit of ammunition. (e.g harpoon gun vs ak)
                [JsonProperty("Minimum Amount")]
                public int Min;
                [JsonProperty("Maximum Amount")]
                public int Max;

                // These should only exist if the weapon can only hold 1 unit of ammo
                [JsonProperty("Spawn Probability")]
                public double Probability;

                // conditional serialization
                public bool ShouldSerializeMin() => CanHoldMultipleAmmoUnits;
                public bool ShouldSerializeMax() => CanHoldMultipleAmmoUnits;
                public bool ShouldSerializeProbability() => !CanHoldMultipleAmmoUnits;
            }

            // A weapon always needs ammo
            [JsonProperty("Ammunition Settings")]
            public AmmoSettings AmmunitionSettings = new AmmoSettings();

            #region Attachment Related
            #region Internal
            [JsonIgnore]
            public int maxMods; // Maximum amount of mods the item can have.
            [JsonIgnore]
            private List<double> _culminativeProbabilities = new List<double>();
            #endregion
            public class ItemModEntry
            {
                [JsonProperty("Spawn Probability (0%-100%)")]  // Culminative probabilty with other items in list. => Implement loot groups like system
                public double Probability;
                [JsonProperty("Durability", NullValueHandling = NullValueHandling.Ignore)]
                public LootEntryDurability? Durability;
            }

            public class ItemModSettings
            {
                [JsonProperty("Minimum Mod Amount")]
                public int minModAmount = 1;
                [JsonProperty("Maximum Mod Amount")]
                public int maxModAmount = 1;
                [JsonProperty("Available Attachments")]
                public Dictionary<string, ItemModEntry> itemMods = new Dictionary<string, ItemModEntry>();
            }

            // Should only be on weapons that have slots for weapon mods
            [JsonProperty("Weapon Attachments", NullValueHandling = NullValueHandling.Ignore)]
            public ItemModSettings? AttachmentSettings; // Item mods / weapon attachments. Null by default. Item shortname: {probabilty, {durability}}

            public void BalanceItemModProbabilities()
            {
                if (!(AttachmentSettings?.itemMods?.Count > 0) || maxMods is 0)
                    return;

                AttachmentSettings.maxModAmount = Math.Clamp(AttachmentSettings.maxModAmount, 1, maxMods);

                double GetSum() => AttachmentSettings.itemMods.Sum(x => x.Value.Probability);
                double Round(double x) => Math.Round(x, 2);

                const double target = 100;
                double sum = GetSum(); // Initial sum

                if (sum > target) // Only balance if greater than target
                {
                    if (_config.Loot.EnableBonusItemsAutoBalanceLogging)
                        Log($"- Bonus items probability sum ({sum}) > 100. Balancing list!");

                    double _ratio = target / sum;

                    string largestKey = AttachmentSettings.itemMods.Keys.First();
                    double largestValue = AttachmentSettings.itemMods[largestKey].Probability;

                    foreach (var item in AttachmentSettings.itemMods)
                    {
                        double probability = item.Value.Probability;
                        if (probability > largestValue)
                        {
                            largestValue = probability;
                            largestKey = item.Key;
                        }

                        item.Value.Probability = Round(probability * _ratio);
                    }

                    var largestEntry = AttachmentSettings.itemMods[largestKey];
                    largestEntry.Probability = Round(largestEntry.Probability - Round(target - GetSum()));
                }
            }

            public void UpdateProbabilities()
            {
                double _culminative = 0;
                foreach (var item in AttachmentSettings.itemMods.Values)
                {
                    _culminative += item.Probability;
                    _culminativeProbabilities.Add(_culminative);
                }
            }

            // Get random attachment from list
            public Item? GetRandomItemMod()
            {
                if (_culminativeProbabilities.Count == 0)
                    UpdateProbabilities();

                double randomSelect = RNG.NextDouble() * 1e2;
                int itemIndex = _culminativeProbabilities.BinarySearch(randomSelect);

                if (itemIndex < 0)
                    itemIndex = ~itemIndex;

                // No item found
                if (itemIndex >= AttachmentSettings.itemMods.Count)
                    return null;

                var entry = AttachmentSettings.itemMods.ElementAt(itemIndex);

                // Create Item
                Item item = ItemManager.CreateByName(entry.Key);

                // Dont need to send network update, it will be sent with OnVirginSpawn()
                if (item is null)
                {
                    Log($"ERROR: item \"{entry.Key}\" could not be created! System returned null entry!");
                    return null;
                }

                if (entry.Value.Durability is not null)
                    item.ChangeConditionPercentage(GetRNG(entry.Value.Durability.MinDurability, entry.Value.Durability.MaxDurability));

                item.OnVirginSpawn();

                return item;
            }
            #endregion
        }
        #endregion

        public class LootEntry : LootEntrySettings
        {
            // Set at top level class to foce only having bonus items at this level and not nested levels.
            [JsonProperty("Bonus Items", Order = 7)] // Forcing field to bottom
            public Dictionary<string, LootEntrySettings> additionalItems = new Dictionary<string, LootEntrySettings>();
            [JsonProperty("Allow Duplicates")]
            public bool allowDuplicates = true;

            public LootEntry(int Min, int Max)
            {
                this.Min = Min;
                this.Max = Max;
            }

            public void CreateBonusItems(ref List<ItemConvertInfo>? bonusItems)
            {
                if (!(additionalItems?.Count > 0))
                    return;

                if (bonusItems is null)
                    bonusItems = new List<ItemConvertInfo>();

                foreach (var bonusItemEntry in additionalItems)
                {
                    var _bonusItemEntry = bonusItemEntry.Value;
                    Item bonusItem = ItemManager.CreateByName(StripUniqueTag(bonusItemEntry.Key), GetRNG(_bonusItemEntry.Min, _bonusItemEntry.Max) * _config.Loot.LootMultiplier, _bonusItemEntry.SkinId);
                    
                    if (bonusItem is null)
                        continue;
                    
                    _bonusItemEntry.ApplyAllProperties(bonusItem);

                    bonusItem.OnVirginSpawn();
                    bonusItems.Add(new ItemConvertInfo(bonusItem, _bonusItemEntry.CanConvertToBlueprint ?? false));
                }
            }
        }
        #endregion
        #endregion

        #region Util
        private static void Log(string msg, params object[] args) => _instance?.Puts(msg, args);
        private void SendMessage(BasePlayer player, string message, params object[] args) => Player.Reply(player, message, _config.ChatConfig.Prefix, _config.ChatConfig.MessageIcon, args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string StripUniqueTag(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOf('{') < 0)
                return name;
            return UniqueTagREGEX?.Replace(name, string.Empty) ?? name;
        }

        private void QueueOptionalApiHookRefresh()
        {
            if (_apiHookRefreshQueued)
                return;

            _apiHookRefreshQueued = true;
            NextTick(() =>
            {
                _apiHookRefreshQueued = false;
                if (_instance == null)
                    return;
                RefreshOptionalApiHooks();
                InvalidateGatherConflictCache();
            });
        }

        private void RefreshOptionalApiHooks()
        {
            _hasShouldBLPopulateContainer = HasExternalHook("ShouldBLPopulate_Container");
            _hasShouldBLPopulateNpc = HasExternalHook("ShouldBLPopulate_NPC");
            _hasShouldBLGather = HasExternalHook("ShouldBLGather");
            _hasShouldBLCollectablePickup = HasExternalHook("ShouldBLCollectablePickup");
        }

        private bool HasExternalHook(string hook)
        {
            try
            {
                foreach (var plugin in plugins.GetAll())
                {
                    if (plugin == null || plugin == this || !plugin.IsLoaded)
                        continue;

                    if (plugin.GetType().GetMethod(hook, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy) != null)
                        return true;
                }
            }
            catch
            {
                return true;
            }

            return false;
        }
        public static int GetRNG(int min, int max) => min == max ? min : UnityEngine.Random.Range(Math.Min(min, max), Math.Max(min, max) + 1);
        public static float GetRNG(float min, float max) => min == max ? min : UnityEngine.Random.Range(Math.Min(min, max), Math.Max(min, max));

        public static int GetWeightedRNG(int min, int max, double weight)
        {
            if (min >= max)
                return min;

            weight = Math.Clamp(weight, 0d, 1d);
            double value = RNG.NextDouble();
            double adjusted;

            if (weight < 0.5d)
            {
                double t = 1d - (weight / 0.5d);
                double exponent = 1d + t * 4d;
                adjusted = Math.Pow(value, exponent);
            }
            else if (weight > 0.5d)
            {
                double t = (weight - 0.5d) / 0.5d;
                double exponent = 1d + t * 4d;
                adjusted = Math.Pow(value, 1d / exponent);
            }
            else
            {
                adjusted = value;
            }

            return min + (int)Math.Floor(adjusted * (max - min + 1));
        }
        #endregion

        #region Oxide Loot Generation Hooks
        // Future compatability layer with CustomLootSpawnPlugin
        private bool IsCustomLootSpawnsContainer(LootFill container)
        {
            if (CustomLootSpawns == null)
                return false;

            var storageContainer = container.StorageContainer;
            if (storageContainer != null)
            {
                object result = CustomLootSpawns.Call("IsLootBox", storageContainer);
                if (result is bool b && b)
                    return true;
            }

            var parentBoat = container.GetComponent<RHIB>();
            if (parentBoat != null)
            {
                object result = CustomLootSpawns.Call("IsLootBox", parentBoat);
                if (result is bool b && b)
                    return true;
            }

            return false;
        }

        private object OnLootSpawn(LootFill container)
        {
            if (!Initialized || container == null || !_config.Generic.WatchedPrefabs.TryGetValue(container.name, out bool enabled) || !enabled || IsCustomLootSpawnsContainer(container))
                return null;

            if (PopulateContainer(container))
                return true;

            return null;
        }

        private object OnLootSpawn(LootContainer container)
        {
            if (!Initialized || container == null || !_config.Generic.WatchedPrefabs.TryGetValue(container.PrefabName, out bool enabled) || !enabled || (CustomLootSpawns != null && CustomLootSpawns.Call<bool>("IsLootBox", container)))
                return null;

            if (PopulateContainer(container))
                return true;

            return null;
        }

        LootableCorpse? OnCorpsePopulate(BaseEntity npcPlayer, LootableCorpse corpse)
        {
            if (corpse?.net != null && npcPlayer != null && !string.IsNullOrEmpty(npcPlayer.PrefabName))
                _corpseNpcPrefabs[corpse.net.ID.Value] = npcPlayer.PrefabName;

            return Initialized && npcPlayer != null && corpse != null && _config.Generic.WatchedPrefabs.TryGetValue(npcPlayer.PrefabName, out bool enabled) && enabled && PopulateContainer(npcPlayer.PrefabName, corpse, npcPlayer) ? corpse : null;
        }

        private object? OnItemUnwrap(Item item, BasePlayer player, ItemModUnwrap itemModUnwrap)
        {
            if (!Initialized || item?.info is null || player is null || itemModUnwrap is null)
                return null;

            string key = ToUnwrapKey(item.info);
            if (!_config.Generic.WatchedPrefabs.TryGetValue(key, out bool enabled) || !enabled ||
                !(lootTables?.LootTables?.TryGetValue(key, out PrefabLoot? profile) ?? false) || profile is null || !profile.Enabled)
                return null;

            if (!PopulateContainer(player.inventory.containerMain, key, false, player.inventory.containerBelt, player, out int deliveredItems) || deliveredItems == 0)
                return null;

            item.UseItem(1);

            if (itemModUnwrap.successEffect.isValid)
                Effect.server.Run(itemModUnwrap.successEffect.resourcePath, player.eyes.position, Vector3.zero, null, false);

            return true;
        }
        #endregion

        #region Loot Methods
        private int ItemWeight(double baseRarity, int index) => (int)(Math.Pow(baseRarity, 4 - index) * 1000);

        // OPTIMIZE
        private LootEntry GetAmounts(ItemAmount amount)
        {
            LootEntry options = new LootEntry(
                (int)amount.amount,
                amount is ItemAmountRanged ranged && ranged.maxAmount > 0 && ranged.maxAmount > amount.amount
                     ? (int)ranged.maxAmount
                     : (int)amount.amount
            );

            return options;
        }

        private bool IsSelectableLootEntry(LootSpawn.Entry entry)
            => entry.category is not null && entry.category.HasAnySpawns() &&
               (entry.restrictedEras is not { Length: > 0 } || Array.IndexOf(entry.restrictedEras, ConVar.Server.Era) >= 0) &&
               entry.weight + entry.RuntimeWeightBonus() > 0;

        private HashSet<string> GetGuaranteedLootItems(LootSpawn lootSpawn)
        {
            LootSpawn.Entry[] selectableEntries = lootSpawn.subSpawn?.Where(IsSelectableLootEntry).ToArray() ?? Array.Empty<LootSpawn.Entry>();
            if (selectableEntries.Length > 0)
            {
                HashSet<string>? commonItems = null;
                foreach (var entry in selectableEntries)
                {
                    HashSet<string> branchItems = GetGuaranteedLootItems(entry.category);
                    if (commonItems is null)
                        commonItems = branchItems;
                    else
                        commonItems.IntersectWith(branchItems);
                }

                return commonItems ?? new HashSet<string>();
            }

            return lootSpawn.items?
                .Where(x => x?.itemDef is not null && x.itemDef.IsAllowed(Rust.EraRestriction.Loot))
                .Select(x => x.itemDef.shortname + (x.itemDef.spawnAsBlueprint ? ".blueprint" : string.Empty))
                .ToHashSet() ?? new HashSet<string>();
        }

        private void GetLootSpawn(LootSpawn lootSpawn, Dictionary<string, LootEntry> items,
            Dictionary<string, LootEntrySettings> guaranteedItems, bool guaranteedCall)
        {
            LootSpawn.Entry[] selectableEntries = lootSpawn.subSpawn?.Where(IsSelectableLootEntry).ToArray() ?? Array.Empty<LootSpawn.Entry>();
            if (selectableEntries.Length > 0)
            {
                foreach (var entry in selectableEntries)
                    GetLootSpawn(entry.category, items, guaranteedItems, false);
            }
            else if (lootSpawn.items is { Length: > 0 })
            {
                foreach (var amount in lootSpawn.items.Where(x => x?.itemDef is not null && x.itemDef.IsAllowed(Rust.EraRestriction.Loot)))
                {
                    LootEntry options = GetAmounts(amount);
                    ItemDefinition itemDef = amount.itemDef;
                    string itemName = itemDef.shortname;
                    options.Rarity = itemDef.rarity.ToString();

                    if (itemDef.spawnAsBlueprint)
                        itemName += ".blueprint";
                    if (!items.ContainsKey(itemName) && !guaranteedItems.ContainsKey(itemName))
                    {
                        // Is fireable weapon type
                        GameObject? entMod = itemDef.GetComponent<ItemModEntity>()?.entityPrefab?.Get();
                        if (entMod is not null && (entMod.HasComponent<BaseProjectile>() || entMod.HasComponent<LiquidWeapon>()))
                        {
                            options.ItemEntryModifications = new ItemEntrySettings();

                            // Allowed to have item mods settings if can have attachments
                            if (itemDef.GetComponent<ItemModContainer>() is ItemModContainer imc && imc.capacity > 0 && imc.availableSlots.Count > 0)
                            {
                                options.ItemEntryModifications.AttachmentSettings = new ItemEntrySettings.ItemModSettings();
                            }
                        }

                        items.Add(itemName, options);
                    }
                }
            }

            if (!guaranteedCall)
                return;

            foreach (string itemName in GetGuaranteedLootItems(lootSpawn))
            {
                if (guaranteedItems.ContainsKey(itemName) || !items.TryGetValue(itemName, out LootEntry? options))
                    continue;

                // Thank you, Nick, for the suggestion for moving these types of items to the guaranteed items list :)
                items.Remove(itemName);
                guaranteedItems.Add(itemName, new LootEntrySettings
                {
                    SkinId = options.SkinId,
                    DisplayName = options.DisplayName,
                    Min = options.Min,
                    Max = options.Max,
                    CanConvertToBlueprint = options.CanConvertToBlueprint,
                    IncludedInVanillaLoot = true,
                    DurabilitySettings = options.DurabilitySettings,
                    ItemEntryModifications = options.ItemEntryModifications,
                    Rarity = options.Rarity
                });
            }
        }

        private VanillaLootNode? CaptureVanillaLootNode(LootSpawn? lootSpawn, int weight = 100, int extraSpawns = 0)
        {
            if (lootSpawn is null)
                return null;

            var node = new VanillaLootNode
            {
                Weight = Math.Max(0, weight),
                ExtraSpawns = Math.Max(0, extraSpawns)
            };
            if (!string.IsNullOrWhiteSpace(lootSpawn.name))
                node.Name = lootSpawn.name;

            LootSpawn.Entry[] selectableEntries = lootSpawn.subSpawn?.Where(IsSelectableLootEntry).ToArray() ?? Array.Empty<LootSpawn.Entry>();
            if (selectableEntries.Length > 0)
            {
                var children = new List<VanillaLootNode>(selectableEntries.Length);
                foreach (var entry in selectableEntries)
                {
                    var child = CaptureVanillaLootNode(
                        entry.category,
                        entry.weight + entry.RuntimeWeightBonus(),
                        GetLootSpawnEntryExtraSpawns(entry));
                    if (child is not null)
                        children.Add(child);
                }

                if (children.Count == 0)
                    return null;

                node.SubSpawn = children;
                return node;
            }

            if (lootSpawn.items is not { Length: > 0 })
                return null;

            var items = new List<VanillaLootItem>();
            foreach (var amount in lootSpawn.items.Where(x => x?.itemDef is not null && x.itemDef.IsAllowed(Rust.EraRestriction.Loot)))
            {
                LootEntry options = GetAmounts(amount);
                string itemName = amount.itemDef.shortname;
                if (amount.itemDef.spawnAsBlueprint)
                    itemName += ".blueprint";

                items.Add(new VanillaLootItem
                {
                    Shortname = itemName,
                    Min = options.Min,
                    Max = options.Max,
                    Rarity = amount.itemDef.rarity.ToString()
                });
            }

            if (items.Count == 0)
                return null;

            node.Items = items;
            return node;
        }

        private static int GetLootSpawnEntryExtraSpawns(LootSpawn.Entry entry)
        {
            var field = typeof(LootSpawn.Entry).GetField("extraSpawns");
            if (field is null)
                return 0;

            try
            {
                return Math.Max(0, Convert.ToInt32(field.GetValue(entry) ?? 0));
            }
            catch
            {
                return 0;
            }
        }

        private void StampVanillaLootNodeRarity(VanillaLootNode? node, ref bool modified)
        {
            if (node is null)
                return;

            if (node.Items is { Count: > 0 })
            {
                foreach (var item in node.Items)
                {
                    if (!string.IsNullOrEmpty(item.Rarity) || string.IsNullOrWhiteSpace(item.Shortname))
                        continue;

                    string lookup = UniqueTagREGEX.Replace(item.Shortname, string.Empty)
                        .Replace(".blueprint", string.Empty, StringComparison.OrdinalIgnoreCase);
                    if (ItemManager.FindItemDefinition(lookup) is not { } rarityDef)
                        continue;

                    item.Rarity = rarityDef.rarity.ToString();
                    modified = true;
                }
            }

            if (node.SubSpawn is not { Count: > 0 })
                return;

            foreach (var child in node.SubSpawn)
                StampVanillaLootNodeRarity(child, ref modified);
        }

        private List<VanillaLootSlot> CaptureVanillaLootSlots(LootContainer.LootSpawnSlot[]? slots)
        {
            var result = new List<VanillaLootSlot>();
            if (slots is not { Length: > 0 })
                return result;

            foreach (var slot in slots)
            {
                if (slot.eras is { Length: > 0 } && Array.IndexOf(slot.eras, ConVar.Server.Era) < 0)
                    continue;

                VanillaLootNode? loot = CaptureVanillaLootNode(slot.definition);
                if (loot is null)
                    continue;

                result.Add(new VanillaLootSlot
                {
                    NumberToSpawn = slot.numberToSpawn,
                    Probability = slot.probability,
                    Loot = loot
                });
            }

            return result;
        }

        private List<VanillaLootSlot> CaptureVanillaLootSlots(LootSpawn? definition, int numberToSpawn, float probability = 1f)
        {
            var result = new List<VanillaLootSlot>();
            VanillaLootNode? loot = CaptureVanillaLootNode(definition);
            if (loot is null)
                return result;

            result.Add(new VanillaLootSlot
            {
                NumberToSpawn = Math.Max(0, numberToSpawn),
                Probability = probability,
                Loot = loot
            });
            return result;
        }

        private List<VanillaLootSlot> CaptureVanillaLootSlotsFromPrefab(string prefab)
        {
            try
            {
                if (IsUnwrapKey(prefab) && FindUnwrapMod(prefab) is { revealList: not null } unwrap)
                {
                    int tries = Math.Max(1, Math.Min(unwrap.minTries, unwrap.maxTries));
                    return CaptureVanillaLootSlots(unwrap.revealList, tries);
                }

                if (IsMissionKey(prefab))
                    return new List<VanillaLootSlot>();

                GameObject? basePrefab = GameManager.server.FindPrefab(prefab);
                if (basePrefab is null)
                    return new List<VanillaLootSlot>();

                if (basePrefab.GetComponent<LootContainer>() is LootContainer loot)
                {
                    if (loot.LootSpawnSlots is { Length: > 0 })
                        return CaptureVanillaLootSlots(loot.LootSpawnSlots);
                    if (loot.lootDefinition is not null)
                        return CaptureVanillaLootSlots(loot.lootDefinition, loot.maxDefinitionsToSpawn);
                }

                if (basePrefab.GetComponent<LootFill>() is LootFill lootFill)
                {
                    if (lootFill.LootSpawnSlots is { Length: > 0 })
                        return CaptureVanillaLootSlots(lootFill.LootSpawnSlots);
                    if (lootFill.LootDefinition is not null)
                        return CaptureVanillaLootSlots(lootFill.LootDefinition, lootFill.MaxDefinitionsToSpawn);
                }

                if (basePrefab.GetComponent<global::HumanNPC>() is global::HumanNPC npc && npc.LootSpawnSlots is { Length: > 0 })
                    return CaptureVanillaLootSlots(npc.LootSpawnSlots);

#if OXIDE_PUBLICIZED
                if (basePrefab.TryGetComponent<FSMComponent>(out var fsm) &&
                    fsm is Scientist2FSM or Scientist2FSM_Heavy or Scientist2FSM_Shotgun)
                {
                    LootContainer.LootSpawnSlot[] spawnSlots = fsm switch
                    {
                        Scientist2FSM a => a.dead.LootSpawnSlots,
                        Scientist2FSM_Heavy b => b.dead.LootSpawnSlots,
                        Scientist2FSM_Shotgun c => c.dead.LootSpawnSlots,
                        _ => Array.Empty<LootContainer.LootSpawnSlot>()
                    };
                    if (spawnSlots.Length > 0)
                        return CaptureVanillaLootSlots(spawnSlots);
                }
#endif
                return new List<VanillaLootSlot>();
            }
            catch (Exception ex)
            {
                Log($"Failed capturing vanilla loot slots for {prefab}: {ex.Message}");
                return new List<VanillaLootSlot>();
            }
        }

        private bool TrySpawnSavedVanillaLoot(PrefabLoot con, ItemContainer dest)
        {
            if (con.VanillaLootSlots is not { Count: > 0 })
                return false;

            foreach (var slot in con.VanillaLootSlots)
            {
                for (int i = 0; i < slot.NumberToSpawn; i++)
                {
                    if (slot.Probability < 1f && UnityEngine.Random.Range(0f, 1f) > slot.Probability)
                        continue;

                    SpawnSavedVanillaNode(slot.Loot, dest);
                }
            }

            return true;
        }

        private void SpawnSavedVanillaNode(VanillaLootNode? node, ItemContainer dest)
        {
            if (node is null)
                return;

            int times = 1 + Math.Max(0, node.ExtraSpawns);
            for (int t = 0; t < times; t++)
            {
                if (node.SubSpawn is { Count: > 0 })
                {
                    int totalWeight = 0;
                    foreach (var child in node.SubSpawn)
                        totalWeight += Math.Max(0, child.Weight);

                    if (totalWeight <= 0)
                        continue;

                    int roll = UnityEngine.Random.Range(0, totalWeight);
                    int cumulative = 0;
                    VanillaLootNode? picked = null;
                    foreach (var child in node.SubSpawn)
                    {
                        cumulative += Math.Max(0, child.Weight);
                        if (roll < cumulative)
                        {
                            picked = child;
                            break;
                        }
                    }

                    if (picked is not null)
                        SpawnSavedVanillaNode(picked, dest);
                }
                else if (node.Items is { Count: > 0 })
                {
                    foreach (var item in node.Items)
                    {
                        if (string.IsNullOrWhiteSpace(item.Shortname))
                            continue;

                        string itemName = UniqueTagREGEX.Replace(item.Shortname, string.Empty);
                        bool spawnAsBlueprint = itemName.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase);
                        itemName = itemName.Replace(".blueprint", string.Empty, StringComparison.OrdinalIgnoreCase);

                        if (item.AllowDuplicates == false)
                        {
                            bool alreadyHas = false;
                            foreach (Item existing in dest.itemList)
                            {
                                if (existing?.info?.shortname == itemName)
                                {
                                    alreadyHas = true;
                                    break;
                                }
                            }
                            if (alreadyHas)
                                continue;
                        }

                        Item? created;
                        if (spawnAsBlueprint && BlueprintBaseDef is not null && ItemManager.FindItemDefinition(itemName) is { } blueprintTarget)
                        {
                            created = ItemManager.Create(BlueprintBaseDef);
                            if (created is not null)
                                created.blueprintTarget = blueprintTarget.itemid;
                        }
                        else
                        {
                            created = ItemManager.CreateByName(itemName, Math.Max(1, GetRNG(item.Min, item.Max)));
                        }

                        if (created is null)
                            continue;

                        if (item.SkinId != 0)
                            created.skin = item.SkinId;

                        item.AppliedProperties?.ApplyAllProperties(created);

                        created.OnVirginSpawn();
                        if (!created.MoveToContainer(dest))
                            created.DoRemove();
                    }
                }
            }
        }

        private void BuildWeaponInfoCache()
        {
            // Build details on all weapons and weapon attachments to build internal loot table flags and options.
            DurabilityItems = Pool.Get<List<string>>();
            WeaponInfoCache = Pool.Get<Dictionary<string, WI_Cache>>();
            WeaponModInfoCache = Pool.Get<Dictionary<string, ItemSlot>>();

            foreach (ItemDefinition itemDef in ItemManager.itemList)
            {
                if (itemDef.condition.enabled && itemDef.condition.max > 0)
                    DurabilityItems.Add(itemDef.shortname);

                // Below scans for weapons only, exit if not in category
                if (itemDef.category is not ItemCategory.Weapon)
                    continue;

                var entMod = itemDef.GetComponent<ItemModEntity>()?.entityPrefab?.Get();

                if (entMod is not null && entMod.HasComponent<ProjectileWeaponMod>())
                {
                    WeaponModInfoCache[itemDef.shortname] = itemDef.occupySlots;
                }
                else
                {
                    List<ItemSlot> modTypes = new List<ItemSlot>();
                    bool isLiquidWeapon = false;
                    int _maxStackSize = 0;
                    int maxMods = 0;
                    int maxAmmo = 0;

                    if (itemDef.GetComponent<ItemModContainer>() is ItemModContainer container)
                    { // Can take item mods
                        // Max mods
                        maxMods = container.capacity;

                        // Mod types
                        modTypes = container.availableSlots;

                        // Liquid weapon check
                        if (container.onlyAllowedContents is ItemContainer.ContentsType.Liquid)
                        {
                            isLiquidWeapon = true;
                            _maxStackSize = container.maxStackSize;
                        }
                    }

                    // Max ammo
                    if (entMod is not null && (entMod.HasComponent<BaseProjectile>() || entMod.HasComponent<FlameThrower>() || entMod.HasComponent<LiquidWeapon>()))
                    {
                        maxAmmo = _maxStackSize; // only applies to liquid weapon

                        if (entMod.GetComponent<BaseProjectile>() is BaseProjectile bp && bp?.primaryMagazine is BaseProjectile.Magazine magazine)
                        {
                            // Safe to reference the built in size as opposed to the capacity, Built in size seems to always have a value.
                            maxAmmo = magazine.definition.builtInSize;
                        }
                        else if (entMod.GetComponent<FlameThrower>() is FlameThrower ft)
                        {
                            maxAmmo = ft.maxAmmo;
                        }

                        ItemSlot totalFlags = modTypes.Count > 0 ? modTypes[0] : ItemSlot.None;
                        for (int i = 1; i < modTypes.Count; i++)
                            totalFlags |= modTypes[i];

                        WeaponInfoCache[itemDef.shortname] = new WI_Cache(isLiquidWeapon, maxMods, maxAmmo, totalFlags);
                    }
                }
            }
        }
        
        private PrefabLoot CreatePrefabLoot(bool enabled = false)
        {
            return new PrefabLoot
            {
                Enabled = enabled,
                UseVanillaLootRng = _config.Loot.DefaultToVanillaLootRng
            };
        }

        private void LoadAllContainers()
        {
            var nullTablePrefabs = Pool.Get<List<string>>();
            bool modifiedLootTables = false;

            const string bradleyCrate = "bradley_crate";
            const string heliCrate = "heli_crate";

            // OPTIMIZE
            foreach (var lootPrefabEntry in _config.Generic.WatchedPrefabs) // Attempt to generate from prefab path and remove any invalid loot
            {
                string lootPrefab = lootPrefabEntry.Key;
                bool shouldBeEnabled = lootPrefabEntry.Value; // if false it will run and silent fail where it should generate loot, this is to ensure it is valid to the point where it needs to be unless it should be removed from the Watched Prefabs list.

                // If prefab loot table is not currently present loaded from LootTables.json, generate it.
                if (!lootTables.LootTables.ContainsKey(lootPrefab))
                {
                    if (IsUnwrapKey(lootPrefab))
                    {
                        if (FindUnwrapMod(lootPrefab) is not { revealList: not null } unwrap)
                        {
                            nullTablePrefabs.Add(lootPrefab);
                            continue;
                        }

                        var container = CreatePrefabLoot(true);
                        var itemList = new Dictionary<string, LootEntry>();
                        var guaranteedItems = new Dictionary<string, LootEntrySettings>();
                        int minTries = Math.Max(1, Math.Min(unwrap.minTries, unwrap.maxTries));
                        int maxTries = Math.Max(minTries, Math.Max(unwrap.minTries, unwrap.maxTries));

                        GetLootSpawn(unwrap.revealList, itemList, guaranteedItems, unwrap.minTries > 0);
                        foreach (LootEntrySettings guaranteedItem in guaranteedItems.Values)
                        {
                            guaranteedItem.Min *= minTries;
                            guaranteedItem.Max *= maxTries;
                        }

                        container.ItemSettings.ItemsMin = minTries;
                        container.ItemSettings.ItemsMax = maxTries;
                        container.ItemSettings.MaxBlueprints = 1;
                        // Each native unwrap attempt can yield both a guaranteed item and a random branch item.
                        container.ItemSettings.guaranteedItemsAddCount = false;
                        container.UngroupedItems = itemList;
                        container.GuaranteedItems = guaranteedItems;
                        container.VanillaLootSlots = CaptureVanillaLootSlots(unwrap.revealList, minTries);

                        lootTables.LootTables.Add(lootPrefab, container);
                        modifiedLootTables = true;
                        continue;
                    }

                    if (IsMissionKey(lootPrefab))
                    {
                        lootTables.LootTables.Add(lootPrefab, CreatePrefabLoot(shouldBeEnabled));
                        modifiedLootTables = true;
                        continue;
                    }

                    var basePrefab = GameManager.server.FindPrefab(lootPrefab);

                    if (basePrefab is null)
                    {
                        nullTablePrefabs.Add(lootPrefab);
                        continue;
                    }

                    #region Loot Helper Functions
                    void PopulateNPCType(LootContainer.LootSpawnSlot[] spawnSlots)
                    {
                        var container = CreatePrefabLoot();

                        container.Enabled = !lootPrefab.Contains(bradleyCrate, CompareOptions.Ordinal) && !lootPrefab.Contains(heliCrate, CompareOptions.Ordinal);
                        container.ItemSettings.MaxScrap = 0;

                        var slotItemCount = 0;
                        var itemList = new Dictionary<string, LootEntry>();
                        var guaranteedItems = new Dictionary<string, LootEntrySettings>();

                        foreach (var slot in spawnSlots)
                        {
                            GetLootSpawn(slot.definition, itemList, guaranteedItems,
                                slot.numberToSpawn > 0 && slot.probability >= 1f && string.IsNullOrEmpty(slot.onlyWithLoadoutNamed));
                            slotItemCount += slot.numberToSpawn;
                        }

                        container.ItemSettings.ItemsMin = container.ItemSettings.ItemsMax = slotItemCount;
                        container.ItemSettings.MaxBlueprints = 1;
                        container.ItemSettings.guaranteedItemsAddCount = true;
                        container.UngroupedItems = itemList;
                        container.GuaranteedItems = guaranteedItems;
                        container.VanillaLootSlots = CaptureVanillaLootSlots(spawnSlots);

                        lootTables.LootTables.Add(lootPrefab, container);
                        modifiedLootTables = true;
                    }

                    int CountSlots(LootContainer.LootSpawnSlot[] lootSpawnSlots)
                    {
                        int slots = 0;
                        for (int i = 0; i < lootSpawnSlots.Length; i++)
                            slots += lootSpawnSlots[i].numberToSpawn;

                        return slots;
                    }
                    #endregion
                    
                    if (basePrefab.GetComponent<global::HumanNPC>() is global::HumanNPC npc)
                    { // NPC Version 1
                        if (shouldBeEnabled)
                            PopulateNPCType(npc.LootSpawnSlots);
                    }
#if OXIDE_PUBLICIZED
                    else if (basePrefab.TryGetComponent<FSMComponent>(out var fsm) && fsm is Scientist2FSM or Scientist2FSM_Heavy or Scientist2FSM_Shotgun)
                    {  // NPC Version 2
                        // Oxide publicizer takes care of this private field.
                        if (shouldBeEnabled) {
                            PopulateNPCType(fsm switch
                            {
                                Scientist2FSM a => a.dead.LootSpawnSlots,
                                Scientist2FSM_Heavy b => b.dead.LootSpawnSlots,
                                Scientist2FSM_Shotgun c => c.dead.LootSpawnSlots
                            });
                        }
                    }
#endif
                    else if (basePrefab.GetComponent<LootFill>() is LootFill lf) // Deep Sea Patrol Boats
                    {
                        var container = CreatePrefabLoot(true);

                        int slots = lf.LootSpawnSlots.Length > 0 ? CountSlots(lf.LootSpawnSlots) : lf.MaxDefinitionsToSpawn;

                        container.ItemSettings.ItemsMin = container.ItemSettings.ItemsMax = slots;
                        container.ItemSettings.MaxBlueprints = 1;
                        container.ItemSettings.guaranteedItemsAddCount = true;

                        var itemList = new Dictionary<string, LootEntry>();
                        var guaranteedItems = new Dictionary<string, LootEntrySettings>();
                        
                        if (lf.LootSpawnSlots.Length > 0)
                        {
                            LootContainer.LootSpawnSlot[] lootSpawnSlots = lf.LootSpawnSlots;
                            foreach (var lootSpawnSlot in lootSpawnSlots)
                            {
                                if (lootSpawnSlot.eras is { Length: > 0 } && Array.IndexOf(lootSpawnSlot.eras, ConVar.Server.Era) < 0)
                                    continue;

                                GetLootSpawn(lootSpawnSlot.definition, itemList, guaranteedItems,
                                    lootSpawnSlot.numberToSpawn > 0 && lootSpawnSlot.probability >= 1f);
                            }
                        } else if (lf.LootDefinition is not null)
                            GetLootSpawn(lf.LootDefinition, itemList, guaranteedItems, lf.MaxDefinitionsToSpawn > 0);

                        // Default items
                        container.UngroupedItems = itemList;
                        container.GuaranteedItems = guaranteedItems;
                        container.VanillaLootSlots = lf.LootSpawnSlots.Length > 0
                            ? CaptureVanillaLootSlots(lf.LootSpawnSlots)
                            : CaptureVanillaLootSlots(lf.LootDefinition, lf.MaxDefinitionsToSpawn);

                        lootTables.LootTables.Add(lootPrefab, container);
                        modifiedLootTables = true;
                    }
                    else
                    { // is not npc
                        var loot = basePrefab.GetComponent<LootContainer>();

                        if (loot is null)
                        {
                            nullTablePrefabs.Add(lootPrefab);
                            continue;
                        }

                        var container = CreatePrefabLoot();

                        container.Enabled = !lootPrefab.Contains(bradleyCrate, CompareOptions.Ordinal) &&
                                            !lootPrefab.Contains(heliCrate, CompareOptions.Ordinal);
                        container.ItemSettings.MinScrap = loot.scrapAmount;
                        container.ItemSettings.MaxScrap = loot.scrapAmount;

                        int slots = loot.LootSpawnSlots.Length > 0 ? CountSlots(loot.LootSpawnSlots) : loot.maxDefinitionsToSpawn;
                        
                        container.ItemSettings.ItemsMin = container.ItemSettings.ItemsMax = slots;
                        container.ItemSettings.MaxBlueprints = 1;
                        container.ItemSettings.guaranteedItemsAddCount = true;

                        var itemList = new Dictionary<string, LootEntry>();
                        var guaranteedItems = new Dictionary<string, LootEntrySettings>();
                        
                        if (loot.LootSpawnSlots.Length > 0)
                        {
                            LootContainer.LootSpawnSlot[] lootSpawnSlots = loot.LootSpawnSlots;
                            foreach (var lootSpawnSlot in lootSpawnSlots)
                            {
                                if (lootSpawnSlot.eras is { Length: > 0 } && Array.IndexOf(lootSpawnSlot.eras, ConVar.Server.Era) < 0)
                                    continue;

                                GetLootSpawn(lootSpawnSlot.definition, itemList, guaranteedItems,
                                    lootSpawnSlot.numberToSpawn > 0 && lootSpawnSlot.probability >= 1f);
                            }
                        }
                        else if (loot.lootDefinition is not null)
                        {
                            GetLootSpawn(loot.lootDefinition, itemList, guaranteedItems, loot.maxDefinitionsToSpawn > 0);
                        }

                        // Default items
                        container.UngroupedItems = itemList;
                        container.GuaranteedItems = guaranteedItems;
                        container.VanillaLootSlots = loot.LootSpawnSlots.Length > 0
                            ? CaptureVanillaLootSlots(loot.LootSpawnSlots)
                            : CaptureVanillaLootSlots(loot.lootDefinition, loot.maxDefinitionsToSpawn);

                        lootTables.LootTables.Add(lootPrefab, container);
                        modifiedLootTables = true;
                    }
                }
            }

            foreach (var existing in lootTables.LootTables)
            {
                if (existing.Value.VanillaLootSlots is not { Count: > 0 })
                {
                    List<VanillaLootSlot> captured = CaptureVanillaLootSlotsFromPrefab(existing.Key);
                    if (captured.Count == 0)
                        continue;

                    existing.Value.VanillaLootSlots = captured;
                    modifiedLootTables = true;
                    continue;
                }

                bool rarityUpdated = false;
                foreach (var slot in existing.Value.VanillaLootSlots)
                    StampVanillaLootNodeRarity(slot.Loot, ref rarityUpdated);

                if (rarityUpdated)
                    modifiedLootTables = true;
            }

            int missingVanillaTrees = lootTables.LootTables.Count(x =>
                !IsUnwrapKey(x.Key) && !IsMissionKey(x.Key) && x.Value.VanillaLootSlots is not { Count: > 0 });
            if (missingVanillaTrees > 0)
                Log($"Warning: {missingVanillaTrees} loot tables have no Vanilla Loot Slots (Facepunch tree empty or prefab not loaded).");

            if (nullTablePrefabs.Count > 0 && _config.Generic.WatchedPrefabs.RemoveAll(nullTablePrefabs.Contains) is int missing && missing > 0)
            {
                if (NewConfigGenerated)
                    Puts($"Removed {missing} invalid / unloaded prefabs from watch list:\n{string.Join(", \n", nullTablePrefabs)}");
                SaveConfig();
            }

            Pool.FreeUnmanaged(ref nullTablePrefabs);

            // Write Changes
            if (modifiedLootTables)
            {
                // Try to create an example loot group within the LootTables.json file for user reference :)
                LootGroupsData.TryCreateExampleGroup();
                DataSystem.SaveLootTables();
            }
            
            ItemManagerItemNamesList.Capacity = ItemManager.itemList.Count;
            ItemManagerItemNamesList = ItemManager.itemList.Select(id => id.shortname).ToList();
            modifiedLootTables = false;

            void scanEntry(string itemKey, LootEntrySettings itemEntry, string lootTableKey, ref bool modificationFlag)
            {
                var defName = UniqueTagREGEX.Replace(itemKey, string.Empty);
                
                // Warning
                if (!ItemManagerItemNamesList.Contains(defName, StringComparer.OrdinalIgnoreCase))
                {
                    Puts($"Error: Item named \"{defName}\" in table of \"{lootTableKey}\" doesnt exist. Item will not generate! Please check naming!");
                    return;
                }

                if (!itemKey.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase))
                {
                    // Validate the blueprint conversion property
                    bool canBeBlueprint = ItemManager.FindItemDefinition(defName) is { } def && (def.Blueprint?.isResearchable ?? false);
                    if (itemEntry.CanConvertToBlueprint is null)
                    {
                        if (canBeBlueprint)
                            itemEntry.CanConvertToBlueprint = true;
                    }
                    else if (!canBeBlueprint)
                        itemEntry.CanConvertToBlueprint = null; // Entry should be allowed to have blueprint in first place.
                }

                string rarityLookup = defName.Replace(".blueprint", string.Empty, StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrEmpty(itemEntry.Rarity) && ItemManager.FindItemDefinition(rarityLookup) is { } rarityDef)
                {
                    itemEntry.Rarity = rarityDef.rarity.ToString();
                    modificationFlag = true;
                }
                
                // Check if it needs durability
                if (DurabilityItems.Contains(itemKey))
                {
                    if (itemEntry.DurabilitySettings is null)
                        itemEntry.DurabilitySettings = new();
                }
                else if (itemEntry.DurabilitySettings is not null)
                    itemEntry.DurabilitySettings = null;

                scanWeaponEntry(itemKey, defName, itemEntry, lootTableKey, ref modificationFlag);
            }

            void scanWeaponEntry(string itemKey, string defName, LootEntrySettings itemEntry, string lootTableKey,
                ref bool modificationFlag, bool addAttachments = true)
            {
                // Check if we need to scan the current entry.
                if (!(WeaponInfoCache?.ContainsKey(defName) ?? false))
                {
                    // Check if a properties object is set but shouldnt be
                    if (itemEntry.ItemEntryModifications != null)
                    {
                        // This should only happen from a misconfiguration, the plugin should never place an entry in a invalid weapon table.
                        Log($"{itemKey} from table: \"{lootTableKey}\" should not have a modification object entry, it does not support this!");

                        itemEntry.ItemEntryModifications = null;
                        modificationFlag = true;
                    }

                    return;
                }

                // Modify the flags from cache.
                WI_Cache WIEntry = WeaponInfoCache[defName]; // Cached entry with static definition data
                itemEntry.ItemEntryModifications ??= new ItemEntrySettings(); // if does not already exist create it

                // Build entry flags
                itemEntry.ItemEntryModifications.AmmunitionSettings.MaxAmmo = WIEntry.MaxAmmo;
                itemEntry.ItemEntryModifications.AmmunitionSettings.CanHoldMultipleAmmoUnits = WIEntry.MaxAmmo > 1;
                itemEntry.ItemEntryModifications.maxMods = WIEntry.MaxMods;

                // Balance profile
                itemEntry.ItemEntryModifications.BalanceItemModProbabilities();

                // Scan item mods
                if (WIEntry.MaxMods > 0 && (itemEntry.ItemEntryModifications.AttachmentSettings ??= addAttachments && !WIEntry.IsLiquidWeapon ? new() : null) is ItemEntrySettings.ItemModSettings itemMods)
                {
                    List<string> invalidMods = Pool.Get<List<string>>();
                    foreach (var modEntry in itemMods.itemMods)
                    {
                        // Remove if invalid
                        if (!WeaponModInfoCache.TryGetValue(modEntry.Key, out ItemSlot modSlotType))
                        {
                            Log($"Invalid weapon mod \"{modEntry.Key}\" removed from table: {lootTableKey}");
                            invalidMods.Add(modEntry.Key);
                            continue;
                        }

                        // Check if needs durability
                        if (modSlotType is ItemSlot.Barrel && modEntry.Value.Durability is not null)
                            modEntry.Value.Durability = null;

                        // Check if incompatible
                        if (!WIEntry.ModTypes.HasFlag(modSlotType))
                        {
                            Log($"Removed incompatible attachment \"{modEntry.Key}\" assigned to item {itemKey} in table: {lootTableKey}");
                            invalidMods.Add(modEntry.Key);
                        }
                    }

                    if (invalidMods.Count > 0)
                        itemEntry.ItemEntryModifications.AttachmentSettings.itemMods.RemoveAll(x => invalidMods.Contains(x));

                    Pool.FreeUnmanaged(ref invalidMods);

                }
                else if (WIEntry.MaxMods is 0 && itemEntry?.ItemEntryModifications?.AttachmentSettings is not null)
                { // if has mods property but shouldnt.
                    itemEntry.ItemEntryModifications.AttachmentSettings = null;
                }

                modificationFlag = true;
            }
            
            void scanVanillaNode(VanillaLootNode? node, string lootTableKey, ref bool modificationFlag)
            {
                if (node is null)
                    return;

                if (node.Items is not null)
                {
                    foreach (var item in node.Items)
                    {
                        item.AppliedProperties = null;
                        if (string.IsNullOrWhiteSpace(item.Shortname) ||
                            (string.IsNullOrWhiteSpace(item.DisplayName) && item.DurabilitySettings is null && item.ItemEntryModifications is null))
                            continue;

                        var properties = new LootEntrySettings
                        {
                            DisplayName = item.DisplayName,
                            DurabilitySettings = item.DurabilitySettings,
                            ItemEntryModifications = item.ItemEntryModifications
                        };

                        if (properties.ItemEntryModifications is not null)
                        {
                            string defName = UniqueTagREGEX.Replace(item.Shortname, string.Empty);
                            properties.ItemEntryModifications.AmmunitionSettings ??= new ItemEntrySettings.AmmoSettings();
                            scanWeaponEntry(item.Shortname, defName, properties, lootTableKey, ref modificationFlag, false);
                            item.ItemEntryModifications = properties.ItemEntryModifications;
                        }

                        item.AppliedProperties = properties;
                    }
                }

                if (node.SubSpawn is not null)
                    foreach (var child in node.SubSpawn)
                        scanVanillaNode(child, lootTableKey, ref modificationFlag);
            }

            // Build entries for loot groups
            bool modifiedLootGroups = false;
            foreach (var lootProfile in lootGroups.LootGroups.ToList())
            {
                foreach (var entry in lootProfile.Value.ItemList)
                {
                    scanEntry(entry.Key, entry.Value.Amount, lootProfile.Key, ref modifiedLootGroups);

                    foreach (var bonusItem in entry.Value.Amount.additionalItems)
                        scanEntry(bonusItem.Key, bonusItem.Value, lootProfile.Key, ref modifiedLootGroups);
                }

                foreach (var bonusItem in lootProfile.Value.GuaranteedItems)
                    scanEntry(bonusItem.Key, bonusItem.Value, lootProfile.Key, ref modifiedLootGroups);
            }

            // Build entries for loot tables
            int activeTypes = 0;
            foreach (var lootTable in lootTables.LootTables.ToList())
            {
                bool validLootSource;
                if (IsUnwrapKey(lootTable.Key))
                {
                    // unwrap/* is an internal table key, never a prefab path.
                    validLootSource = FindUnwrapMod(lootTable.Key) is { revealList: not null };
                }
                else if (IsMissionKey(lootTable.Key))
                {
                    validLootSource = true;
                }
                else
                {
                    var basePrefab = GameManager.server.FindPrefab(lootTable.Key);
                    validLootSource = (basePrefab?.HasComponent<global::HumanNPC>() ?? false) ||  // NPC v1
                                      (basePrefab?.HasComponent<ScientistNPC2>() ?? false) || // NPC v2
                                      (basePrefab?.HasComponent<LootContainer>() ?? false) || // Loot Box
                                      (basePrefab?.HasComponent<LootFill>() ?? false); // Deep Sea Patrol Boat
                }

                if (!validLootSource)
                {
                    lootTables.LootTables.Remove(lootTable.Key);
                    Log($"Removed Invalid Loot Table {lootTable.Key}");
                    modifiedLootTables = true;

                    continue;
                }

                var container = lootTable.Value;

                #region Sort Available Loot Profile Imports
                // Sort by RNG
                container.LootProfiles = container.LootProfiles.OrderBy(x => x.LootProfileProbability).ToList();
                #endregion

                #region pre-v4 Loot System
                // This is the original plugin's loot system. It has not been touched aside from integrating loot profiles.

                // Groups items by rarity (weight). Reference: ItemDefinition.Rarity enum
                Items.Add(lootTable.Key, new List<string>[5]);
                Blueprints.Add(lootTable.Key, new List<string>[5]);

                for (var i = 0; i < 5; ++i)
                {
                    Items[lootTable.Key][i] = new List<string>();
                    Blueprints[lootTable.Key][i] = new List<string>();
                }

                if (container.VanillaLootSlots is not null)
                    foreach (var slot in container.VanillaLootSlots)
                        scanVanillaNode(slot.Loot, lootTable.Key, ref modifiedLootTables);

                // Scan guaranteed items
                foreach (var itemEntry in container.GuaranteedItems)
                    scanEntry(itemEntry.Key, itemEntry.Value, lootTable.Key, ref modifiedLootTables);

                // Scan ungrouped items
                foreach (var itemEntry in container.UngroupedItems)
                {
                    #region Entry Internal Flag Mapping
                    // Scan all items in profile and add internal flags
                    scanEntry(itemEntry.Key, itemEntry.Value, lootTable.Key, ref modifiedLootTables);

                    if (itemEntry.Value.additionalItems?.Count > 0)
                        foreach (var bonusItem in itemEntry.Value.additionalItems)
                            scanEntry(bonusItem.Key, bonusItem.Value, lootTable.Key, ref modifiedLootTables);
                    #endregion

                    bool isBP = itemEntry.Key.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase);
                    var def = ItemManager.FindItemDefinition(UniqueTagREGEX.Replace(itemEntry.Key.Replace(".blueprint", string.Empty), string.Empty));

                    if (def is not null)
                    {
                        if (isBP && def.Blueprint is not null && def.Blueprint.isResearchable)
                        {
                            itemEntry.Value.CanConvertToBlueprint = null; // This is a manual blueprint entry, nullify the conversion option property
                            int index = (int)def.rarity;
                            if (!Blueprints[lootTable.Key][index].Contains(def.shortname))
                                Blueprints[lootTable.Key][index].Add(def.shortname);
                        }
                        else
                        {
                            int index = (int)def.rarity;
                            if (!Items[lootTable.Key][index].Contains(itemEntry.Key))
                                Items[lootTable.Key][index].Add(itemEntry.Key);
                        }
                    }
                }

                TotalItemWeights.Add(lootTable.Key, 0);
                TotalBlueprintWeights.Add(lootTable.Key, 0);
                ItemWeights.Add(lootTable.Key, new int[5]);
                BlueprintWeights.Add(lootTable.Key, new int[5]);

                for (var i = 0; i < 5; ++i)
                {
                    TotalItemWeights[lootTable.Key] += (ItemWeights[lootTable.Key][i] = ItemWeight(BASE_ITEM_RARITY, i) * Items[lootTable.Key][i].Count);
                    TotalBlueprintWeights[lootTable.Key] += (BlueprintWeights[lootTable.Key][i] = ItemWeight(BASE_ITEM_RARITY, i) * Blueprints[lootTable.Key][i].Count);
                }
                #endregion
            }
            
            ItemManagerItemNamesList.Clear();

            if (modifiedLootTables)
                DataSystem.SaveLootTables();

            if (modifiedLootGroups)
                DataSystem.SaveLootGroups();

            activeTypes = lootTables.LootTables.Count(table => table.Value.Enabled);

            Log($"Using '{activeTypes}' active of '{lootTables.LootTables.Count}' supported container types");
        }
        #endregion

        #region Core
        // NPC Implementation
        #region Container Population Boiler
        private bool PopulateContainer(string prefab, LootableCorpse npc, BaseEntity? npcPlayer = null)
        {
            if (npc is not { IsDestroyed: false, containers.Length: > 0 } || npc.containers[0] is not { } inventory)
                return false;

            // API Call
            if (_hasShouldBLPopulateNpc && Interface.CallHook("ShouldBLPopulate_NPC", npc.playerSteamID) != null)
                return false;

            return PopulateContainer(inventory, prefab, true, null, null, npcPlayer ?? npc, out _);
        }

        private bool PopulateContainer(LootContainer container)
        {
            if (container is null || container.IsDestroyed || (_hasShouldBLPopulateContainer && Interface.CallHook("ShouldBLPopulate_Container", container.net.ID.Value) != null))
                return false;

            if (container.inventory is null)
            {
                container.CreateInventory(true);
                container.OnInventoryFirstCreated(container.inventory);
            }

            return PopulateContainer(container.inventory, container.PrefabName, true, null, null, container, out _);
        }

        private bool PopulateContainer(LootFill lootFill)
        {
            if (lootFill.GetComponent<RHIB>() is not RHIB rHIB || (_hasShouldBLPopulateContainer && Interface.CallHook("ShouldBLPopulate_Container", rHIB.net.ID) != null))
                return false;

            if (lootFill.StorageContainer.inventory is null)
            {
                lootFill.StorageContainer.CreateInventory(true);
                lootFill.StorageContainer.OnInventoryFirstCreated(lootFill.StorageContainer.inventory);
            }

            return PopulateContainer(lootFill.StorageContainer.inventory, rHIB.PrefabName, true, null, null, lootFill, out _);
        }
        #endregion

        private bool TryPopulateVanillaRustLoot(ItemContainer dest, string prefab, object? lootSource)
        {
            try
            {
                if (IsUnwrapKey(prefab) && FindUnwrapMod(prefab) is { revealList: not null } unwrap)
                {
                    int minTries = Math.Max(1, Math.Min(unwrap.minTries, unwrap.maxTries));
                    int maxTries = Math.Max(minTries, Math.Max(unwrap.minTries, unwrap.maxTries));
                    int tries = GetRNG(minTries, maxTries);
                    for (int i = 0; i < tries; i++)
                        unwrap.revealList.SpawnIntoContainer(dest);
                    return true;
                }

                if (IsMissionKey(prefab))
                    return false;

                if (lootSource is LootContainer liveLoot && SpawnVanillaLootContainerDefinition(liveLoot, dest))
                    return true;

                if (lootSource is LootFill liveFill && SpawnVanillaLootFillDefinition(liveFill, dest))
                    return true;

                if (lootSource is global::HumanNPC liveNpc && liveNpc.LootSpawnSlots is { Length: > 0 } liveSlots)
                {
                    SpawnVanillaLootSlots(liveSlots, dest);
                    return true;
                }

                GameObject? basePrefab = GameManager.server.FindPrefab(prefab);
                if (basePrefab is null)
                    return false;

                if (basePrefab.GetComponent<LootContainer>() is LootContainer prefabLoot &&
                    SpawnVanillaLootContainerDefinition(prefabLoot, dest))
                    return true;

                if (basePrefab.GetComponent<LootFill>() is LootFill prefabFill &&
                    SpawnVanillaLootFillDefinition(prefabFill, dest))
                    return true;

                if (basePrefab.GetComponent<global::HumanNPC>() is global::HumanNPC npc &&
                    npc.LootSpawnSlots is { Length: > 0 } npcSlots)
                {
                    SpawnVanillaLootSlots(npcSlots, dest);
                    return true;
                }

#if OXIDE_PUBLICIZED
                if (basePrefab.TryGetComponent<FSMComponent>(out var fsm) &&
                    fsm is Scientist2FSM or Scientist2FSM_Heavy or Scientist2FSM_Shotgun)
                {
                    LootContainer.LootSpawnSlot[] spawnSlots = fsm switch
                    {
                        Scientist2FSM a => a.dead.LootSpawnSlots,
                        Scientist2FSM_Heavy b => b.dead.LootSpawnSlots,
                        Scientist2FSM_Shotgun c => c.dead.LootSpawnSlots,
                        _ => Array.Empty<LootContainer.LootSpawnSlot>()
                    };
                    if (spawnSlots.Length > 0)
                    {
                        SpawnVanillaLootSlots(spawnSlots, dest);
                        return true;
                    }
                }
#endif
                return false;
            }
            catch (Exception ex)
            {
                Puts($"[ERROR]: Vanilla loot spawn failed for \"{prefab}\": {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        private bool SpawnVanillaLootContainerDefinition(LootContainer loot, ItemContainer dest)
        {
            if (loot.LootSpawnSlots is { Length: > 0 })
            {
                SpawnVanillaLootSlots(loot.LootSpawnSlots, dest);
                return true;
            }

            if (loot.lootDefinition is null)
                return false;

            int rolls = loot.maxDefinitionsToSpawn;
            for (int i = 0; i < rolls; i++)
                loot.lootDefinition.SpawnIntoContainer(dest);

            return true;
        }

        private bool SpawnVanillaLootFillDefinition(LootFill lootFill, ItemContainer dest)
        {
            if (lootFill.LootSpawnSlots is { Length: > 0 })
            {
                SpawnVanillaLootSlots(lootFill.LootSpawnSlots, dest);
                return true;
            }

            if (lootFill.LootDefinition is null)
                return false;

            int rolls = lootFill.MaxDefinitionsToSpawn;
            for (int i = 0; i < rolls; i++)
                lootFill.LootDefinition.SpawnIntoContainer(dest);

            return true;
        }

        private void SpawnVanillaLootSlots(LootContainer.LootSpawnSlot[] slots, ItemContainer dest)
        {
            foreach (var slot in slots)
            {
                if (slot.definition is null)
                    continue;

                if (slot.eras is { Length: > 0 } && Array.IndexOf(slot.eras, ConVar.Server.Era) < 0)
                    continue;

                for (int i = 0; i < slot.numberToSpawn; i++)
                {
                    if (slot.probability < 1f && UnityEngine.Random.Range(0f, 1f) > slot.probability)
                        continue;

                    slot.definition.SpawnIntoContainer(dest);
                }
            }
        }

        private void ApplyVanillaLootOverlays(ItemContainer container, ItemContainer? overflowContainer,
            BasePlayer? dropOwner, PrefabLoot con, bool clearContainer, out int deliveredItems)
        {
            int multiplier = Math.Max(1, _config.Loot.LootMultiplier);
            using PooledList<Item> snapshot = Pool.Get<PooledList<Item>>();
            snapshot.AddRange(container.itemList);

            foreach (Item item in snapshot)
            {
                if (item?.info is null)
                    continue;

                if (storedBlacklist?.ItemList.Contains(item.info.shortname) == true)
                {
                    item.Remove();
                    continue;
                }

                if (multiplier > 1 && item.info.shortname != "scrap")
                {
                    int stack = Math.Max(1, item.info.stackable);
                    long next = (long)item.amount * multiplier;
                    item.amount = (int)Math.Min(next, stack);
                    item.MarkDirty();
                }
            }

            foreach (var gItemEntry in con.GuaranteedItems)
            {
                if (gItemEntry.Value.IncludedInVanillaLoot)
                    continue;

                string itemName = StripUniqueTag(gItemEntry.Key);
                bool spawnAsBlueprint = itemName.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase);
                itemName = itemName.Replace(".blueprint", string.Empty, StringComparison.OrdinalIgnoreCase);

                Item? gItem;
                if (spawnAsBlueprint && BlueprintBaseDef is not null && ItemManager.FindItemDefinition(itemName) is { } blueprintTarget)
                {
                    gItem = ItemManager.Create(BlueprintBaseDef);
                    if (gItem is not null)
                        gItem.blueprintTarget = blueprintTarget.itemid;
                }
                else
                {
                    gItem = ItemManager.CreateByName(itemName, GetRNG(gItemEntry.Value.Min, gItemEntry.Value.Max), gItemEntry.Value.SkinId);
                }

                if (gItem is null)
                    continue;

                gItemEntry.Value.ApplyAllProperties(gItem);
                if (!(gItem.MoveToContainer(container) ||
                      (overflowContainer is not null && gItem.MoveToContainer(overflowContainer))))
                {
                    if (dropOwner is not null)
                        gItem.Drop(dropOwner.GetDropPosition(), dropOwner.GetDropVelocity(), default);
                    else
                        gItem.DoRemove();
                }
            }

            int scrapAmt;
            if (con.ItemSettings.MinScrap > con.ItemSettings.MaxScrap)
                scrapAmt = con.ItemSettings.MinScrap;
            else if (con.ItemSettings.MaxScrap > con.ItemSettings.MinScrap)
                scrapAmt = GetRNG(con.ItemSettings.MinScrap, con.ItemSettings.MaxScrap);
            else
                scrapAmt = con.ItemSettings.MaxScrap;

            if (scrapAmt > 0)
            {
                Item scrap = ItemManager.CreateByItemID(-932201673, scrapAmt * _config.Loot.ScrapMultiplier);
                if (scrap is not null &&
                    !(scrap.MoveToContainer(container) ||
                      (overflowContainer is not null && scrap.MoveToContainer(overflowContainer))))
                {
                    if (dropOwner is not null)
                        scrap.Drop(dropOwner.GetDropPosition(), dropOwner.GetDropVelocity(), default);
                    else
                        scrap.DoRemove();
                }
            }

            ItemManager.DoRemoves();

            if (clearContainer)
                container.capacity = container.itemList.Count;

            container.MarkDirty();
            overflowContainer?.MarkDirty();
            deliveredItems = container.itemList.Count;
        }

        private void TryFillFromLootProfile(LootProfile lootProfile, string? profileName,
            HashSet<string> currentItemEntries, List<KeyValuePair<string, LootEntrySettings>> guaranteedFromProfile,
            ref ItemConvertInfo? itemInfo, ref List<ItemConvertInfo>? bonusItems,
            ref bool isLootGroupItem, ref string? selectedProfileName)
        {
            if (!lootProfile.DoProbabilitiesExist)
                lootProfile.UpdateProbabilities(lootProfile.ItemList.Select(x => x.Value.Probability));

            (itemInfo, bonusItems) = lootProfile.GetItem(currentItemEntries);
            if (itemInfo == null)
                return;

            guaranteedFromProfile.AddRange(lootProfile.GuaranteedItems);
            isLootGroupItem = true;
            selectedProfileName = profileName;
        }

        private bool PopulateContainer(ItemContainer? container, string? prefab)
            => PopulateContainer(container, prefab, true, null, null, null, out _);

        private bool PopulateContainer(ItemContainer? container, string? prefab, bool clearContainer,
            ItemContainer? overflowContainer, BasePlayer? dropOwner, out int deliveredItems)
            => PopulateContainer(container, prefab, clearContainer, overflowContainer, dropOwner, null, out deliveredItems);

        private bool PopulateContainer(ItemContainer? container, string? prefab, bool clearContainer,
            ItemContainer? overflowContainer, BasePlayer? dropOwner, object? vanillaLootSource, out int deliveredItems)
        {
            deliveredItems = 0;

            // LINQ optimizations here courtesy of Shady14u
            if (container is null || prefab is null || !(lootTables?.LootTables?.TryGetValue(prefab, out PrefabLoot? con) ?? false) || con is null || !con.Enabled)
                return false;

            int min = con.ItemSettings.ItemsMin, max = con.ItemSettings.ItemsMax;
            int minBPs = con.ItemSettings.MinBlueprints, maxBPs = con.ItemSettings.MaxBlueprints;

            int itemCount = Math.Clamp(GetRNG(Math.Min(min, max), Math.Max(min, max)), 1, 36);

            if (clearContainer)
            {
                container.capacity = 36;
                container.Clear();
            }

            if (con.UseVanillaLootRng)
            {
                ItemContainer vanillaContainer = container;
                if (!clearContainer)
                {
                    vanillaContainer = new ItemContainer();
                    vanillaContainer.ServerInitialize(null, 1);
                    vanillaContainer.containerVolume = int.MaxValue;
                    vanillaContainer.GiveUID();
                    vanillaContainer.onItemAddedRemoved = (reward, added, _) =>
                    {
                        if (added && reward.parent is not null)
                            reward.parent.capacity = reward.parent.itemList.Count + 1;
                    };
                }

                try
                {
                    if (TrySpawnSavedVanillaLoot(con, vanillaContainer) || TryPopulateVanillaRustLoot(vanillaContainer, prefab, vanillaLootSource))
                    {
                        ApplyVanillaLootOverlays(vanillaContainer, clearContainer ? overflowContainer : null,
                            clearContainer ? dropOwner : null, con, clearContainer, out deliveredItems);
                        if (!clearContainer)
                        {
                            deliveredItems = 0;
                            using PooledList<Item> rewards = Pool.Get<PooledList<Item>>();
                            rewards.AddRange(vanillaContainer.itemList);
                            foreach (Item reward in rewards)
                            {
                                if (reward is null || !reward.IsValid())
                                    continue;

                                int amount = reward.amount;
                                if (reward.MoveToContainer(container) ||
                                    (overflowContainer is not null && reward.MoveToContainer(overflowContainer)))
                                    deliveredItems++;
                                else
                                {
                                    bool partiallyDelivered = reward.amount < amount;
                                    if (dropOwner is not null && reward.Drop(dropOwner.GetDropPosition(), dropOwner.GetDropVelocity(), default) is not null)
                                        deliveredItems++;
                                    else
                                    {
                                        if (partiallyDelivered)
                                            deliveredItems++;
                                        reward.DoRemove();
                                    }
                                }
                            }

                            container.MarkDirty();
                            overflowContainer?.MarkDirty();
                        }

                        return true;
                    }
                }
                finally
                {
                    if (!clearContainer)
                        vanillaContainer.Kill();
                }
            }

            // Cache frequently accessed config values to avoid property access in loop
            bool allowDupes = _config.Loot.AllowDuplicateItems;
            bool allowGroupDupes = _config.LootGroupsConfig.AllowLootGroupDuplicateItems;
            bool allowBonusDupes = _config.Loot.AllowBonusItemsDuplicateItems;
            bool guaranteedItemsCount = con.ItemSettings.guaranteedItemsAddCount;
            bool lootLockingEnabled = _config.Loot.EnableLootPoolLocking && con.LootPoolLocking;
            
            using PooledList<string> itemNames = Pool.Get<PooledList<string>>(); // Current item shortnames
            if (itemNames.Capacity < itemCount)
                itemNames.Capacity = itemCount;

            using PooledList<ItemConvertInfo> items = Pool.Get<PooledList<ItemConvertInfo>>();
            if (items.Capacity < itemCount + 5) // +5 for guaranteed items buffer
                items.Capacity = itemCount + 5;

            using PooledList<int> itemBlueprints = Pool.Get<PooledList<int>>();
            if (itemBlueprints.Capacity < maxBPs)
                itemBlueprints.Capacity = maxBPs;

            using PooledList<KeyValuePair<string, LootEntrySettings>> guaranteedItemEntries = Pool.Get<PooledList<KeyValuePair<string, LootEntrySettings>>>();
            using PooledHashSet<string> currentItemEntries = Pool.Get<PooledHashSet<string>>(); // Current unique item entry tags (for duplicate generation checking)

            guaranteedItemEntries.AddRange(con.GuaranteedItems);

            Dictionary<string, int>? profileItemCounts = null;
            if (lootLockingEnabled || con.HasEnabledProfileItemLimits())
                profileItemCounts = new Dictionary<string, int>();
            
            // Attempt to select initial profile for LPL
            LootProfile? lockedProfile = null;
            string? lockedProfileName = null;
            if (lootLockingEnabled)
                lockedProfile = con.GetRandomProfile(prefab, profileItemCounts, out lockedProfileName); // Loot pool locking sets an initial profile, if none was set a profile will be selected every time pulling items from either a group or 'null group' (pulling from ungrouped items)
            
            int maxRetry = 10;
            List<KeyValuePair<string, LootEntrySettings>> guaranteedFromProfile = Pool.Get<List<KeyValuePair<string, LootEntrySettings>>>();

            bool IsDuplicate(Item item, bool bonusItem, bool fromGroup) =>
                ((fromGroup && !allowGroupDupes) || (bonusItem && !allowBonusDupes) ||
                 (!bonusItem && !allowDupes)) && ((itemNames.Contains(item.info.shortname) ||
                                                   (item.IsBlueprint() &&
                                                    itemBlueprints.Contains(item.blueprintTarget))));
            
            try
            {
            for (int i = guaranteedItemsCount ? guaranteedItemEntries.Count : 0; i < itemCount; ++i)
            {
                ItemConvertInfo? itemInfo = null;
                List<ItemConvertInfo>? bonusItems = null;
                bool isLootGroupItem = false;
                string? selectedProfileName = null;
                guaranteedFromProfile.Clear();
             
                try
                {
                    bool useLockedProfile = lockedProfile is not null &&
                                            !con.IsProfileAtItemLimit(lockedProfileName, profileItemCounts);

                    if (useLockedProfile) // Loot Pool Locking
                    {
                        TryFillFromLootProfile(lockedProfile!, lockedProfileName, currentItemEntries, guaranteedFromProfile,
                            ref itemInfo, ref bonusItems, ref isLootGroupItem, ref selectedProfileName);
                    } else // Normal System (or locked profile already hit its per-crate item cap)
                    {
                        #region Attempt Loot Import Select
                        if (!lootLockingEnabled && con.GetRandomProfile(prefab, profileItemCounts, out string? rolledProfileName) is {} profile)
                            TryFillFromLootProfile(profile, rolledProfileName, currentItemEntries, guaranteedFromProfile,
                                ref itemInfo, ref bonusItems, ref isLootGroupItem, ref selectedProfileName);
                        #endregion
                        
                        // Used if LPL is enabled but no profile import was used (selecting ungrouped profile as the locked profile)
                        // Also used when a locked profile has already contributed its max items for this fill.
                        #region Ungrouped Items Select
                        // Loot import not used, generate from ungrouped items with default rng system
                        if (itemInfo == null)
                        {
                            if (con.IgnoreRarityBias) 
                                (itemInfo, bonusItems) = UngroupedFlatSelect(con, currentItemEntries, itemBlueprints.Count >= con.ItemSettings.MaxBlueprints);
                            else
                                (itemInfo, bonusItems) = MightyRNG(con, currentItemEntries, prefab, itemCount, itemBlueprints.Count >= con.ItemSettings.MaxBlueprints);
                        }
                        #endregion
                    }
                    
                }
                catch (Exception e)
                {
                    Puts($"[ERROR]: Failed to generate item for \"{prefab}\". Reason: {e.Message} \n{e.StackTrace}");
                }
                
                // No item was generated from either system, attempt to regenerate.
                if (itemInfo == null)
                {
                    if (--maxRetry <= 0)
                        break;

                    --i;
                    continue;
                }

                // Duplicate checking
                if (IsDuplicate(itemInfo.Item, false, isLootGroupItem))
                {
                    itemInfo.Item.Remove();
                    if (--maxRetry <= 0)
                        break;

                    --i;
                    continue;
                }

                if (itemInfo.Item.IsBlueprint())
                {
                    itemBlueprints.Add(itemInfo.Item.blueprintTarget);
                }
                else
                {
                    itemNames.Add(itemInfo.Item.info.shortname);
                }

                if (storedBlacklist.ItemList.Contains(itemInfo.Item.info.shortname))
                {
                    itemInfo.Item.Remove(); // broken item fix
                    continue;
                }

                items.Add(itemInfo);

                if (isLootGroupItem && selectedProfileName is not null && profileItemCounts is not null)
                {
                    profileItemCounts.TryGetValue(selectedProfileName, out int takenFromProfile);
                    profileItemCounts[selectedProfileName] = takenFromProfile + 1;
                }

                //Only if bonus items are present
                if (bonusItems is not null)
                {
                    foreach (ItemConvertInfo bonusItem in bonusItems)
                    {
                        if (IsDuplicate(bonusItem.Item, true, isLootGroupItem))
                            bonusItem.Item.Remove();
                        else
                        {
                            items.Add(bonusItem);

                            if (con.ItemSettings.bonusItemsAddCount && ++i >= itemCount)
                                break;
                        }
                    }
                }

                if (guaranteedItemsCount)
                {
                    int t = Math.Min(itemCount - i, guaranteedFromProfile.Count);
                    for (int g = 0; g < t; g++)
                        guaranteedItemEntries.Add(guaranteedFromProfile[g]);
                    if ((i += t) >= itemCount)
                        break;
                }
                else if (guaranteedFromProfile.Count > 0)
                {
                    guaranteedItemEntries.AddRange(guaranteedFromProfile);
                }
            }
            }
            finally
            {
                Pool.FreeUnmanaged(ref guaranteedFromProfile);
            }

            guaranteedItemEntries.Shuffle((uint)GetRNG(0, 100));
            foreach (var gItemEntry in guaranteedItemEntries)
            {
                // Spawn item. No rng, just spawn em.
                string itemName = StripUniqueTag(gItemEntry.Key);
                bool spawnAsBlueprint = itemName.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase);
                itemName = itemName.Replace(".blueprint", string.Empty, StringComparison.OrdinalIgnoreCase);

                Item? gItem;
                if (spawnAsBlueprint && BlueprintBaseDef is not null && ItemManager.FindItemDefinition(itemName) is { } blueprintTarget)
                {
                    gItem = ItemManager.Create(BlueprintBaseDef);
                    if (gItem is not null)
                    {
                        gItem.blueprintTarget = blueprintTarget.itemid;
                        itemBlueprints.Add(blueprintTarget.itemid);
                    }
                }
                else
                {
                    gItem = ItemManager.CreateByName(itemName, GetRNG(gItemEntry.Value.Min, gItemEntry.Value.Max), gItemEntry.Value.SkinId);
                }
                
                if (gItem is null)
                    continue;

                gItemEntry.Value.ApplyAllProperties(gItem);
                
                items.Add(new (gItem, gItemEntry.Value.CanConvertToBlueprint ?? false));
            }

            int scrapAmt = 0;
            if (con.ItemSettings.MinScrap > con.ItemSettings.MaxScrap)
            { // Lower max to min
                scrapAmt = con.ItemSettings.MinScrap;
                con.ItemSettings.MaxScrap = con.ItemSettings.MinScrap;
            }
            else if (con.ItemSettings.MaxScrap > con.ItemSettings.MinScrap)
            {
                scrapAmt = GetRNG(con.ItemSettings.MinScrap, con.ItemSettings.MaxScrap);
            }
            else
            {
                scrapAmt = con.ItemSettings.MaxScrap;
            }

            // Add scrap
            if (scrapAmt > 0)
                items.Add(new (ItemManager.CreateByItemID(-932201673, scrapAmt * _config.Loot.ScrapMultiplier), false)); // Scrap item ID

            // Blueprint conversion pass
            if (_config.Loot.EnableBlueprintConversion && con.ItemSettings.MaxBlueprints > 0)
            {
                int currentBPCount = itemBlueprints.Count;

                if (currentBPCount < maxBPs)
                {
                    int targetBPCount = GetWeightedRNG(minBPs, maxBPs, _config.Generic.BlueprintWeight);
                    
                    if (currentBPCount < targetBPCount)
                    {
                        if (BlueprintBaseDef is not null)
                        {
                            using PooledList<int> eligibleItemIndices = Pool.Get<PooledList<int>>();
                            for (int i = 0; i < items.Count; i++)
                            {
                                ItemConvertInfo itemEntry = items[i];
                                if (itemEntry == null || itemEntry.Item.IsBlueprint())
                                    continue;

                                ItemDefinition def = itemEntry.Item.info;
                                if (def?.Blueprint is null || !def.Blueprint.isResearchable)
                                    continue;
                                
                                // Tracked from upper item generation system
                                if (itemEntry.CanBeBp)
                                    eligibleItemIndices.Add(i);
                            }

                            while (currentBPCount < targetBPCount && eligibleItemIndices.Count > 0)
                            {
                                int randomIdx = GetRNG(0, eligibleItemIndices.Count - 1);
                                int itemIdx = eligibleItemIndices[randomIdx];
                                ItemConvertInfo originalItem = items[itemIdx];

                                int blueprintTarget = originalItem.Item.info.itemid;

                                bool hasDuplicateBP = !_config.Loot.AllowDuplicateBlueprints && itemBlueprints.Contains(blueprintTarget);
                                if (hasDuplicateBP)
                                {
                                    eligibleItemIndices.RemoveAt(randomIdx);
                                    continue;
                                }

                                Item blueprint = ItemManager.Create(BlueprintBaseDef);
                                blueprint.blueprintTarget = blueprintTarget;

                                originalItem.Item.Remove();
                                items[itemIdx] = new ItemConvertInfo(blueprint, false);
                                itemBlueprints.Add(blueprintTarget);

                                currentBPCount++;
                                eligibleItemIndices.RemoveAt(randomIdx);
                            }
                        }
                    }
                }
            }

            items.Shuffle((uint)UnityEngine.Random.Range(0, 100));
            for (int n = 0; n < items.Count; n++)
            {
                var item = items[n];
                if (item is null || !item.Item.IsValid())
                    continue;

                if (item.Item.MoveToContainer(container) ||
                    (overflowContainer is not null && item.Item.MoveToContainer(overflowContainer)))
                {
                    deliveredItems++;
                }
                else if (dropOwner is not null)
                {
                    item.Item.Drop(dropOwner.GetDropPosition(), dropOwner.GetDropVelocity(), default);
                    deliveredItems++;
                }
                else
                {
                    item.Item.DoRemove();
                }
            }

            if (clearContainer)
                container.capacity = container.itemList.Count;

            container.MarkDirty();
            overflowContainer?.MarkDirty();

            return true;
        }

        private void UpdateInternals(bool doLog)
        {
            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }

            if (doLog)
                Log("Updating internals ...");

            int populatedContainers = 0;

            // Pre kill crate markers to avoid spam.
            var crates = Pool.Get<PooledList<HackableLockedCrate>>();
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is HackableLockedCrate { IsDestroyed: false, mapMarkerInstance: { IsDestroyed: false } } crate)
                    crates.Add(crate);
            }
            foreach (var crate in crates)
                crate.mapMarkerInstance.Kill();

            bool APICheck(BaseEntity entity)
                => CustomLootSpawns is not null && CustomLootSpawns.Call<bool>("IsLootBox", entity);
            
            NextTick(() =>
            {
                if (_config.Generic.RemoveStackedContainers)
                    FixLoot();

                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    if (entity is LootContainer lootContainer)
                    {
                        if (!APICheck(lootContainer) && PopulateContainer(lootContainer))
                            populatedContainers++;
                    }
                    else if (entity is RHIB rhib && rhib.GetComponent<LootFill>() is { } lf && !APICheck(rhib) && PopulateContainer(lf))
                    {
                        populatedContainers++;
                    }
                }

                if (doLog)
                    Log($"Populated ({populatedContainers}) supported loot containers.");

                Initialized = true;
                populatedContainers = 0;

                // Add crate markers back to those that had it
                foreach (var crate in crates)
                    if (crate is { IsDestroyed: false })
                        crate.CreateMapMarker(120);

                if (crates.Count > 0)
                    Puts($"Restored {crates.Count} crate markers.");

                // Manual pool free since we get detached
                Pool.Free(ref crates);
            });
        }

        private void FixLoot()
        {
            var spawns = Pool.Get<List<LootContainer>>();
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is LootContainer { IsDestroyed: false, isActiveAndEnabled: true } loot)
                    spawns.Add(loot);
            }

            spawns.Sort((a, b) =>
            {
                int cmp = a.transform.position.x.CompareTo(b.transform.position.x);
                return cmp != 0 ? cmp : a.transform.position.z.CompareTo(b.transform.position.z);
            });

            var count = spawns.Count;
            var racelimit = count * count;

            var antirace = 0;
            var deleted = 0;
            const float stackDistSqr = 0.25f * 0.25f;

            for (var i = 0; i < count; i++)
            {
                var box = spawns[i];
                var posX = box.transform.position.x;
                var posZ = box.transform.position.z;

                if (++antirace > racelimit)
                {
                    Pool.FreeUnmanaged(ref spawns);
                    return;
                }

                var next = i + 1;
                while (next < count)
                {
                    var box2 = spawns[next];
                    var dx = posX - box2.transform.position.x;
                    var dz = posZ - box2.transform.position.z;
                    var distanceSqr = dx * dx + dz * dz;

                    if (++antirace > racelimit)
                    {
                        Pool.FreeUnmanaged(ref spawns);
                        return;
                    }

                    if (distanceSqr < stackDistSqr)
                    {
                        spawns.RemoveAt(next);
                        count--;

                        if (box2 is BaseEntity _box2 && !_box2.IsDestroyed)
                        {
                            _box2.KillMessage();
                            deleted++;
                        }
                    }
                    else
                        break;
                }
            }

            Pool.FreeUnmanaged(ref spawns);

            if (deleted > 0)
                Log($"Removed {deleted} stacked LootContainer");
            else
                Log($"No stacked LootContainer found.");
        }

        // Mighty RNG but without the rarity bias for item selection. Random entry is selected within the list's index.
        private (ItemConvertInfo? item, List<ItemConvertInfo>? bonusItems) UngroupedFlatSelect(PrefabLoot entry, HashSet<string> currentItemEntries, bool blockBPs = false)
        {
            bool asBP = RNG.NextDouble() < _config.Generic.BlueprintWeight && !blockBPs;
            int available = 0;
            foreach (var x in entry.UngroupedItems)
            {
                if (IsAvailableUngroupedEntry(x.Key, x.Value, currentItemEntries, asBP))
                    available++;
            }

            if (available == 0)
                return default;

            int pick = RNG.Next(available);
            KeyValuePair<string, LootEntry> selectedEntry = default;
            foreach (var x in entry.UngroupedItems)
            {
                if (!IsAvailableUngroupedEntry(x.Key, x.Value, currentItemEntries, asBP))
                    continue;
                if (pick-- == 0)
                {
                    selectedEntry = x;
                    break;
                }
            }

            if (selectedEntry.Value is null)
                return default;

            var lootEntry = selectedEntry.Value;
            
            string itemShortname = StripUniqueTag(selectedEntry.Key).Replace(".blueprint", string.Empty, StringComparison.OrdinalIgnoreCase);

            List<ItemConvertInfo>? bonusItems = null;
            ItemDefinition itemDef = ItemManager.FindItemDefinition(itemShortname);
            
            // Explicit failsafe, caught in outer call.
            if (itemDef is null)
                throw new Exception($"Error: invalid shortname {itemShortname}. Should not happen, contact developer.");
            
            Item item;
            if (asBP && itemDef.Blueprint is not null && itemDef.Blueprint.isResearchable)
            {
                item = ItemManager.Create(BlueprintBaseDef);
                item.blueprintTarget = itemDef.itemid;
            } else
            {
                item = ItemManager.Create(itemDef);
            }
            
            if (item is null)
                return default;

            #region Apply custom properties
            item.amount = GetRNG(lootEntry.Min, lootEntry.Max) * _config.Loot.LootMultiplier;
            item.skin = lootEntry.SkinId;

            lootEntry.ApplyAllProperties(item);
            #endregion
            
            lootEntry.CreateBonusItems(ref bonusItems); // Create bonus items and apply attachments
            
            // Add for future duplicate checking.
            currentItemEntries.Add(selectedEntry.Key);
            
            item.OnVirginSpawn();
            return (new ItemConvertInfo(item, lootEntry.CanConvertToBlueprint ?? false), bonusItems);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAvailableUngroupedEntry(string key, LootEntry value, HashSet<string> currentItemEntries, bool asBP)
            => !(currentItemEntries.Contains(key) && !value.allowDuplicates) &&
               key.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase) == asBP;

        /// <summary>
        /// Select an entry from the specified loot table using the ingame rarity system as the bias / weight for selection.
        /// </summary>
        /// <param name="entry">Loot table entry</param>
        /// <param name="currentItemEntries">List of currently generated items' shortnames</param>
        /// <param name="type">Prefab name</param>
        /// <param name="itemCount"></param>
        /// <param name="blockBPs"></param>
        /// <returns>Random item and its bonus item list if applicable.</returns>
        private (ItemConvertInfo? item, List<ItemConvertInfo>? bonusItem) MightyRNG(PrefabLoot entry, HashSet<string> currentItemEntries, string type, int itemCount, bool blockBPs = false)
        {
            List<ItemConvertInfo>? bonusItems = null;
            LootEntry? lootEntry = null;
            Item? item = null;

            bool asBP = !blockBPs && TotalBlueprintWeights[type] > 0 && RNG.NextDouble() < _config.Generic.BlueprintWeight;
            string itemEntryName = string.Empty;
            int maxRetry = 10 * itemCount;
            var weights = asBP ? BlueprintWeights[type] : ItemWeights[type];
            var prefabs = asBP ? Blueprints[type] : Items[type];
            var totalWeight = asBP ? TotalBlueprintWeights[type] : TotalItemWeights[type];

            // TODO change duplicate regen to O(1) nudge to next or previous neighbour
            do
            {
                item = null;

                if (totalWeight <= 0)
                {
                    if (--maxRetry <= 0)
                        break;

                    continue;
                }

                var r = RNG.Next(totalWeight);
                int limit = 0;
                List<string>? selectFrom = null;
                for (int i = 0; i < 5; ++i)
                {
                    limit += weights[i];
                    if (r < limit)
                    {
                        selectFrom = prefabs[i];
                        break;
                    }
                }

                if (selectFrom == null || selectFrom.Count == 0)
                {
                    if (--maxRetry <= 0)
                        break;

                    continue;
                }

                // Select item name
                itemEntryName = selectFrom[RNG.Next(0, selectFrom.Count)];

                if (!entry.UngroupedItems.TryGetValue(itemEntryName + (asBP ? ".blueprint" : string.Empty), out lootEntry) || lootEntry is null)
                {
                    Puts($"Cannot get config for item {itemEntryName} in prefab {type} bp: {asBP}");
                    if (--maxRetry <= 0)
                        break;

                    continue;
                }
                
                if (!lootEntry.allowDuplicates && currentItemEntries.Contains(itemEntryName))
                {
                    // Check if is only possible item to avoid retry if needed
                    if (entry.UngroupedItems.Count == 1)
                        return (null, null);

                    if (--maxRetry <= 0)
                        break;

                    continue;
                }

                string itemShortname = StripUniqueTag(itemEntryName);
                ItemDefinition itemDef = ItemManager.FindItemDefinition(itemShortname);
                if (itemDef is null)
                {
                    if (--maxRetry <= 0)
                        break;

                    continue;
                }

                if (asBP && itemDef.Blueprint is not null && itemDef.Blueprint.isResearchable)
                {
                    item = ItemManager.Create(BlueprintBaseDef);
                    item.blueprintTarget = itemDef.itemid;
                }
                else
                {
                    item = ItemManager.Create(itemDef);
                }

                // Shouldn't happen
                if (item.info is null)
                {
                    if (--maxRetry <= 0)
                        break;

                    continue;
                }

                break;
            } while (true);

            if (item is null)
                return (null, null);

            if (lootEntry is null)
            {
                item.Remove();
                return (null, null);
            }

            // Apply custom properties
            item.amount = GetRNG(lootEntry.Min, lootEntry.Max) * _config.Loot.LootMultiplier;
            item.skin = lootEntry.SkinId;

            lootEntry.ApplyAllProperties(item);
            lootEntry.CreateBonusItems(ref bonusItems); // Create bonus items and apply attachments

            // Add for future duplicate checking.
            currentItemEntries.Add(itemEntryName);

            item.OnVirginSpawn();
            return (new ItemConvertInfo(item, lootEntry.CanConvertToBlueprint ?? false), bonusItems);
        }

        
        private bool ItemExists(string name)
            => ItemManager.FindItemDefinition(name) is not null;

        // API
        private bool isSupplyDropActive()
        {
            if (!lootTables.LootTables.TryGetValue("assets/prefabs/misc/supply drop/supply_drop.prefab", out PrefabLoot? con) || con is null)
                return false;

            if (con.Enabled)
                return true;

            return false;
        }
        #endregion

        #region Looty API Commands
        [ChatCommand("looty")]
        private void LootyConfigDownload(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, ADMIN_PERM))
            {
                SendMessage(player, BLLang("perm"));
                return;
            }

            if (args.Length != 1)
            {
                SendMessage(player, BLLang("lootycmdformat", player.UserIDString));
                return;
            }

            GetLootyAPI(args[0], player);
        }

        [ConsoleCommand("looty")]
        private void LootyConfigDownload_Console(Arg arg)
        {
            if (!arg.IsRcon)
            {
                arg.ReplyWith("Error: Should not execute command outside of RCON.");
                return;
            }

            if (arg.Args is not {Length: 1} args)
            {
                Puts(BLLang("lootycmdformat"));
                Puts("Please visit https://looty.cc/betterloot to create your custom loot configuration!");
                return;
            }

            // Send Request
            GetLootyAPI(args[0].ToString());
        }

        #region Processing Routine
        private void GetLootyAPI(string lootyId, BasePlayer? player = null)
        {
            // Compatibility between console and chat
            void Respond(string key)
            {
                string _lang = BLLang(key);
                if (player is not null)
                    SendMessage(player, _lang);
                else
                    Puts(_lang);
            }

            IEnumerator SendRequest()
            {
                using (UnityWebRequest www = UnityWebRequest.Get($"https://looty.cc/api/fetch-loot-table?id={lootyId}"))
                {
                    Respond($"Attempting to download configuration: {lootyId}");
                    yield return www.SendWebRequest();

                    if (www.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
                    {
                        long code = www.responseCode;
                        if (www.result is UnityWebRequest.Result.ProtocolError && (code == 404 || code == 410))
                            Respond("lootynotfound");

                        Puts($"Error: Could not download request: {www.result} ({code})");
                    }
                    else
                    {
                        #region API Download + Verification
                        LootyResponse tableData;
                        try { 
                            tableData = JsonConvert.DeserializeObject<LootyResponse>(www.downloadHandler.text);

                            if (tableData.IsUnityNull())
                            {
                                Respond("Error: Failed to load data. Aborting...");
                                yield break;
                            }

                            // Verification
                            bool targetMatch = tableData.looty.target.Equals("betterloot", StringComparison.OrdinalIgnoreCase);
                            bool idMatch = tableData.looty.id.Equals(lootyId, comparisonType: StringComparison.Ordinal);
                            if (!(idMatch && targetMatch))
                            {
                                Puts("Invalid table received! {0} {1} Please contact developer!", !targetMatch ? "Wrong plugin target!" : string.Empty, !idMatch ? "Mismatch in Table ID!" : string.Empty);
                                yield break;
                            }
                        } catch (Exception ex)
                        {
                            Log($"Failed to download table \"{lootyId}\"... Reason: {ex.Message}");
                            yield break;
                        }
                        #endregion
                        
                        #region File Updates
                        bool restoreFailsafe = false;
                        
                        #region LootTable.json Update
                        try
                        {
                            DataSystem.BakDataFile("LootTables");
                            
                            if (lootTables != null) 
                                lootTables.LootTables = tableData.LootTables;
                            else
                                lootTables = new () { LootTables = tableData.LootTables };
                            
                            restoreFailsafe = true; // Failsafe flag. Restore on error / fail
                            DataSystem.SaveLootTables();

                            Respond("Loaded new LootTable successfully!");
                            restoreFailsafe = false;

                            SyncWatchedPrefabsFromLootTables(enableIfTableEnabled: true);
                            EnsureNaturalBeehiveWatched();
                        }
                        catch (Exception ex)
                        {
                            Respond("Failed to load new LootTables.json data...");

                            if (restoreFailsafe)
                                DataSystem.BakDataFile("LootTables", true);

                            Log($"Failed to load new LootTables data, please contact developer. Message: \n{ex}");
                        }
                        #endregion

                        #region LootGroups.json Update
                        try {
                            
                            if (tableData.LootGroups != null)
                            {
                                DataSystem.BakDataFile("LootGroups");
                                
                                if (lootGroups != null)
                                {
                                    lootGroups.LootGroups = tableData.LootGroups;
                                }
                                else
                                {
                                    // Data non-existent, create new data and save.
                                    lootGroups = new() { LootGroups = tableData.LootGroups };
                                    LootGroupsData.TryCreateExampleGroup();
                                }
                                
                                restoreFailsafe = true;
                                DataSystem.SaveLootGroups();

                                Respond("Loaded new LootGroups.json successfully");
                                restoreFailsafe = false;
                            }
                        } catch (Exception ex)
                        {
                            Respond("LootGroups.json load failed.");
                            Log($"Failed to load new LootGroups data, please contact developer. Message: \n{ex}");
                            
                            if (restoreFailsafe)
                                DataSystem.BakDataFile("LootGroups", true);
                            
                            yield break;
                        }
                        
                        InitLootSystem(true);

                        #region Ore Spawn Update
                        try
                        {
                            if (tableData.OreSpawn != null)
                            {
                                DataSystem.BakDataFile("GatherRates");
                                oreSpawnData = new OreSpawnData { OreSpawn = tableData.OreSpawn };
                                DataSystem.SaveOreSpawn();
                                RestartOreSpawnSystem();
                                Respond("Loaded new GatherRates.json successfully");
                            }
                        }
                        catch (Exception ex)
                        {
                            Respond("GatherRates.json load failed.");
                            Log($"Failed to load Ore Spawn data: {ex.Message}");
                        }
                        #endregion
                        #endregion
                    }
                }
            }

            ServerMgr.Instance.StartCoroutine(SendRequest());
        }
        #endregion
        #endregion

        #region Commands
        #region Backup / Restore
        [ChatCommand("bl-backup")]
        private void ManualBackupCommand(BasePlayer player)
            => ManualBackupRestore(player, false);

        [ChatCommand("bl-restore")]
        private void RestoreBackupCommand(BasePlayer player)
            => ManualBackupRestore(player, true);

        private void ManualBackupRestore(BasePlayer player, bool restore)
        {
            if (!permission.UserHasPermission(player.UserIDString, ADMIN_PERM))
            {
                SendMessage(player, BLLang("perm"));
                return;
            }

            DataSystem.BakDataFile("LootTables", restore, player);
        }

        [ConsoleCommand("bl-backup")]
        private void ManualBackupCommand_Console(Arg arg)
            => ManualBackupRestore_Console(arg, false);

        [ConsoleCommand("bl-restore")]
        private void RestoreBackupCommand_Console(Arg arg)
            => ManualBackupRestore_Console(arg, true);

        private void ManualBackupRestore_Console(Arg arg, bool restore)
        {
            if (!arg.IsRcon)
                return;

            DataSystem.BakDataFile("LootTables", restore);
        }
        #endregion

        [ChatCommand("blacklist")]
        private void CmdChatBlacklistNew(BasePlayer player, string command, string[] args)
        {

            if (!Initialized)
            {
                SendMessage(player, BLLang("initialized"));
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, ADMIN_PERM))
            {
                SendMessage(player, BLLang("perm"));
                return;
            }

            if (args.Length is 0)
            {
                if (storedBlacklist.ItemList.Count is 0)
                    SendMessage(player, BLLang("none"));
                else
                {
                    string _BLItems = string.Join(", ", storedBlacklist.ItemList);
                    SendMessage(player, BLLang("blocked", player.UserIDString, _BLItems));
                }

                return;
            }

            switch (args[0].ToLower())
            {
                case "additem":
                    if (!ItemExists(args[1]))
                    {
                        SendMessage(player, BLLang("notvalid", player.UserIDString, args[1]));
                        return;
                    }

                    if (!storedBlacklist.ItemList.Contains(args[1]))
                    {
                        storedBlacklist.ItemList.Add(args[1]);
                        UpdateInternals(false);
                        SendMessage(player, BLLang("blockedpass", player.UserIDString, args[1]));
                        DataSystem.SaveBlacklist();
                        return;
                    }

                    SendMessage(player, BLLang("blockedtrue", player.UserIDString, args[1]));
                    break;
                case "deleteitem":
                    if (!ItemExists(args[1]))
                    {
                        SendMessage(player, BLLang("notvalid", player.UserIDString, args[1]));
                        return;
                    }

                    if (storedBlacklist.ItemList.Contains(args[1]))
                    {
                        storedBlacklist.ItemList.Remove(args[1]);
                        UpdateInternals(false);
                        SendMessage(player, BLLang("unblacklisted", player.UserIDString, args[1]));
                        DataSystem.SaveBlacklist();
                        return;
                    }

                    SendMessage(player, BLLang("blockedfalse", player.UserIDString, args[1]));
                    break;
                default:
                    SendMessage(player, BLLang("syntax"));
                    break;
            }
        }
        #endregion

        #region Hammer loot cycle

        private void OnMeleeAttack(BasePlayer player, HitInfo c)
        {
            if (!_config.Loot.EnableHammerLootCycle || player is null || c is null)
                return;

            Item item = player.GetActiveItem();
            if (item?.info?.shortname is not string shortname || item.hasCondition || !player.IsAdmin)
                return;
            if (shortname.IndexOf("hammer", StringComparison.OrdinalIgnoreCase) < 0)
                return;

            BaseEntity entity = c.HitEntity;
            if (entity is null || entity.gameObject is null)
                return;

            if (entity is LootableCorpse)
            {
                _instance.SendMessage(player, "Cannot rotate loot directly on corpse.");
                return;
            }

            if (entity.GetComponent<LootContainer>() is not StorageContainer _inv || _inv.inventory is null)
                return;

            ItemContainer container = _inv.inventory;
            string panelName = _inv.panelName ?? string.Empty;

            container.capacity = 36; // For viewing purposes
            if (entity.gameObject.GetComponent<HammerHitLootCycle>() is null)
                entity.gameObject.AddComponent<HammerHitLootCycle>();


            player.inventory.loot.StartLootingEntity(entity, false);
            player.inventory.loot.AddContainer(container);
            player.inventory.loot.SendImmediate();
            player.ClientRPC(RpcTarget.Player("RPC_OpenLootPanel", player), panelName);
        }

        private class HammerHitLootCycle : FacepunchBehaviour
        {
            private bool RestoreCrateFlag;
            private LootContainer loot;

            private void Awake()
            {
                loot = GetComponent<LootContainer>();
                if (loot is null)
                {
                    Destroy(this);
                    return;
                }

                if (loot is HackableLockedCrate crate && (!crate.mapMarkerInstance?.IsDestroyed ?? false))
                {
                    RestoreCrateFlag = true;
                    crate.mapMarkerInstance.Kill();
                    crate.SendNetworkUpdateImmediate();
                }

                _instance.NextTick(() => InvokeRepeating(Repeater, 0, (float)_config.Loot.HammerLootCycleTime));
            }

            private void Repeater()
            {
                if (!enabled) 
                    return;

                if (loot is null)
                {
                    CancelInvoke(Repeater);
                    Destroy(this);
                    return;
                }

                _instance.PopulateContainer(loot);
                loot.inventory.capacity = 36; // For viewing purposes
            }

            private void PlayerStoppedLooting(BasePlayer _)
            {
                if (GetComponent<LootContainer>() is LootContainer container)
                {
                    container.inventory.capacity = container.inventory.itemList.Count;
                    container.inventory.MarkDirty();
                }

                CancelInvoke(Repeater);

                if (RestoreCrateFlag && loot is HackableLockedCrate crate)
                    crate.CreateMapMarker(120);

                Destroy(this);
            }
        }
        #endregion

        #region Ore Spawn System
        [ProtoBuf.ProtoContract]
        private sealed class OreSpawnStoredData
        {
            [ProtoBuf.ProtoMember(1)]
            public List<SavedOre> Ores { get; set; } = new List<SavedOre>();
        }

        [ProtoBuf.ProtoContract]
        private sealed class SavedOre
        {
            [ProtoBuf.ProtoMember(1)] public float X;
            [ProtoBuf.ProtoMember(2)] public float Y;
            [ProtoBuf.ProtoMember(3)] public float Z;
            [ProtoBuf.ProtoMember(4)] public string Prefab;
            [ProtoBuf.ProtoMember(5)] public byte Type;
            [ProtoBuf.ProtoMember(6)] public float GatherMultiplier;
            [ProtoBuf.ProtoMember(7)] public byte Biome;
        }

        private readonly struct OreSpawnPosition
        {
            public readonly Vector3 Position;
            public readonly OreType Type;
            public readonly string Prefab;
            public readonly TerrainBiome.Enum Biome;

            public OreSpawnPosition(Vector3 position, OreType type, string prefab, TerrainBiome.Enum biome)
            {
                Position = position;
                Type = type;
                Prefab = prefab;
                Biome = biome;
            }
        }

        private readonly struct OreNodeData
        {
            public readonly OreType Type;
            public readonly Vector3 Position;
            public readonly TerrainBiome.Enum Biome;
            public readonly float GatherMultiplier;
            public readonly string Prefab;

            public OreNodeData(OreType type, Vector3 position, TerrainBiome.Enum biome, float gatherMultiplier, string prefab)
            {
                Type = type;
                Position = position;
                Biome = biome;
                GatherMultiplier = gatherMultiplier;
                Prefab = prefab;
            }
        }

        private sealed class OreGatherSession
        {
            public float VanillaStartTotal;
            public readonly Dictionary<string, float> VanillaStartByItem = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, int> GivenByItem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> ExtraGrantedThisFrame = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public int LastFrame = -1;
            public string LastTool = "rock";
        }

        private enum OreType : byte
        {
            Stone = 0,
            Metal = 1,
            Sulfur = 2,
            HQM = 3,
            CollectStone = 4,
            CollectMetal = 5,
            CollectSulfur = 6,
            CollectWood = 7,
            CollectHemp = 8,
            CollectBerryBlack = 9,
            CollectBerryBlue = 10,
            CollectBerryGreen = 11,
            CollectBerryRed = 12,
            CollectBerryWhite = 13,
            CollectBerryYellow = 14,
            CollectMushroom = 15,
            CollectCorn = 16,
            CollectPotato = 17,
            CollectPumpkin = 18,
            CollectOrchid = 19,
            CollectRose = 20,
            CollectSunflower = 21,
            CollectWheat = 22,
            WoodPile = 23,
            Cactus = 24,
            Tree = 25,
            CollectHQM = 26,
            CollectCoconut = 27,
            CollectDiesel = 28,
            CollectHalloweenBone = 29,
            CollectHalloweenMetal = 30,
            CollectHalloweenStone = 31,
            CollectHalloweenSulfur = 32,
            CollectHalloweenWood = 33
        }

        private static readonly string[] OreTypeNames =
        {
            "Stone", "Metal", "Sulfur", "HQM",
            "Stone Pickup", "Metal Pickup", "Sulfur Pickup", "Wood Pickup", "Hemp",
            "Black Berry", "Blue Berry", "Green Berry", "Red Berry", "White Berry", "Yellow Berry",
            "Mushroom", "Corn", "Potato", "Pumpkin", "Orchid", "Rose", "Sunflower", "Wheat",
            "Wood Pile", "Cactus", "Tree", "HQM Pickup", "Coconut", "Diesel",
            "Halloween Bone", "Halloween Metal", "Halloween Stone", "Halloween Sulfur", "Halloween Wood"
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsOreNodeType(OreType type) => type is OreType.Stone or OreType.Metal or OreType.Sulfur or OreType.HQM;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHarvestType(OreType type) => type is OreType.WoodPile or OreType.Cactus or OreType.Tree;

        private enum OreShowKind : byte { All, Ore, Collectable, Harvest }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool MatchesShowKind(OreType type, OreShowKind kind) => kind switch
        {
            OreShowKind.Ore => IsOreNodeType(type),
            OreShowKind.Collectable => IsCollectableType(type),
            OreShowKind.Harvest => IsHarvestType(type),
            _ => true
        };

        private static string ShowKindLabel(OreShowKind kind) => kind switch
        {
            OreShowKind.Ore => "ores",
            OreShowKind.Collectable => "collectables",
            OreShowKind.Harvest => "wood piles, cactus, and trees",
            _ => "ores and collectables"
        };

        private static bool TryParseShowKind(string[] args, out OreShowKind kind)
        {
            kind = OreShowKind.All;
            if (args is null || args.Length == 0) return true;
            switch (args[0].Trim().ToLowerInvariant())
            {
                case "ore":
                case "ores":
                case "node":
                case "nodes":
                    kind = OreShowKind.Ore;
                    return true;
                case "collectable":
                case "collectables":
                case "collectible":
                case "collectibles":
                case "plant":
                case "plants":
                    kind = OreShowKind.Collectable;
                    return true;
                case "harvest":
                case "wood":
                case "tree":
                case "trees":
                    kind = OreShowKind.Harvest;
                    return true;
                case "all":
                    kind = OreShowKind.All;
                    return true;
                default:
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsNodeType(OreType type) => IsOreNodeType(type) || IsHarvestType(type);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsCollectableType(OreType type) => !IsNodeType(type);

        private OreTypeSettings GetOreTypeSettings(OreType type) => type switch
        {
            OreType.Stone => Ore.Stone,
            OreType.Metal => Ore.Metal,
            OreType.Sulfur => Ore.Sulfur,
            OreType.HQM => Ore.HQM,
            OreType.WoodPile => Ore.WoodPile,
            OreType.Cactus => Ore.Cactus,
            OreType.Tree => Ore.Tree,
            OreType.CollectStone => Ore.Collectables.Stone,
            OreType.CollectMetal => Ore.Collectables.Metal,
            OreType.CollectSulfur => Ore.Collectables.Sulfur,
            OreType.CollectWood => Ore.Collectables.Wood,
            OreType.CollectHemp => Ore.Collectables.Hemp,
            OreType.CollectBerryBlack => Ore.Collectables.BerryBlack,
            OreType.CollectBerryBlue => Ore.Collectables.BerryBlue,
            OreType.CollectBerryGreen => Ore.Collectables.BerryGreen,
            OreType.CollectBerryRed => Ore.Collectables.BerryRed,
            OreType.CollectBerryWhite => Ore.Collectables.BerryWhite,
            OreType.CollectBerryYellow => Ore.Collectables.BerryYellow,
            OreType.CollectMushroom => Ore.Collectables.Mushroom,
            OreType.CollectCorn => Ore.Collectables.Corn,
            OreType.CollectPotato => Ore.Collectables.Potato,
            OreType.CollectPumpkin => Ore.Collectables.Pumpkin,
            OreType.CollectOrchid => Ore.Collectables.Orchid,
            OreType.CollectRose => Ore.Collectables.Rose,
            OreType.CollectSunflower => Ore.Collectables.Sunflower,
            OreType.CollectWheat => Ore.Collectables.Wheat,
            OreType.CollectHQM => Ore.Collectables.HQM,
            OreType.CollectCoconut => Ore.Collectables.Coconut,
            OreType.CollectDiesel => Ore.Collectables.Diesel,
            OreType.CollectHalloweenBone => Ore.Collectables.HalloweenBone,
            OreType.CollectHalloweenMetal => Ore.Collectables.HalloweenMetal,
            OreType.CollectHalloweenStone => Ore.Collectables.HalloweenStone,
            OreType.CollectHalloweenSulfur => Ore.Collectables.HalloweenSulfur,
            OreType.CollectHalloweenWood => Ore.Collectables.HalloweenWood,
            _ => null
        };

        private IEnumerable<(OreType Type, OreTypeSettings Settings)> EnumerateResourceTypes()
        {
            yield return (OreType.Stone, Ore.Stone);
            yield return (OreType.Metal, Ore.Metal);
            yield return (OreType.Sulfur, Ore.Sulfur);
            yield return (OreType.HQM, Ore.HQM);
            yield return (OreType.WoodPile, Ore.WoodPile);
            yield return (OreType.Cactus, Ore.Cactus);
            yield return (OreType.Tree, Ore.Tree);
            yield return (OreType.CollectStone, Ore.Collectables.Stone);
            yield return (OreType.CollectMetal, Ore.Collectables.Metal);
            yield return (OreType.CollectSulfur, Ore.Collectables.Sulfur);
            yield return (OreType.CollectWood, Ore.Collectables.Wood);
            yield return (OreType.CollectHemp, Ore.Collectables.Hemp);
            yield return (OreType.CollectBerryBlack, Ore.Collectables.BerryBlack);
            yield return (OreType.CollectBerryBlue, Ore.Collectables.BerryBlue);
            yield return (OreType.CollectBerryGreen, Ore.Collectables.BerryGreen);
            yield return (OreType.CollectBerryRed, Ore.Collectables.BerryRed);
            yield return (OreType.CollectBerryWhite, Ore.Collectables.BerryWhite);
            yield return (OreType.CollectBerryYellow, Ore.Collectables.BerryYellow);
            yield return (OreType.CollectMushroom, Ore.Collectables.Mushroom);
            yield return (OreType.CollectCorn, Ore.Collectables.Corn);
            yield return (OreType.CollectPotato, Ore.Collectables.Potato);
            yield return (OreType.CollectPumpkin, Ore.Collectables.Pumpkin);
            yield return (OreType.CollectOrchid, Ore.Collectables.Orchid);
            yield return (OreType.CollectRose, Ore.Collectables.Rose);
            yield return (OreType.CollectSunflower, Ore.Collectables.Sunflower);
            yield return (OreType.CollectWheat, Ore.Collectables.Wheat);
            yield return (OreType.CollectHQM, Ore.Collectables.HQM);
            yield return (OreType.CollectCoconut, Ore.Collectables.Coconut);
            yield return (OreType.CollectDiesel, Ore.Collectables.Diesel);
            yield return (OreType.CollectHalloweenBone, Ore.Collectables.HalloweenBone);
            yield return (OreType.CollectHalloweenMetal, Ore.Collectables.HalloweenMetal);
            yield return (OreType.CollectHalloweenStone, Ore.Collectables.HalloweenStone);
            yield return (OreType.CollectHalloweenSulfur, Ore.Collectables.HalloweenSulfur);
            yield return (OreType.CollectHalloweenWood, Ore.Collectables.HalloweenWood);
        }

        private bool HasOrePermission(BasePlayer player, string perm)
        {
            if (player is null) return false;
            return permission.UserHasPermission(player.UserIDString, ADMIN_PERM)
                || permission.UserHasPermission(player.UserIDString, perm);
        }

        #region Vanilla Gather Capture
        private bool PopulateVanillaGatherDefaults(bool replaceExistingGather)
        {
            bool changed = false;
            Ore.Collectables ??= new OreCollectableConfiguration();

            foreach (var pair in EnumerateResourceTypes())
            {
                var settings = pair.Settings;
                if (settings == null || string.IsNullOrEmpty(settings.Prefab))
                    continue;

                try
                {
                    if (IsCollectableType(pair.Type))
                    {
                        if (settings.PickupOutputs is { Count: > 0 } && !replaceExistingGather)
                            continue;

                        var pickups = CapturePickupOutputsFromPrefab(settings.Prefab);
                        if (pickups.Count == 0)
                            continue;

                        settings.PickupOutputs = pickups;
                        changed = true;
                        continue;
                    }

                    if (settings.GatherOutputs is { Count: > 0 } && !replaceExistingGather)
                        continue;

                    var gather = CaptureGatherOutputsFromPrefab(settings.Prefab);
                    if (gather.Count == 0)
                        continue;

                    settings.GatherOutputs = gather;
                    changed = true;
                }
                catch (Exception ex)
                {
                    Log($"Could not read vanilla gather defaults from {settings.Prefab}: {ex.Message}");
                }
            }

            Ore.NpcHarvest ??= new Dictionary<string, NpcHarvestSettings>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in HarvestPrefabAliases)
            {
                if (group is not { Length: > 0 })
                    continue;

                string key = group[0];
                if (!replaceExistingGather
                    && Ore.NpcHarvest.TryGetValue(key, out var existing)
                    && existing?.GatherOutputs is { Count: > 0 })
                    continue;

                var harvest = CaptureNpcHarvestFromPrefabs(group);
                if (harvest.Count == 0)
                    continue;

                Ore.NpcHarvest[key] = new NpcHarvestSettings
                {
                    OverrideGatherAmounts = false,
                    GatherOutputs = harvest
                };
                changed = true;
            }

            foreach (string prefab in EnumerateScientistHarvestPrefabs())
            {
                if (!replaceExistingGather
                    && Ore.NpcHarvest.TryGetValue(prefab, out var existing)
                    && existing?.GatherOutputs is { Count: > 0 })
                    continue;

                var harvest = CaptureGatherOutputsFromPrefab(prefab);
                if (harvest.Count == 0)
                    harvest = CaptureGatherOutputsFromPrefab(prefab.Replace(".prefab", ".corpse.prefab", StringComparison.OrdinalIgnoreCase));
                if (harvest.Count == 0)
                    continue;

                Ore.NpcHarvest[prefab] = new NpcHarvestSettings
                {
                    OverrideGatherAmounts = false,
                    GatherOutputs = harvest
                };
                changed = true;
            }

            return changed;
        }

        private IEnumerable<string> EnumerateScientistHarvestPrefabs()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_config?.Generic?.WatchedPrefabs != null)
            {
                foreach (string prefab in _config.Generic.WatchedPrefabs.Keys)
                {
                    if (IsScientistHarvestPrefab(prefab) && seen.Add(prefab))
                        yield return prefab;
                }
            }

            if (lootTables?.LootTables == null)
                yield break;

            foreach (string prefab in lootTables.LootTables.Keys)
            {
                if (IsScientistHarvestPrefab(prefab) && seen.Add(prefab))
                    yield return prefab;
            }
        }

        private static bool IsScientistHarvestPrefab(string prefab)
        {
            if (string.IsNullOrEmpty(prefab))
                return false;

            var n = prefab.Replace('\\', '/');
            if (n.IndexOf("/m2bradley/", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("patrol helicopter", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("/scrap heli carrier/", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("/attackhelicopter/", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("/npc/gingerbread/", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("/npc/scarecrow/", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return n.IndexOf("humannpc", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("tunneldweller", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("underwaterdweller", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("npcplayer", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private List<OreGatherOutput> CaptureNpcHarvestFromPrefabs(string[] prefabs)
        {
            foreach (string prefab in prefabs)
            {
                var captured = CaptureGatherOutputsFromPrefab(prefab);
                if (captured.Count > 0)
                    return captured;
            }

            return new List<OreGatherOutput>();
        }

        private List<OrePickupOutput> CapturePickupOutputsFromPrefab(string prefabPath)
        {
            var list = new List<OrePickupOutput>();
            var collectible = FindPrefabComponent<CollectibleEntity>(prefabPath);
            if (collectible?.itemList == null)
                return list;

            foreach (var amount in collectible.itemList)
            {
                if (amount?.itemDef == null)
                    continue;

                var range = GetAmounts(amount);
                list.Add(new OrePickupOutput
                {
                    Shortname = amount.itemDef.shortname,
                    MinAmount = Math.Max(0, range.Min),
                    MaxAmount = Math.Max(Math.Max(0, range.Min), range.Max)
                });
            }

            return list;
        }

        private List<OreGatherOutput> CaptureGatherOutputsFromPrefab(string prefabPath)
        {
            var dispenser = FindPrefabComponent<ResourceDispenser>(prefabPath);
            if (dispenser == null)
                return new List<OreGatherOutput>();

            var totals = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            AddDispenserItemAmounts(totals, dispenser.containedItems);
            AddDispenserItemAmounts(totals, dispenser.finishBonus);
            if (totals.Count == 0)
                return new List<OreGatherOutput>();

            var tools = GetGatherTools(dispenser.gatherType);
            float maxHealth = GetDispenserMaxHealth(dispenser);
            var outputs = new List<OreGatherOutput>(totals.Count);

            foreach (var item in totals)
            {
                var amounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (string tool in tools)
                {
                    GetToolGatherStats(tool, dispenser.gatherType, maxHealth, out float gatherDamage, out float destroyFraction);
                    amounts[tool] = SimulateDispenserYield(item.Value, gatherDamage, destroyFraction, maxHealth);
                }

                outputs.Add(new OreGatherOutput
                {
                    Shortname = item.Key,
                    AmountsByTool = amounts
                });
            }

            return outputs;
        }

        private static void AddDispenserItemAmounts(Dictionary<string, float> totals, IEnumerable<ItemAmount>? amounts)
        {
            if (amounts == null)
                return;

            foreach (var amount in amounts)
            {
                var shortname = amount?.itemDef?.shortname;
                if (string.IsNullOrEmpty(shortname))
                    continue;

                float value = amount.amount;
                if (value <= 0f)
                    continue;

                if (!totals.ContainsKey(shortname))
                    totals[shortname] = 0f;
                totals[shortname] += value;
            }
        }

        private static string[] GetGatherTools(ResourceDispenser.GatherType gatherType) => gatherType switch
        {
            ResourceDispenser.GatherType.Tree => ChopGatherTools,
            ResourceDispenser.GatherType.Flesh => FleshGatherTools,
            _ => MineGatherTools
        };

        private static T FindPrefabComponent<T>(string prefabPath) where T : Component
        {
            if (string.IsNullOrEmpty(prefabPath))
                return null;

            var go = GameManager.server.FindPrefab(prefabPath);
            if (go == null)
                return null;

            return go.GetComponent<T>() ?? go.GetComponentInChildren<T>(true);
        }

        private static float GetDispenserMaxHealth(ResourceDispenser dispenser)
        {
            var entity = dispenser.baseEntity as BaseCombatEntity
                ?? dispenser.GetComponent<BaseCombatEntity>()
                ?? dispenser.GetComponentInParent<BaseCombatEntity>();
            if (entity != null)
            {
                float health = entity.MaxHealth();
                if (health > 0f)
                    return health;
            }

            return 100f;
        }

        private static void GetToolGatherStats(string shortname, ResourceDispenser.GatherType gatherType, float maxHealth, out float gatherDamage, out float destroyFraction)
        {
            gatherDamage = 0f;
            destroyFraction = 0f;

            var melee = FindItemMelee(shortname);
            var entry = melee?.GetGatherInfoFromIndex(gatherType);
            if (entry != null)
            {
                gatherDamage = entry.gatherDamage;
                destroyFraction = entry.destroyFraction;
            }

            if (gatherDamage <= 0f)
                gatherDamage = string.Equals(shortname, "jackhammer", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(shortname, "chainsaw", StringComparison.OrdinalIgnoreCase)
                    ? maxHealth
                    : maxHealth * 0.15f;
        }

        private static BaseMelee FindItemMelee(string shortname)
        {
            var def = ItemManager.FindItemDefinition(shortname);
            var held = def?.GetComponent<ItemModEntity>()?.entityPrefab?.Get();
            return held?.GetComponent<BaseMelee>() ?? held?.GetComponentInChildren<BaseMelee>(true);
        }

        private static int SimulateDispenserYield(float startAmount, float gatherDamage, float destroyFraction, float maxHealth)
        {
            if (startAmount <= 0f)
                return 0;
            if (maxHealth <= 0f || gatherDamage <= 0f)
                return Mathf.Max(0, Mathf.RoundToInt(startAmount));

            float remaining = startAmount;
            float health = maxHealth;
            float given = 0f;
            int hits = 0;
            while (health > 0.001f && remaining > 0.001f && hits++ < 256)
            {
                float fraction = Mathf.Min(1f, gatherDamage / maxHealth);
                float give = remaining * fraction;
                float destroy = remaining * destroyFraction * fraction;
                given += give;
                remaining = Mathf.Max(0f, remaining - give - destroy);
                health -= gatherDamage;
            }

            return Mathf.Max(0, Mathf.RoundToInt(given));
        }
        #endregion

        private static readonly Dictionary<string, Dictionary<string, int>> VanillaQuarryPerDiesel =
            new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
            {
                [QUARRY_HQM_KEY] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["hq.metal.ore"] = 50
                },
                [QUARRY_STONE_KEY] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["stones"] = 5000,
                    ["metal.ore"] = 1000
                },
                [QUARRY_SULFUR_KEY] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sulfur.ore"] = 1000
                },
                [QUARRY_EXCAVATOR_KEY] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["stones"] = 10000,
                    ["metal.fragments"] = 5000,
                    ["sulfur.ore"] = 2000,
                    ["hq.metal.ore"] = 100
                }
            };

        private bool EnsureQuarryHarvestDefaults()
        {
            bool changed = false;
            Ore.QuarryHarvest = Ore.QuarryHarvest == null
                ? new Dictionary<string, QuarryHarvestSettings>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, QuarryHarvestSettings>(Ore.QuarryHarvest, StringComparer.OrdinalIgnoreCase);

            foreach (var pair in VanillaQuarryPerDiesel)
            {
                if (Ore.QuarryHarvest.TryGetValue(pair.Key, out var existing) && existing?.Outputs is { Count: > 0 })
                    continue;

                var outputs = new List<QuarryHarvestOutput>(pair.Value.Count);
                foreach (var amount in pair.Value)
                    outputs.Add(new QuarryHarvestOutput { Shortname = amount.Key, Amount = amount.Value });

                Ore.QuarryHarvest[pair.Key] = new QuarryHarvestSettings
                {
                    OverrideGatherAmounts = existing?.OverrideGatherAmounts ?? false,
                    Outputs = outputs
                };
                changed = true;
            }

            return changed;
        }

        private void InitOreSpawnSystem()
        {
            Ore.NpcHarvest = Ore.NpcHarvest == null
                ? new Dictionary<string, NpcHarvestSettings>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, NpcHarvestSettings>(Ore.NpcHarvest, StringComparer.OrdinalIgnoreCase);

            try
            {
                bool vanillaChanged = PopulateVanillaGatherDefaults(_gatherRatesNeedsVanillaFill);
                bool quarryChanged = EnsureQuarryHarvestDefaults();
                if (vanillaChanged || quarryChanged)
                {
                    DataSystem.SaveOreSpawn();
                    if (vanillaChanged)
                        Log("Loaded vanilla gather, pickup, and harvest amounts from in-game prefabs into GatherRates.json");
                    if (quarryChanged)
                        Log("Added quarry and excavator diesel amounts to GatherRates.json");
                }
            }
            catch (Exception ex)
            {
                Log($"Could not load vanilla gather defaults from in-game prefabs: {ex.Message}");
            }
            _gatherRatesNeedsVanillaFill = false;
            NotifyGatherStandDownIfNeeded();
            _npcHarvestHitCache.Clear();
            _npcHarvestMissCache.Clear();
            _oreTypeCache.Clear();
            _oreTypeMissCache.Clear();

            if (TerrainMeta.HeightMap is null)
            {
                UpdateOreHookSubscriptions();
                return;
            }
            mapSize = TerrainMeta.Size.x;
            halfMapSize = mapSize * 0.5f;
            _cachedPlayerDistSqr = Ore.MinPlayerDistance * Ore.MinPlayerDistance;
            SpawnHandler_SpawnRepeating_Patch.ResetTracking();
            PopulateOrePrefabIds();

            if (!IsOreSystemEnabled())
            {
                Puts("Ore spawning is disabled. Turn on Custom spawning for a node or collectable in the Looty Editor in the Gather → Ore tab.");
                UpdateOreHookSubscriptions();
                return;
            }

            Ore.Collectables ??= new OreCollectableConfiguration();
            Ore.HQM ??= new OreTypeSettings
            {
                GridSize = 320f,
                Prefab = HQM_ORE_PREFAB,
                Enabled = false,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.08f, TemperateBiome = 0.06f, ArcticBiome = 0.12f, TundraBiome = 0.1f, JungleBiome = 0.15f }
            };
            Ore.WoodPile ??= new OreTypeSettings { GridSize = 220f, Prefab = WOOD_PILE_PREFAB };
            Ore.Cactus ??= new OreTypeSettings { GridSize = 120f, Prefab = CACTUS_PREFAB, Enabled = false };
            Ore.Tree ??= new OreTypeSettings { GridSize = 80f, Prefab = TREE_PREFAB, Enabled = false };
            StartOreWorld();
        }

        private void StartOreWorld()
        {
            Ore.Collectables ??= new OreCollectableConfiguration();
            Ore.HQM ??= new OreTypeSettings
            {
                GridSize = 320f,
                Prefab = HQM_ORE_PREFAB,
                Enabled = false,
                BiomeProbabilities = new OreBiomeProbabilities { AridBiome = 0.08f, TemperateBiome = 0.06f, ArcticBiome = 0.12f, TundraBiome = 0.1f, JungleBiome = 0.15f }
            };
            Ore.WoodPile ??= new OreTypeSettings { GridSize = 220f, Prefab = WOOD_PILE_PREFAB };
            Ore.Cactus ??= new OreTypeSettings { GridSize = 120f, Prefab = CACTUS_PREFAB, Enabled = false };
            Ore.Tree ??= new OreTypeSettings { GridSize = 80f, Prefab = TREE_PREFAB, Enabled = false };
            if (Ore.DisableNativeSpawning)
                PatchNestedClasses();

            LoadOreData();
            UpdateOreHookSubscriptions();

            cachedPlayerCount = BasePlayer.activePlayerList.Count;
            UpdatePopulationScalar();
            _oreSystemStarted = true;

            if (_oreStoredData.Ores.Count > 0)
            {
                StopSpawning();
                spawnCoroutine = ServerMgr.Instance.StartCoroutine(RestoreOresCoroutine());
            }
            else
            {
                if (Ore.DisableNativeSpawning)
                    CleanupNativeOres();
                StartSpawning();
            }
        }

        private void RestartOreSpawnSystem()
        {
            _npcHarvestHitCache.Clear();
            _npcHarvestMissCache.Clear();
            SpawnHandler_SpawnRepeating_Patch.ResetTracking();
            EnsureQuarryHarvestDefaults();
            StopSpawning();
            if (IsOreSystemEnabled())
            {
                if (_oreSystemStarted)
                    CleanupSpawnedOres();
                StartOreWorld();
            }
            else
            {
                UnsubscribeOreHooks();
                if (_oreSystemStarted)
                    CleanupSpawnedOres();
                _oreSystemStarted = false;
                UpdateOreHookSubscriptions();
                Puts("Ore spawning disabled. Native resource spawning is no longer suppressed.");
            }
        }

        private void ShutdownOreSpawnSystem()
        {
            StopSpawning();
            StopAllShowCoroutines();
            if (cleanupCoroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(cleanupCoroutine);
                cleanupCoroutine = null;
            }

            if (Ore.WipeOnUnload)
                CleanupSpawnedOres();
            else if (_oreSystemStarted)
                SaveOreData();

            CleanupAllMarkers();
            playerSpatialGrid.Clear();
            _corpseNpcPrefabs.Clear();
            _oreSystemStarted = false;
        }

        private void PopulateOrePrefabIds()
        {
            orePrefabIds.Clear();
            orePrefabIds.Add(StringPool.Get(STONE_ORE_PREFAB));
            orePrefabIds.Add(StringPool.Get(METAL_ORE_PREFAB));
            orePrefabIds.Add(StringPool.Get(SULFUR_ORE_PREFAB));
            orePrefabIds.Add(StringPool.Get("assets/bundled/prefabs/autospawn/resource/ores_sand/metal-ore.prefab"));
            orePrefabIds.Add(StringPool.Get("assets/bundled/prefabs/autospawn/resource/ores_sand/stone-ore.prefab"));
            orePrefabIds.Add(StringPool.Get("assets/bundled/prefabs/autospawn/resource/ores_sand/sulfur-ore.prefab"));
            orePrefabIds.Add(StringPool.Get("assets/bundled/prefabs/autospawn/resource/ores_snow/metal-ore.prefab"));
            orePrefabIds.Add(StringPool.Get("assets/bundled/prefabs/autospawn/resource/ores_snow/stone-ore.prefab"));
            orePrefabIds.Add(StringPool.Get("assets/bundled/prefabs/autospawn/resource/ores_snow/sulfur-ore.prefab"));
            orePrefabIds.Add(StringPool.Get(HQM_ORE_PREFAB));
            orePrefabIds.Add(StringPool.Get(WOOD_PILE_PREFAB));

            collectablePrefabIds.Clear();
            collectablePrefabIds.Add(StringPool.Get(STONE_COLLECTABLE_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(METAL_COLLECTABLE_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(SULFUR_COLLECTABLE_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(WOOD_COLLECTABLE_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(HEMP_COLLECTABLE_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(BERRY_BLACK_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(BERRY_BLUE_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(BERRY_GREEN_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(BERRY_RED_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(BERRY_WHITE_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(BERRY_YELLOW_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(MUSHROOM_CLUSTER_5_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(MUSHROOM_CLUSTER_6_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(CORN_COLLECTABLE_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(POTATO_COLLECTABLE_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(PUMPKIN_COLLECTABLE_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(ORCHID_COLLECTABLE_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(ROSE_COLLECTABLE_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(SUNFLOWER_COLLECTABLE_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(WHEAT_COLLECTABLE_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(HQM_COLLECTABLE_PREFAB));
            collectablePrefabIds.Add(StringPool.Get(COCONUT_COLLECTABLE_PREFAB));
        }

        private static bool IsOrePrefab(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return false;
            if (prefabName.Contains("halloween", StringComparison.OrdinalIgnoreCase)) return false;
            if (prefabName.Contains("collectablecandy", StringComparison.OrdinalIgnoreCase) ||
                prefabName.Contains("collectableegg", StringComparison.OrdinalIgnoreCase) ||
                prefabName.Contains("diesel_collectable", StringComparison.OrdinalIgnoreCase))
                return false;

            var isOre = prefabName.Contains("stone-ore") ||
                        prefabName.Contains("metal-ore") ||
                        prefabName.Contains("sulfur-ore") ||
                        prefabName.Contains("hqm-ore");
            if (isOre && !prefabName.Contains("collectable"))
                return true;

            if (prefabName.Contains("wood-pile") || prefabName.Contains("wood_log_pile"))
                return true;

            return prefabName.Contains("collectable") || prefabName.Contains("mushroom-cluster");
        }

        private void UpdateOreHookSubscriptions()
        {
            if (Ore.HQM.Enabled || Ore.WoodPile.Enabled || Ore.Cactus.Enabled || Ore.Tree.Enabled
                || Ore.RichVein.Enabled || HasAnyNodeBonusLoot() || HasAnyNodeGatherOverride() || HasAnyNpcHarvestOverride())
            {
                Subscribe(nameof(OnDispenserGather));
                Subscribe(nameof(OnDispenserBonus));
            }
            else
            {
                Unsubscribe(nameof(OnDispenserGather));
                Unsubscribe(nameof(OnDispenserBonus));
            }

            if (HasAnyQuarryHarvestOverride())
            {
                Subscribe(nameof(OnQuarryGather));
                Subscribe(nameof(OnExcavatorGather));
            }
            else
            {
                Unsubscribe(nameof(OnQuarryGather));
                Unsubscribe(nameof(OnExcavatorGather));
            }

            if (HasAnyCollectableEnabled() || HasAnyCollectableBonusLoot() || HasAnyCollectablePickupOverride())
            {
                Subscribe(nameof(OnCollectiblePickup));
            }
            else
            {
                Unsubscribe(nameof(OnCollectiblePickup));
            }

            if (HasAnyCollectableEnabled() || HasAnyCollectableBonusLoot() || HasAnyCollectablePickupOverride() || HasAnyNpcHarvestOverride() || HasAnyNodeGatherOverride())
            {
                Subscribe(nameof(OnEntityKill));
            }
            else
            {
                Unsubscribe(nameof(OnEntityKill));
            }

            if (Ore.DisableNativeSpawning && HasAnyResourceEnabled())
                Subscribe(nameof(OnEntitySpawned));
            else
                Unsubscribe(nameof(OnEntitySpawned));
        }

        private void UnsubscribeOreHooks()
        {
            Unsubscribe(nameof(OnDispenserGather));
            Unsubscribe(nameof(OnDispenserBonus));
            Unsubscribe(nameof(OnQuarryGather));
            Unsubscribe(nameof(OnExcavatorGather));
            Unsubscribe(nameof(OnCollectiblePickup));
            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void PatchNestedClasses()
        {
            if (_harmonyPatchesApplied) return;

            foreach (var type in typeof(BetterLoot).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public))
            {
                var harmonyPatchAttributes = type.GetCustomAttributes(typeof(HarmonyPatch), true);
                if (harmonyPatchAttributes is not { Length: > 0 }) continue;
                try 
                { 
                    HarmonyInstance.CreateClassProcessor(type).Patch();
                }
                catch (Exception ex) 
                { 
                    PrintError($"[HARMONY] Failed to patch {type.Name}: {ex.Message}"); 
                }
            }

            _harmonyPatchesApplied = true;
        }

        private bool HasAnyBonusLoot()
        {
            foreach (var pair in EnumerateResourceTypes())
            {
                if (pair.Settings?.BonusLoot is { Count: > 0 })
                    return true;
            }
            return false;
        }

        private bool HasAnyNodeBonusLoot()
        {
            return Ore.Stone.BonusLoot.Count > 0
                || Ore.Metal.BonusLoot.Count > 0
                || Ore.Sulfur.BonusLoot.Count > 0
                || Ore.HQM.BonusLoot.Count > 0
                || Ore.WoodPile.BonusLoot.Count > 0
                || Ore.Cactus.BonusLoot.Count > 0
                || Ore.Tree.BonusLoot.Count > 0;
        }

        private bool HasAnyNodeGatherOverride()
        {
            foreach (var pair in EnumerateResourceTypes())
            {
                if (!IsNodeType(pair.Type) || pair.Settings is null) continue;
                if (pair.Settings.OverrideGatherAmounts && pair.Settings.GatherOutputs is { Count: > 0 })
                    return true;
                if (HasCustomGatherMultiplier(pair.Settings))
                    return true;
            }
            return false;
        }

        private static float GetConfiguredGatherMultiplier(OreTypeSettings settings)
        {
            if (settings == null)
                return 1f;
            var n = settings.GatherMultiplier;
            if (n <= 0f)
                return 1f;
            return Mathf.Clamp(n, 0.01f, 100f);
        }

        private static bool HasCustomGatherMultiplier(OreTypeSettings settings)
        {
            return Mathf.Abs(GetConfiguredGatherMultiplier(settings) - 1f) > 0.0001f;
        }

        private static float CombineGatherScale(OreTypeSettings settings, OreNodeData oreData)
        {
            var configured = GetConfiguredGatherMultiplier(settings);
            var rich = oreData.GatherMultiplier > 0f ? oreData.GatherMultiplier : 1f;
            return configured * rich;
        }

        private bool HasAnyNpcHarvestOverride()
        {
            if (Ore.NpcHarvest == null || Ore.NpcHarvest.Count == 0) return false;
            foreach (var harvest in Ore.NpcHarvest.Values)
            {
                if (harvest != null && harvest.OverrideGatherAmounts && harvest.GatherOutputs is { Count: > 0 })
                    return true;
            }
            return false;
        }

        private bool HasAnyQuarryHarvestOverride()
        {
            if (Ore.QuarryHarvest == null || Ore.QuarryHarvest.Count == 0) return false;
            foreach (var harvest in Ore.QuarryHarvest.Values)
            {
                if (harvest != null && harvest.OverrideGatherAmounts && harvest.Outputs is { Count: > 0 })
                    return true;
            }
            return false;
        }

        private bool HasAnyCollectablePickupOverride()
        {
            foreach (var pair in EnumerateResourceTypes())
            {
                if (!IsCollectableType(pair.Type) || pair.Settings is null) continue;
                if (pair.Settings.OverridePickupAmounts && pair.Settings.PickupOutputs is { Count: > 0 })
                    return true;
                if (pair.Settings.PickupMaxAmount > 0)
                    return true;
            }
            return false;
        }

        private static bool HasPickupOutputOverride(OreTypeSettings settings)
        {
            return settings != null && settings.OverridePickupAmounts && settings.PickupOutputs is { Count: > 0 };
        }

        private bool HasAnyCollectableBonusLoot()
        {
            foreach (var pair in EnumerateResourceTypes())
            {
                if (IsCollectableType(pair.Type) && pair.Settings?.BonusLoot is { Count: > 0 })
                    return true;
            }
            return false;
        }

        private bool HasAnyCollectableEnabled()
        {
            foreach (var pair in EnumerateResourceTypes())
            {
                if (IsCollectableType(pair.Type) && pair.Settings is { Enabled: true })
                    return true;
            }
            return false;
        }

        private bool HasAnyResourceEnabled()
        {
            foreach (var pair in EnumerateResourceTypes())
            {
                if (pair.Settings is { Enabled: true })
                    return true;
            }
            return false;
        }

        private bool IsOreSystemEnabled() =>
            Ore.PerResourceSpawning ? HasAnyResourceEnabled() : Ore.Enabled;

        private bool IsResourceCustomEnabled(OreType type)
        {
            var settings = GetOreTypeSettings(type);
            return settings is { Enabled: true };
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player is null) return;
            UpdatePlayerSpatialGrid(player, true);
            SchedulePopulationCheck();
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player is null) return;

            UpdatePlayerSpatialGrid(player, false);
            StopShowCoroutine(player.userID);

            if (playerMarkers.TryGetValue(player.userID, out var markers))
            {
                foreach (var marker in markers)
                    marker.Dispose();
                playerMarkers.Remove(player.userID);
            }

            SchedulePopulationCheck();
        }

        #endregion

        #region Population Scaling
        private void SchedulePopulationCheck()
        {
            if (!Ore.PopulationScaling.Enabled) return;
            if (Time.realtimeSinceStartup - lastPopulationCheck < POPULATION_CHECK_INTERVAL) return;

            lastPopulationCheck = Time.realtimeSinceStartup;
            var newCount = BasePlayer.activePlayerList.Count;

            if (newCount == cachedPlayerCount) return;
            cachedPlayerCount = newCount;

            var oldScalar = currentGridScalar;
            UpdatePopulationScalar();

            if (Mathf.Abs(oldScalar - currentGridScalar) > 0.05f && Ore.Debug)
                Puts($"Population scalar changed: {oldScalar:F2} -> {currentGridScalar:F2} (Players: {cachedPlayerCount})");
        }

        private void UpdatePopulationScalar()
        {
            if (!Ore.PopulationScaling.Enabled)
            {
                currentGridScalar = 1f;
                return;
            }

            var settings = Ore.PopulationScaling;

            if (cachedPlayerCount <= settings.MinPlayers)
            {
                currentGridScalar = settings.MinPlayerScalar;
            }
            else if (cachedPlayerCount >= settings.MaxPlayers)
            {
                currentGridScalar = settings.MaxPlayerScalar;
            }
            else
            {
                var t = (float)(cachedPlayerCount - settings.MinPlayers) / (settings.MaxPlayers - settings.MinPlayers);
                currentGridScalar = Mathf.Lerp(settings.MinPlayerScalar, settings.MaxPlayerScalar, t);
            }
        }
        #endregion

        #region Spatial Hashing
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetPlayerGridKey(Vector3 position)
        {
            var x = Mathf.FloorToInt(position.x / PLAYER_GRID_SIZE);
            var z = Mathf.FloorToInt(position.z / PLAYER_GRID_SIZE);
            return (x << 16) | (z & 0xFFFF);
        }

        private void UpdatePlayerSpatialGrid(BasePlayer player, bool add)
        {
            var key = GetPlayerGridKey(player.transform.position);

            if (add)
            {
                if (!playerSpatialGrid.TryGetValue(key, out var set))
                {
                    set = new HashSet<ulong>();
                    playerSpatialGrid[key] = set;
                }
                set.Add(player.userID);
            }
            else
            {
                if (!playerSpatialGrid.TryGetValue(key, out var set)) return;
                set.Remove(player.userID);
                if (set.Count == 0)
                    playerSpatialGrid.Remove(key);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsNearAnyPlayerFast(Vector3 position, float minDistSqr)
        {
            var centerX = Mathf.FloorToInt(position.x / PLAYER_GRID_SIZE);
            var centerZ = Mathf.FloorToInt(position.z / PLAYER_GRID_SIZE);
            var searchRadius = Mathf.CeilToInt(Ore.MinPlayerDistance / PLAYER_GRID_SIZE) + 1;

            for (var x = centerX - searchRadius; x <= centerX + searchRadius; x++)
            {
                for (var z = centerZ - searchRadius; z <= centerZ + searchRadius; z++)
                {
                    var key = (x << 16) | (z & 0xFFFF);
                    if (!playerSpatialGrid.TryGetValue(key, out var playerIds)) continue;

                    foreach (var userId in playerIds)
                    {
                        var player = RelationshipManager.FindByID(userId);
                        if (player is null || !player.IsConnected) continue;

                        var dx = position.x - player.transform.position.x;
                        var dy = position.y - player.transform.position.y;
                        var dz = position.z - player.transform.position.z;

                        if (dx * dx + dy * dy + dz * dz < minDistSqr)
                            return true;
                    }
                }
            }
            return false;
        }
        #endregion

        #region Data Persistence
        private void LoadOreData()
        {
            try
            {
                _oreStoredData = ProtoStorage.Load<OreSpawnStoredData>(ORE_DATA_FILE);
                if (_oreStoredData is null || _oreStoredData.Ores.Count == 0)
                {
                    var legacy = Interface.Oxide.DataFileSystem.ReadObject<OreSpawnStoredData>("OreSpawnController");
                    if (legacy is { Ores: { Count: > 0 } })
                        _oreStoredData = legacy;
                }
            }
            catch
            {
                _oreStoredData = new OreSpawnStoredData();
            }

            _oreStoredData ??= new OreSpawnStoredData();
        }

        private void SaveOreData()
        {
            _oreStoredData.Ores.Clear();

            foreach (var kvp in spawnedOres)
            {
                if (kvp.Key is null || kvp.Key.IsDestroyed) continue;

                _oreStoredData.Ores.Add(new SavedOre
                {
                    X = kvp.Value.Position.x,
                    Y = kvp.Value.Position.y,
                    Z = kvp.Value.Position.z,
                    Prefab = kvp.Value.Prefab,
                    Type = (byte)kvp.Value.Type,
                    GatherMultiplier = kvp.Value.GatherMultiplier,
                    Biome = (byte)kvp.Value.Biome
                });
            }

            ProtoStorage.Save(_oreStoredData, ORE_DATA_FILE);

            if (Ore.Debug)
                Puts($"Saved {_oreStoredData.Ores.Count} ore positions to data file");
        }

        private IEnumerator RestoreOresCoroutine()
        {
            yield return null;

            var restored = 0;
            var failed = 0;
            var batchCount = 0;
            var wait = CoroutineEx.waitForSeconds(Ore.SpawnDelay);

            foreach (var saved in _oreStoredData.Ores)
            {
                if (!Enum.IsDefined(typeof(OreType), saved.Type))
                {
                    failed++;
                    continue;
                }

                var oreType = (OreType)saved.Type;
                if (!IsResourceCustomEnabled(oreType))
                    continue;

                var position = new Vector3(saved.X, saved.Y, saved.Z);
                position.y = TerrainMeta.HeightMap.GetHeight(position);

                var existing = FindResourceEntityNear(position, saved.Prefab, oreType);
                if (existing is not null)
                {
                    if (!spawnedOres.ContainsKey(existing))
                        TrackResourceEntity(existing, oreType, saved.GatherMultiplier);
                    restored++;
                }
                else
                {
                    var entity = GameManager.server.CreateEntity(saved.Prefab, position);
                    if (entity is null && oreType == OreType.HQM)
                        entity = GameManager.server.CreateEntity(METAL_ORE_PREFAB, position);
                    if (entity is null)
                    {
                        failed++;
                    }
                    else
                    {
                        entity.Spawn();
                        TrackResourceEntity(entity, oreType, saved.GatherMultiplier, saved.Prefab, (TerrainBiome.Enum)saved.Biome);
                        restored++;
                    }
                }

                if (++batchCount >= Ore.BatchSize)
                {
                    batchCount = 0;
                    yield return wait;
                }
            }

            _oreStoredData.Ores.Clear();

            if (Ore.DisableNativeSpawning)
                CleanupNativeOres();

            if (Ore.Debug)
                Puts($"Restored {restored} ores from data ({failed} failed). Tracked: {spawnedOres.Count}");

            yield return CoroutineEx.waitForSeconds(2f);
            RemoveFloatingOres();
        }
        #endregion

        #region Commands
        private const string ShowUsage = "Usage: <color=#ffb347>/blshow ore</color> or <color=#ffb347>/blshow collectables</color> (also <color=#ffb347>/betterlootshow</color>)";

        [ChatCommand("blshow")]
        private void CmdBlShow(BasePlayer player, string command, string[] args) => CmdNodesShow(player, command, args);

        [ChatCommand("betterlootshow")]
        private void CmdBetterLootShow(BasePlayer player, string command, string[] args) => CmdNodesShow(player, command, args);

        [ChatCommand("oreshow")]
        private void CmdNodesShow(BasePlayer player, string command, string[] args)
        {
            if (!HasOrePermission(player, ORE_PERM_SHOW))
            {
                PrintToChat(player, "You don't have permission to use this command.");
                return;
            }
            if (!TryParseShowKind(args, out var kind))
            {
                PrintToChat(player, ShowUsage);
                return;
            }
            StopShowCoroutine(player.userID);
            activeShowCoroutines[player.userID] = ServerMgr.Instance.StartCoroutine(ShowNodesCoroutine(player, kind));
        }

        [ChatCommand("orehide")]
        private void CmdNodesHide(BasePlayer player, string command, string[] args) => CmdBlHide(player, command, args);

        [ChatCommand("blhide")]
        private void CmdBlHide(BasePlayer player, string command, string[] args)
        {
            if (!HasOrePermission(player, ORE_PERM_SHOW))
            {
                PrintToChat(player, "You don't have permission to use this command.");
                return;
            }
            ClearOreMarkersForPlayer(player);
            SendClearPingsToPlayer(player);
            PrintToChat(player, "Ore markers cleared from your map.");
        }

        [ChatCommand("betterloothide")]
        private void CmdBetterLootHide(BasePlayer player, string command, string[] args) => CmdBlHide(player, command, args);

        [ChatCommand("orehelp")]
        private void CmdOreHelp(BasePlayer player, string command, string[] args) => CmdBlHelp(player, command, args);

        [ChatCommand("blhelp")]
        private void CmdBlHelp(BasePlayer player, string command, string[] args)
        {
            if (!HasOrePermission(player, ORE_PERM_SHOW))
            {
                PrintToChat(player, "You don't have permission to use this command.");
                return;
            }
            
            var sb = Pool.Get<StringBuilder>();
            sb.Clear();
            sb.AppendLine("=== <color=#4d94ff>BetterLoot Ore Commands</color> ===");
            sb.AppendLine("Configure ores and collectables in the Looty editor Ores tab, then deploy with the Looty command.");
            sb.AppendLine("<color=#ffb347>/blshow ore</color> - Ping nearby ore nodes on your map for 30 seconds.");
            sb.AppendLine("<color=#ffb347>/blshow collectables</color> - Ping nearby collectables on your map for 30 seconds.");
            sb.AppendLine("<color=#ffb347>/betterlootshow</color> - Same as /blshow.");
            sb.AppendLine("<color=#ffb347>/oreshow</color> - Ping both (same as /blshow all).");
            sb.AppendLine("<color=#ffb347>/blhide</color> - Hide those markers from map.");
            PrintToChat(player, sb.ToString());
            Pool.FreeUnmanaged(ref sb);
        }

        [ChatCommand("betterloothelp")]
        private void CmdBetterLootHelp(BasePlayer player, string command, string[] args) => CmdBlHelp(player, command, args);
        #endregion

        #region Core Spawning System
        private void StartSpawning()
        {
            StopSpawning();
            RebuildPlayerSpatialGrid();
            spawnCoroutine = ServerMgr.Instance.StartCoroutine(SpawnOresCoroutine());
        }

        private void RebuildPlayerSpatialGrid()
        {
            playerSpatialGrid.Clear();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player is not null && player.IsConnected)
                    UpdatePlayerSpatialGrid(player, true);
            }
        }

        private void StopSpawning()
        {
            if (spawnCoroutine == null) return;
            ServerMgr.Instance.StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        private const int GENERATE_YIELD_EVERY = 200;

        private IEnumerator GeneratePositionsForOre(OreTypeSettings settings, OreType oreType)
        {
            if (Ore.GlobalSpawnMultiplier <= 0.01f) yield break;
            var densityMult = Mathf.Sqrt(Mathf.Max(0.05f, Ore.GlobalSpawnMultiplier));
            var scaledGridSize = settings.GridSize * currentGridScalar / densityMult;
            var maxOffset = scaledGridSize * 0.4f;

            var evaluated = 0;
            for (var x = -halfMapSize; x < halfMapSize; x += scaledGridSize)
            {
                for (var z = -halfMapSize; z < halfMapSize; z += scaledGridSize)
                {
                    var position = new Vector3(
                        x + UnityEngine.Random.Range(-maxOffset, maxOffset),
                        0f,
                        z + UnityEngine.Random.Range(-maxOffset, maxOffset)
                    );

                    if (IsValidOreSpawnPosition(position, settings, out var adjustedPos, out var biome) &&
                        !(Ore.NodeDensity < 0.999f && UnityEngine.Random.Range(0f, 1f) > Ore.NodeDensity))
                    {
                        spawnQueue.Enqueue(new OreSpawnPosition(adjustedPos, oreType, settings.Prefab, biome));
                    }

                    if (++evaluated >= GENERATE_YIELD_EVERY)
                    {
                        evaluated = 0;
                        yield return null;
                    }
                }
            }
        }

        private bool IsValidOreSpawnPosition(Vector3 position, OreTypeSettings settings, out Vector3 adjustedPos, out TerrainBiome.Enum biome)
        {
            adjustedPos = position;
            biome = TerrainBiome.Enum.Arid;

            if (position.x < -halfMapSize || position.x > halfMapSize || 
                position.z < -halfMapSize || position.z > halfMapSize) 
                return false;

            var terrainHeight = TerrainMeta.HeightMap.GetHeight(position);
            adjustedPos.y = terrainHeight;

            if (adjustedPos.y < 1f)
                return false;

            var waterInfo = WaterLevel.GetWaterInfo(adjustedPos, waves: false, volumes: true);
            if (waterInfo.isValid && adjustedPos.y <= waterInfo.surfaceLevel + Ore.WaterBuffer)
                return false;

            if (WaterSystem.Collision is not null && WaterSystem.Collision.GetIgnore(adjustedPos))
                return false;

            if (TerrainMeta.AlphaMap is not null)
            {
                var alpha = TerrainMeta.AlphaMap.GetAlpha(position);
                if (alpha < 0.5f) return false;
            }

            var topology = TerrainMeta.TopologyMap.GetTopology(position);
            const int BLOCKED_TOPOLOGY = TerrainTopology.RIVER | TerrainTopology.LAKE | TerrainTopology.SWAMP |
                                         TerrainTopology.OCEAN | TerrainTopology.MONUMENT | TerrainTopology.ROADSIDE |
                                         TerrainTopology.ROAD | TerrainTopology.RAIL | TerrainTopology.RAILSIDE |
                                         TerrainTopology.CLIFF | TerrainTopology.OFFSHORE | TerrainTopology.BUILDING |
                                         TerrainTopology.DECOR | TerrainTopology.RIVERSIDE | TerrainTopology.LAKESIDE |
                                         TerrainTopology.BEACH | TerrainTopology.OCEANSIDE;

            if ((topology & BLOCKED_TOPOLOGY) != 0) 
                return false;

            if (TerrainMeta.HeightMap.GetSlope(position) > Ore.MaxSlope) 
                return false;

            biome = (TerrainBiome.Enum)TerrainMeta.BiomeMap.GetBiomeMaxType(position);
            var probability = GetBiomeProbability(settings.BiomeProbabilities, biome);

            if (UnityEngine.Random.Range(0f, 1f) > probability) 
                return false;

            return !IsNearPlayersOrBuildings(adjustedPos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsNearPlayersOrBuildings(Vector3 position)
            => IsNearAnyPlayerFast(position, _cachedPlayerDistSqr) || IsNearAnyBuilding(position);

        private bool IsNearAnyBuilding(Vector3 position)
        {
            var colliders = Pool.Get<List<Collider>>();
            Vis.Colliders(position, Ore.MinBuildingDistance, colliders, Layers.Mask.Construction, QueryTriggerInteraction.Ignore);
            var hasBuilding = colliders.Count > 0;
            Pool.FreeUnmanaged(ref colliders);
            return hasBuilding;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetBiomeProbability(OreBiomeProbabilities probs, TerrainBiome.Enum biome) => biome switch
        {
            TerrainBiome.Enum.Arid => probs.AridBiome,
            TerrainBiome.Enum.Temperate => probs.TemperateBiome,
            TerrainBiome.Enum.Arctic => probs.ArcticBiome,
            TerrainBiome.Enum.Tundra => probs.TundraBiome,
            TerrainBiome.Enum.Jungle => probs.JungleBiome,
            _ => 0f
        };

        private IEnumerator SpawnOresCoroutine()
        {
            yield return null;

            spawnQueue.Clear();
            foreach (var pair in EnumerateResourceTypes())
            {
                if (pair.Settings is { Enabled: true })
                    yield return GeneratePositionsForOre(pair.Settings, pair.Type);
            }

            if (Ore.Debug)
                Puts($"Generated {spawnQueue.Count} potential spawn positions (Grid scalar: {currentGridScalar:F2})");

            var batchCount = 0;
            var wait = CoroutineEx.waitForSeconds(Ore.SpawnDelay);

            while (spawnQueue.Count > 0)
            {
                SpawnOre(spawnQueue.Dequeue());
                batchCount++;

                if (batchCount < Ore.BatchSize) continue;
                yield return wait;
                batchCount = 0;
            }

            if (Ore.Debug)
                Puts($"Spawning completed. Total ores: {spawnedOres.Count}");

            yield return CoroutineEx.waitForSeconds(1f);
            RemoveFloatingOres();
        }

        private void SpawnOre(OreSpawnPosition spawnData)
        {
            var prefab = spawnData.Prefab;
            if (spawnData.Type == OreType.CollectMushroom)
                prefab = UnityEngine.Random.Range(0, 2) == 0 ? MUSHROOM_CLUSTER_5_PREFAB : MUSHROOM_CLUSTER_6_PREFAB;

            var entity = GameManager.server.CreateEntity(prefab, spawnData.Position);
            if (entity is null && spawnData.Type == OreType.HQM)
                entity = GameManager.server.CreateEntity(METAL_ORE_PREFAB, spawnData.Position);
            if (entity is null) return;

            entity.Spawn();

            var gatherMultiplier = 1f;
            if (IsNodeType(spawnData.Type) && Ore.RichVein.Enabled && UnityEngine.Random.Range(0f, 1f) < Ore.RichVein.Chance)
                gatherMultiplier = Ore.RichVein.Multiplier;

            TrackResourceEntity(entity, spawnData.Type, gatherMultiplier, prefab, spawnData.Biome);

            if (Ore.Debug && gatherMultiplier > 1f)
                Puts($"Spawned RICH {spawnData.Type} vein at {spawnData.Position:F1} (x{gatherMultiplier})");
        }

        private void TrackResourceEntity(BaseEntity entity, OreType type, float gatherMultiplier, string prefab = null, TerrainBiome.Enum? biome = null)
        {
            if (entity is null || entity.IsDestroyed || spawnedOres.ContainsKey(entity)) return;
            var position = entity.transform.position;
            var resolvedBiome = biome ?? (TerrainMeta.BiomeMap is not null
                ? (TerrainBiome.Enum)TerrainMeta.BiomeMap.GetBiomeMaxType(position)
                : TerrainBiome.Enum.Temperate);
            spawnedOres[entity] = new OreNodeData(type, position, resolvedBiome, gatherMultiplier, prefab ?? entity.PrefabName);
            IncrementOreCount(type, gatherMultiplier);
        }

        private BaseEntity FindResourceEntityNear(Vector3 position, string prefab, OreType expectedType)
        {
            var list = Pool.Get<List<BaseEntity>>();
            Vis.Entities(position, 2f, list, -1, QueryTriggerInteraction.Collide);
            BaseEntity found = null;
            foreach (var worldEntity in list)
            {
                if (worldEntity is null || worldEntity.IsDestroyed) continue;
                if (!string.IsNullOrEmpty(prefab) && string.Equals(worldEntity.PrefabName, prefab, StringComparison.OrdinalIgnoreCase))
                {
                    found = worldEntity;
                    break;
                }
                if (InferOreTypeFromPrefab(worldEntity.PrefabName) == expectedType)
                {
                    found = worldEntity;
                    break;
                }
            }
            Pool.FreeUnmanaged(ref list);
            return found;
        }

        private int AdoptUntrackedWorldResources()
        {
            var adopted = 0;
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is not BaseEntity { IsDestroyed: false } worldEntity) continue;
                if (spawnedOres.ContainsKey(worldEntity)) continue;
                var inferred = InferOreTypeFromPrefab(worldEntity.PrefabName);
                if (!inferred.HasValue) continue;
                if (Ore.PerResourceSpawning)
                {
                    if (!IsResourceCustomEnabled(inferred.Value)) continue;
                }
                else if (!Ore.Enabled)
                {
                    continue;
                }

                TrackResourceEntity(worldEntity, inferred.Value, 1f);
                adopted++;
            }
            return adopted;
        }

        private List<OreNodeData> CollectWorldResourceMarkers(OreShowKind kind = OreShowKind.All)
        {
            var markers = new List<OreNodeData>();
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is not BaseEntity { IsDestroyed: false } worldEntity) continue;
                if (spawnedOres.ContainsKey(worldEntity)) continue;
                var inferred = InferOreTypeFromPrefab(worldEntity.PrefabName);
                if (!inferred.HasValue || !MatchesShowKind(inferred.Value, kind)) continue;
                var biome = TerrainMeta.BiomeMap is not null
                    ? (TerrainBiome.Enum)TerrainMeta.BiomeMap.GetBiomeMaxType(worldEntity.transform.position)
                    : TerrainBiome.Enum.Temperate;
                markers.Add(new OreNodeData(inferred.Value, worldEntity.transform.position, biome, 1f, worldEntity.PrefabName));
            }
            return markers;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void IncrementOreCount(OreType type, float gatherMultiplier)
        {
            switch (type)
            {
                case OreType.Stone: _trackedStoneCount++; break;
                case OreType.Metal: _trackedMetalCount++; break;
                case OreType.Sulfur: _trackedSulfurCount++; break;
                case OreType.HQM: _trackedHqmCount++; break;
                case OreType.WoodPile:
                case OreType.Cactus:
                case OreType.Tree:
                    _trackedHarvestCount++;
                    break;
                default:
                    if (IsCollectableType(type))
                        _trackedCollectableCount++;
                    break;
            }
            if (gatherMultiplier > 1f) _trackedRichCount++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecrementOreCount(OreType type, float gatherMultiplier)
        {
            switch (type)
            {
                case OreType.Stone: _trackedStoneCount = Mathf.Max(0, _trackedStoneCount - 1); break;
                case OreType.Metal: _trackedMetalCount = Mathf.Max(0, _trackedMetalCount - 1); break;
                case OreType.Sulfur: _trackedSulfurCount = Mathf.Max(0, _trackedSulfurCount - 1); break;
                case OreType.HQM: _trackedHqmCount = Mathf.Max(0, _trackedHqmCount - 1); break;
                case OreType.WoodPile:
                case OreType.Cactus:
                case OreType.Tree:
                    _trackedHarvestCount = Mathf.Max(0, _trackedHarvestCount - 1);
                    break;
                default:
                    if (IsCollectableType(type))
                        _trackedCollectableCount = Mathf.Max(0, _trackedCollectableCount - 1);
                    break;
            }
            if (gatherMultiplier > 1f) _trackedRichCount = Mathf.Max(0, _trackedRichCount - 1);
        }

        private void ResetOreCounts()
        {
            _trackedStoneCount = _trackedMetalCount = _trackedSulfurCount = _trackedHqmCount = _trackedCollectableCount = _trackedHarvestCount = _trackedRichCount = 0;
        }

        private const float MAX_FLOAT_DISTANCE = 1.5f;

        private void RemoveFloatingOres()
        {
            var toRemove = Pool.Get<List<BaseEntity>>();
            var removed = 0;

            foreach (var kvp in spawnedOres)
            {
                var entity = kvp.Key;
                if (entity is null || entity.IsDestroyed)
                {
                    toRemove.Add(entity);
                    continue;
                }

                var pos = entity.transform.position;
                var terrainHeight = TerrainMeta.HeightMap.GetHeight(pos);
                var distAboveGround = pos.y - terrainHeight;

                if (distAboveGround > MAX_FLOAT_DISTANCE)
                {
                    toRemove.Add(entity);
                    removed++;
                    continue;
                }

                var waterInfo = WaterLevel.GetWaterInfo(pos, waves: false, volumes: true);
                if (!waterInfo.isValid || !(pos.y <= waterInfo.surfaceLevel + Ore.WaterBuffer)) continue;
                toRemove.Add(entity);
                removed++;
            }

            foreach (var entity in toRemove)
            {
                if (entity is null) continue;
                if (spawnedOres.TryGetValue(entity, out var data))
                {
                    DecrementOreCount(data.Type, data.GatherMultiplier);
                    spawnedOres.Remove(entity);
                    _oreGatherSessions.Remove(entity);
                }
                if (!entity.IsDestroyed)
                    entity.Kill();
            }

            Pool.FreeUnmanaged(ref toRemove);

            if (removed > 0)
                PrintWarning($"Removed {removed} floating/underwater ores");
        }

        private void CleanupSpawnedOres()
        {
            var toRemove = Pool.Get<List<BaseEntity>>();
            toRemove.AddRange(spawnedOres.Keys);
            spawnedOres.Clear();
            _oreGatherSessions.Clear();
            ResetOreCounts();

            foreach (var entity in toRemove)
            {
                if (entity is not null && !entity.IsDestroyed)
                    entity.Kill();
            }

            Pool.FreeUnmanaged(ref toRemove);
        }
        #endregion

        #region Harmony Patches
        [HarmonyPatch(typeof(BaseMission), "GiveRewards")]
        private static class BaseMission_GiveRewards_Patch
        {
            private static bool Prepare()
            {
                try
                {
                    return typeof(BaseMission).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Any(method => method.Name == "GiveRewards");
                }
                catch
                {
                    return false;
                }
            }

            private static bool Prefix(BaseMission __instance, BasePlayer player)
            {
                if (_instance == null || __instance is null || player is null)
                    return true;
                return !_instance.TryGiveCustomMissionRewards(__instance, player);
            }
        }

        [HarmonyPatch(typeof(SpawnHandler), "SpawnRepeating")]
        private static class SpawnHandler_SpawnRepeating_Patch
        {
            private static readonly Dictionary<string, int> _blockedStats = new();
            private static readonly Dictionary<string, bool> _managedPopCache = new(StringComparer.OrdinalIgnoreCase);

            public static void ResetTracking()
            {
                _blockedStats.Clear();
                _managedPopCache.Clear();
            }

            private static bool Prefix(SpawnPopulationBase population, SpawnDistribution distribution)
            {
                if (_instance == null || !_instance.Ore.DisableNativeSpawning || population is null ||
                    (!_instance.Ore.PerResourceSpawning && !_instance.Ore.Enabled))
                    return true;

                var popName = population.name;
                if (!_managedPopCache.TryGetValue(popName, out bool managed))
                {
                    managed = IsManagedNativePopulation(population);
                    _managedPopCache[popName] = managed;
                }

                if (!managed)
                    return true;

                if (_instance.Ore.Debug)
                {
                    _blockedStats.TryAdd(popName, 0);
                    _blockedStats[popName]++;
                }
                return false;
            }

            private static bool IsManagedNativePopulation(SpawnPopulationBase pop)
            {
                var name = pop.name;

                if (name.Contains("forest", StringComparison.OrdinalIgnoreCase) || 
                    name.Contains("logs", StringComparison.OrdinalIgnoreCase) || 
                    name.Contains("loot", StringComparison.OrdinalIgnoreCase) || 
                    name.Contains("bushes", StringComparison.OrdinalIgnoreCase) || 
                    name.Contains("palms", StringComparison.OrdinalIgnoreCase) || 
                    name.Contains("cactus", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("driftwood", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("halloween", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("diesel", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (_instance.Ore.PerResourceSpawning)
                {
                    if (string.Equals(name, "ores", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "ores_sand", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "ores_snow", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("ores_jungle", StringComparison.OrdinalIgnoreCase))
                    {
                        return _instance.IsResourceCustomEnabled(OreType.Stone)
                            && _instance.IsResourceCustomEnabled(OreType.Metal)
                            && _instance.IsResourceCustomEnabled(OreType.Sulfur);
                    }

                    if (string.Equals(name, "hemp", StringComparison.OrdinalIgnoreCase))
                        return _instance.IsResourceCustomEnabled(OreType.CollectHemp);

                    if (name.Contains("mushroom", StringComparison.OrdinalIgnoreCase))
                        return _instance.IsResourceCustomEnabled(OreType.CollectMushroom);
                }
                else if (string.Equals(name, "ores", StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(name, "ores_sand", StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(name, "ores_snow", StringComparison.OrdinalIgnoreCase) || 
                    name.Contains("ores_jungle", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("collectable", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("mushroom", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "hemp", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("berry", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (pop.Prefabs is not { Length: > 0 }) return false;
                var containsManaged = false;
                var containsUnmanaged = false;

                foreach (var prefab in pop.Prefabs)
                {
                    if (prefab == null || string.IsNullOrEmpty(prefab.Name)) continue;
                    var inferred = _instance.InferOreTypeFromPrefab(prefab.Name);
                    if (inferred.HasValue && _instance.IsResourceCustomEnabled(inferred.Value))
                        containsManaged = true;
                    else
                    {
                        containsUnmanaged = true;
                        break;
                    }
                }

                return containsManaged && !containsUnmanaged;
            }
        }

        [HarmonyPatch(typeof(OreResourceEntity), nameof(OreResourceEntity.OnDied))]
        private static class OreResourceEntity_OnDied_Patch
        {
            private static void Prefix(OreResourceEntity __instance)
            {
                if (_instance is null) return;
                if (!_instance.spawnedOres.TryGetValue(__instance, out var oreData)) return;
                _instance.spawnedOres.Remove(__instance);
                _instance._oreGatherSessions.Remove(__instance);
                _instance.DecrementOreCount(oreData.Type, oreData.GatherMultiplier);

                var settings = _instance.GetOreTypeSettings(oreData.Type);
                if (settings is null || !settings.Enabled) return;

                var respawnDelay = Mathf.Max(1f, _instance.Ore.RespawnIntervalMinutes) * 60f * UnityEngine.Random.Range(0.85f, 1.15f);
                _instance.timer.Once(respawnDelay, () =>
                {
                    if (_instance is null) return;
                    RespawnOre(oreData, settings);
                });
            }

            private static void RespawnOre(OreNodeData oreData, OreTypeSettings settings)
            {
                _instance.TryRespawnResource(oreData, settings);
            }
        }

        private OreType? InferOreTypeFromPrefab(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return null;
            if (_oreTypeCache.TryGetValue(prefab, out var cached))
                return cached;
            if (_oreTypeMissCache.Contains(prefab))
                return null;

            var inferred = InferOreTypeFromPrefabUncached(prefab);
            if (inferred.HasValue)
                _oreTypeCache[prefab] = inferred.Value;
            else
                _oreTypeMissCache.Add(prefab);
            return inferred;
        }

        private static OreType? InferOreTypeFromPrefabUncached(string prefab)
        {
            var p = prefab;
            if (p.Contains("halloween", StringComparison.OrdinalIgnoreCase))
            {
                if (p.Contains("bone", StringComparison.OrdinalIgnoreCase)) return OreType.CollectHalloweenBone;
                if (p.Contains("metal", StringComparison.OrdinalIgnoreCase)) return OreType.CollectHalloweenMetal;
                if (p.Contains("sulfur", StringComparison.OrdinalIgnoreCase)) return OreType.CollectHalloweenSulfur;
                if (p.Contains("wood", StringComparison.OrdinalIgnoreCase)) return OreType.CollectHalloweenWood;
                if (p.Contains("stone", StringComparison.OrdinalIgnoreCase)) return OreType.CollectHalloweenStone;
                return null;
            }

            if (p.Contains("collectable", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("collectible", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("mushroom-cluster", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("diesel_collectable", StringComparison.OrdinalIgnoreCase))
            {
                if (p.Contains("hqm", StringComparison.OrdinalIgnoreCase)) return OreType.CollectHQM;
                if (p.Contains("metal", StringComparison.OrdinalIgnoreCase)) return OreType.CollectMetal;
                if (p.Contains("sulfur", StringComparison.OrdinalIgnoreCase)) return OreType.CollectSulfur;
                if (p.Contains("stone", StringComparison.OrdinalIgnoreCase)) return OreType.CollectStone;
                if (p.Contains("wood", StringComparison.OrdinalIgnoreCase)) return OreType.CollectWood;
                if (p.Contains("hemp", StringComparison.OrdinalIgnoreCase)) return OreType.CollectHemp;
                if (p.Contains("berry-black", StringComparison.OrdinalIgnoreCase) || p.Contains("berry_black", StringComparison.OrdinalIgnoreCase))
                    return OreType.CollectBerryBlack;
                if (p.Contains("berry-blue", StringComparison.OrdinalIgnoreCase) || p.Contains("berry_blue", StringComparison.OrdinalIgnoreCase))
                    return OreType.CollectBerryBlue;
                if (p.Contains("berry-green", StringComparison.OrdinalIgnoreCase) || p.Contains("berry_green", StringComparison.OrdinalIgnoreCase))
                    return OreType.CollectBerryGreen;
                if (p.Contains("berry-red", StringComparison.OrdinalIgnoreCase) || p.Contains("berry_red", StringComparison.OrdinalIgnoreCase))
                    return OreType.CollectBerryRed;
                if (p.Contains("berry-white", StringComparison.OrdinalIgnoreCase) || p.Contains("berry_white", StringComparison.OrdinalIgnoreCase))
                    return OreType.CollectBerryWhite;
                if (p.Contains("berry-yellow", StringComparison.OrdinalIgnoreCase) || p.Contains("berry_yellow", StringComparison.OrdinalIgnoreCase))
                    return OreType.CollectBerryYellow;
                if (p.Contains("mushroom", StringComparison.OrdinalIgnoreCase)) return OreType.CollectMushroom;
                if (p.Contains("corn", StringComparison.OrdinalIgnoreCase)) return OreType.CollectCorn;
                if (p.Contains("potato", StringComparison.OrdinalIgnoreCase)) return OreType.CollectPotato;
                if (p.Contains("pumpkin", StringComparison.OrdinalIgnoreCase)) return OreType.CollectPumpkin;
                if (p.Contains("orchid", StringComparison.OrdinalIgnoreCase)) return OreType.CollectOrchid;
                if (p.Contains("rose", StringComparison.OrdinalIgnoreCase)) return OreType.CollectRose;
                if (p.Contains("sunflower", StringComparison.OrdinalIgnoreCase)) return OreType.CollectSunflower;
                if (p.Contains("wheat", StringComparison.OrdinalIgnoreCase)) return OreType.CollectWheat;
                if (p.Contains("coconut", StringComparison.OrdinalIgnoreCase)) return OreType.CollectCoconut;
                if (p.Contains("diesel", StringComparison.OrdinalIgnoreCase)) return OreType.CollectDiesel;
                return null;
            }

            if (p.Contains("hqm-ore", StringComparison.OrdinalIgnoreCase)) return OreType.HQM;
            if (p.Contains("stone-ore", StringComparison.OrdinalIgnoreCase)) return OreType.Stone;
            if (p.Contains("metal-ore", StringComparison.OrdinalIgnoreCase)) return OreType.Metal;
            if (p.Contains("sulfur-ore", StringComparison.OrdinalIgnoreCase)) return OreType.Sulfur;
            if (p.Contains("wood-pile", StringComparison.OrdinalIgnoreCase) || p.Contains("wood_log_pile", StringComparison.OrdinalIgnoreCase))
                return OreType.WoodPile;
            if (p.Contains("cactus", StringComparison.OrdinalIgnoreCase)) return OreType.Cactus;
            if (string.Equals(p, TREE_PREFAB, StringComparison.OrdinalIgnoreCase)) return OreType.Tree;
            return null;
        }

        private bool IsNativeManagedEntity(BaseEntity entity)
        {
            if (entity is not OreResourceEntity and not CollectibleEntity)
                return false;
            var inferred = InferOreTypeFromPrefab(entity.PrefabName);
            if (!inferred.HasValue) return false;
            if (Ore.PerResourceSpawning)
                return IsResourceCustomEnabled(inferred.Value);
            return true;
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!Ore.DisableNativeSpawning || !Ore.PerResourceSpawning) return;
            if (entity is not BaseEntity worldEntity || worldEntity.IsDestroyed) return;
            if (worldEntity is not OreResourceEntity and not CollectibleEntity) return;
            if (spawnedOres.ContainsKey(worldEntity)) return;
            if (!IsNativeManagedEntity(worldEntity)) return;

            NextTick(() =>
            {
                if (!Ore.DisableNativeSpawning || !Ore.PerResourceSpawning) return;
                if (worldEntity is null || worldEntity.IsDestroyed) return;
                if (spawnedOres.ContainsKey(worldEntity)) return;
                if (!IsNativeManagedEntity(worldEntity)) return;
                worldEntity.Kill();
            });
        }

        private void CleanupNativeOres()
        {
            var toKill = Pool.Get<List<BaseEntity>>();

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is not BaseEntity { IsDestroyed: false } worldEntity) continue;
                if (!IsNativeManagedEntity(worldEntity) || spawnedOres.ContainsKey(worldEntity)) continue;
                toKill.Add(worldEntity);
            }

            var count = toKill.Count;
            foreach (var worldEntity in toKill)
            {
                if (worldEntity is not null && !worldEntity.IsDestroyed)
                    worldEntity.Kill();
            }

            Pool.FreeUnmanaged(ref toKill);

            if (count > 0)
                Puts($"Cleaned up {count} existing native ore and collectable entities.");
        }

        private IEnumerator CleanupNativeOresCoroutine()
        {
            var toKill = Pool.Get<List<BaseEntity>>();

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is not BaseEntity { IsDestroyed: false } worldEntity) continue;
                if (!IsNativeManagedEntity(worldEntity) || spawnedOres.ContainsKey(worldEntity)) continue;
                toKill.Add(worldEntity);
            }

            var count = toKill.Count;
            var batch = 0;
            foreach (var worldEntity in toKill)
            {
                if (worldEntity is not null && !worldEntity.IsDestroyed)
                    worldEntity.Kill();
                if (++batch >= Ore.BatchSize)
                {
                    batch = 0;
                    yield return null;
                }
            }

            Pool.FreeUnmanaged(ref toKill);
            cleanupCoroutine = null;

            if (count > 0)
                Puts($"Cleaned up {count} existing native ore and collectable entities.");
        }
        #endregion

        #region Gather Hooks
        private bool IsListedGatherPluginName(string? pluginName)
        {
            if (string.IsNullOrWhiteSpace(pluginName) || _config?.Generic?.OtherGatherPluginNames == null)
                return false;
            if (pluginName.Equals(Name, StringComparison.OrdinalIgnoreCase))
                return false;

            foreach (var listed in _config.Generic.OtherGatherPluginNames)
            {
                if (!string.IsNullOrWhiteSpace(listed) && listed.Equals(pluginName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private bool TryGetLoadedGatherConflictPlugins(out List<string> names)
        {
            if (_gatherConflictCacheValid)
            {
                names = _cachedGatherConflictNames;
                return _cachedHasGatherConflict;
            }

            names = null;
            var listed = _config?.Generic?.OtherGatherPluginNames;
            if (listed == null || listed.Count == 0)
            {
                _gatherConflictCacheValid = true;
                _cachedHasGatherConflict = false;
                _cachedGatherConflictNames = null;
                return false;
            }

            foreach (var name in listed)
            {
                if (string.IsNullOrWhiteSpace(name) || name.Equals(Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                var plugin = plugins.Find(name.Trim());
                if (plugin is not { IsLoaded: true })
                    continue;

                names ??= new List<string>();
                names.Add(plugin.Name);
            }

            _cachedGatherConflictNames = names;
            _cachedHasGatherConflict = names is { Count: > 0 };
            _gatherConflictCacheValid = true;
            return _cachedHasGatherConflict;
        }

        private void InvalidateGatherConflictCache()
        {
            _gatherConflictCacheValid = false;
            _cachedHasGatherConflict = false;
            _cachedGatherConflictNames = null;
        }

        private bool ShouldStandDownGatherRates()
            => _config?.Generic?.StandDownGatherRatesIfOtherPlugins == true && TryGetLoadedGatherConflictPlugins(out _);

        private void NotifyGatherStandDownIfNeeded()
        {
            if (_loggedGatherStandDown || !TryGetLoadedGatherConflictPlugins(out var names))
                return;

            _loggedGatherStandDown = true;
            Log($"Gather/harvest rate overrides are standing down because another gather plugin is loaded ({string.Join(", ", names)}). Set \"Stand down gather rates if other gather plugins are loaded\" to false in BetterLoot.json to apply BetterLoot amounts anyway.");
        }

        private bool ShouldSkipGatherRateOverride(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (ShouldStandDownGatherRates())
            {
                NotifyGatherStandDownIfNeeded();
                return true;
            }

            return _hasShouldBLGather && Interface.CallHook("ShouldBLGather", dispenser, player, item) != null;
        }

        private bool ShouldSkipCollectablePickupOverride(CollectibleEntity collectible, BasePlayer player)
        {
            if (ShouldStandDownGatherRates())
            {
                NotifyGatherStandDownIfNeeded();
                return true;
            }

            return _hasShouldBLCollectablePickup && Interface.CallHook("ShouldBLCollectablePickup", collectible, player) != null;
        }

        private bool ShouldSkipQuarryGatherOverride()
        {
            if (ShouldStandDownGatherRates())
            {
                NotifyGatherStandDownIfNeeded();
                return true;
            }

            return false;
        }

        private object OnQuarryGather(MiningQuarry quarry, Item item)
        {
            if (quarry == null || item?.info == null)
                return null;
            if (ShouldSkipQuarryGatherOverride())
                return null;

            var key = GetQuarryHarvestKey(quarry);
            if (string.IsNullOrEmpty(key) || !TryGetQuarryHarvestSettings(key, out var settings))
                return null;
            if (!settings.OverrideGatherAmounts)
                return null;

            ApplyQuarryOutputOverride(settings, key, item);
            return item.amount <= 0 ? (object)true : null;
        }

        private object OnExcavatorGather(ExcavatorArm excavator, Item item)
        {
            if (excavator == null || item?.info == null)
                return null;
            if (ShouldSkipQuarryGatherOverride())
                return null;
            if (!TryGetQuarryHarvestSettings(QUARRY_EXCAVATOR_KEY, out var settings) || !settings.OverrideGatherAmounts)
                return null;

            ApplyQuarryOutputOverride(settings, QUARRY_EXCAVATOR_KEY, item);
            return item.amount <= 0 ? (object)true : null;
        }

        private bool TryGetQuarryHarvestSettings(string key, out QuarryHarvestSettings settings)
        {
            settings = null;
            if (string.IsNullOrEmpty(key) || Ore.QuarryHarvest == null)
                return false;
            return Ore.QuarryHarvest.TryGetValue(key, out settings) && settings != null;
        }

        private static string GetQuarryHarvestKey(MiningQuarry quarry)
        {
            if (quarry == null)
                return null;

            switch (quarry.staticType)
            {
                case MiningQuarry.QuarryType.HQM:
                    return QUARRY_HQM_KEY;
                case MiningQuarry.QuarryType.Sulfur:
                    return QUARRY_SULFUR_KEY;
                case MiningQuarry.QuarryType.Basic:
                    return QUARRY_STONE_KEY;
            }

            var monument = quarry.GetComponentInParent<MonumentInfo>();
            var name = monument != null
                ? $"{monument.name} {monument.transform.root?.name}"
                : quarry.PrefabName ?? string.Empty;
            if (name.IndexOf("mining_quarry_c", StringComparison.OrdinalIgnoreCase) >= 0)
                return QUARRY_HQM_KEY;
            if (name.IndexOf("mining_quarry_a", StringComparison.OrdinalIgnoreCase) >= 0)
                return QUARRY_SULFUR_KEY;
            if (name.IndexOf("mining_quarry_b", StringComparison.OrdinalIgnoreCase) >= 0)
                return QUARRY_STONE_KEY;
            return null;
        }

        private static void ApplyQuarryOutputOverride(QuarryHarvestSettings settings, string key, Item item)
        {
            if (settings?.Outputs == null || item?.info == null)
                return;

            var shortname = item.info.shortname;
            int? configured = null;
            for (var i = 0; i < settings.Outputs.Count; i++)
            {
                var output = settings.Outputs[i];
                if (output != null && string.Equals(output.Shortname, shortname, StringComparison.OrdinalIgnoreCase))
                {
                    configured = output.Amount;
                    break;
                }
            }

            if (!configured.HasValue)
                return;
            if (!VanillaQuarryPerDiesel.TryGetValue(key, out var vanillaMap)
                || !vanillaMap.TryGetValue(shortname, out var vanilla)
                || vanilla <= 0)
                return;

            if (configured.Value <= 0)
            {
                item.amount = 0;
                return;
            }

            long scaled = (long)item.amount * configured.Value / vanilla;
            item.amount = (int)Math.Max(1, Math.Min(int.MaxValue, scaled));
        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin == null || plugin == this)
                return;

            InvalidateGatherConflictCache();
            QueueOptionalApiHookRefresh();
            if (!IsListedGatherPluginName(plugin.Name))
                return;

            _loggedGatherStandDown = false;
            NotifyGatherStandDownIfNeeded();
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin == null || plugin == this)
                return;

            InvalidateGatherConflictCache();
            QueueOptionalApiHookRefresh();
            if (!IsListedGatherPluginName(plugin.Name))
                return;

            _loggedGatherStandDown = false;
        }

        private object FinishOwnedDispenserHit(BasePlayer player, Item item, bool isBonus)
        {
            if (isBonus)
                return true;

            if (player != null && item != null && item.amount > 0)
                player.GiveItem(item, BaseEntity.GiveItemReason.ResourceHarvested);

            return true;
        }

        private object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            return HandleDispenserYield(dispenser, player, item, false);
        }

        private object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            return HandleDispenserYield(dispenser, player, item, true);
        }

        private object HandleDispenserYield(ResourceDispenser dispenser, BasePlayer player, Item item, bool isBonus)
        {
            if (dispenser is null || player is null || item == null) return null;

            var entity = dispenser.baseEntity;
            if (entity is null) return null;

            bool skipRates = ShouldSkipGatherRateOverride(dispenser, player, item);

            if (spawnedOres.TryGetValue(entity, out var oreData))
            {
                bool owned = false;
                if (!skipRates)
                {
                    var settings = GetOreTypeSettings(oreData.Type);
                    owned = settings != null
                        && settings.OverrideGatherAmounts
                        && settings.GatherOutputs is { Count: > 0 }
                        && ApplyGatherOverride(dispenser, player, item, oreData, settings, isBonus);

                    if (!owned)
                    {
                        if (oreData.Type == OreType.HQM && item.info != null && item.info.shortname == METAL_ORE_SHORTNAME)
                        {
                            var hqmDef = ItemManager.FindItemDefinition(HQM_ORE_SHORTNAME);
                            if (hqmDef != null)
                                item.info = hqmDef;
                        }

                        var scale = CombineGatherScale(settings, oreData);
                        if (Mathf.Abs(scale - 1f) > 0.0001f)
                        {
                            item.amount = Mathf.Max(0, Mathf.FloorToInt(item.amount * scale));
                            if (!isBonus && oreData.GatherMultiplier > 1f && Ore.RichVein.PlayEffect)
                                Effect.server.Run(RICH_VEIN_EFFECT, entity.transform.position, Vector3.up);
                            owned = true;
                        }
                    }
                }

                if (!isBonus)
                    ProcessBonusLoot(player, oreData.Type);

                return owned ? FinishOwnedDispenserHit(player, item, isBonus) : null;
            }

            if (!skipRates
                && TryGetNpcHarvestSettings(entity, out var npcHarvest)
                && npcHarvest.OverrideGatherAmounts
                && npcHarvest.GatherOutputs is { Count: > 0 })
            {
                var dummy = new OreNodeData(OreType.Stone, entity.transform.position, default(TerrainBiome.Enum), 1f, entity.PrefabName);
                ApplyGatherOverride(dispenser, player, item, dummy, npcHarvest.GatherOutputs, isBonus);
                return FinishOwnedDispenserHit(player, item, isBonus);
            }

            if (!skipRates)
            {
                var inferred = InferOreTypeFromPrefab(entity.PrefabName);
                if (!inferred.HasValue && entity is TreeEntity)
                    inferred = OreType.Tree;

                if (inferred.HasValue && IsNodeType(inferred.Value))
                {
                    var settings = GetOreTypeSettings(inferred.Value);
                    if (settings is { OverrideGatherAmounts: true, GatherOutputs: { Count: > 0 } })
                    {
                        var nativeData = new OreNodeData(inferred.Value, entity.transform.position, default(TerrainBiome.Enum), 1f, entity.PrefabName);
                        if (ApplyGatherOverride(dispenser, player, item, nativeData, settings, isBonus))
                            return FinishOwnedDispenserHit(player, item, isBonus);
                    }

                    var scale = GetConfiguredGatherMultiplier(settings);
                    if (Mathf.Abs(scale - 1f) > 0.0001f)
                    {
                        item.amount = Mathf.Max(0, Mathf.FloorToInt(item.amount * scale));
                        return FinishOwnedDispenserHit(player, item, isBonus);
                    }
                }
            }

            return null;
        }

        private bool TryGetNpcHarvestSettings(BaseEntity entity, out NpcHarvestSettings settings)
        {
            settings = null;
            if (entity is null || Ore.NpcHarvest == null || Ore.NpcHarvest.Count == 0)
                return false;

            if (entity is NPCPlayerCorpse)
            {
                if (entity.net != null && _corpseNpcPrefabs.TryGetValue(entity.net.ID.Value, out var npcMapped)
                    && TryMatchNpcHarvest(npcMapped, out settings))
                    return true;
                return TryMatchNpcHarvest(entity.PrefabName, out settings);
            }

            if (TryMatchNpcHarvest(entity.PrefabName, out settings))
                return true;

            if (entity.net != null && _corpseNpcPrefabs.TryGetValue(entity.net.ID.Value, out var mapped)
                && TryMatchNpcHarvest(mapped, out settings))
                return true;

            return entity is PlayerCorpse && TryMatchNpcHarvest(PlayerHarvestPrefab, out settings);
        }

        private bool TryMatchNpcHarvest(string prefabName, out NpcHarvestSettings settings)
        {
            settings = null;
            if (string.IsNullOrEmpty(prefabName) || Ore.NpcHarvest == null)
                return false;

            if (_npcHarvestHitCache.TryGetValue(prefabName, out settings))
                return settings != null;
            if (_npcHarvestMissCache.Contains(prefabName))
                return false;

            foreach (var candidate in EnumerateHarvestKeys(prefabName))
            {
                if (Ore.NpcHarvest.TryGetValue(candidate, out settings) && settings != null)
                {
                    _npcHarvestHitCache[prefabName] = settings;
                    return true;
                }
            }

            _npcHarvestMissCache.Add(prefabName);
            return false;
        }

        private static IEnumerable<string> EnumerateHarvestKeys(string prefabName)
        {
            var n = prefabName.Replace('\\', '/');
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (seen.Add(prefabName))
                yield return prefabName;
            if (seen.Add(n))
                yield return n;

            foreach (var group in HarvestPrefabAliases)
            {
                var hit = false;
                foreach (var alias in group)
                {
                    if (PrefabMatchesHarvestAlias(n, alias))
                    {
                        hit = true;
                        break;
                    }
                }
                if (!hit) continue;
                foreach (var alias in group)
                {
                    if (seen.Add(alias))
                        yield return alias;
                }
                yield break;
            }

            var stripped = n
                .Replace(".corpse.tutorial", string.Empty)
                .Replace(".corpse", string.Empty)
                .Replace("_tutorial", string.Empty)
                .Replace(".tutorial", string.Empty);
            if (seen.Add(stripped))
                yield return stripped;
        }

        private static bool PrefabMatchesHarvestAlias(string prefab, string alias)
        {
            if (string.IsNullOrEmpty(prefab) || string.IsNullOrEmpty(alias))
                return false;
            if (prefab.Equals(alias, StringComparison.OrdinalIgnoreCase))
                return true;

            var file = alias.Replace('\\', '/');
            var slash = file.LastIndexOf('/');
            if (slash >= 0)
                file = file.Substring(slash + 1);
            if (file.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                file = file.Substring(0, file.Length - 7);
            if (file.Length < 12 && file.IndexOf("monumentblocker", StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            return prefab.IndexOf(file, StringComparison.OrdinalIgnoreCase) >= 0
                || prefab.IndexOf(file.Replace('_', '-'), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool ApplyGatherOverride(ResourceDispenser dispenser, BasePlayer player, Item item, OreNodeData oreData, OreTypeSettings settings, bool isBonus)
            => ApplyGatherOverride(dispenser, player, item, oreData, settings.GatherOutputs, isBonus, GetConfiguredGatherMultiplier(settings));

        private bool ApplyGatherOverride(ResourceDispenser dispenser, BasePlayer player, Item item, OreNodeData oreData, List<OreGatherOutput> outputs, bool isBonus, float configuredMultiplier = 1f)
        {
            var entity = dispenser.baseEntity;
            if (entity is null || outputs == null) return false;

            var session = GetOrCreateGatherSession(entity, dispenser, item);
            var tool = GetHeldToolShortname(player);
            session.LastTool = tool;

            if (Time.frameCount != session.LastFrame)
            {
                session.LastFrame = Time.frameCount;
                session.ExtraGrantedThisFrame.Clear();
            }

            var rich = oreData.GatherMultiplier > 0f ? oreData.GatherMultiplier : 1f;
            var configured = configuredMultiplier > 0f ? configuredMultiplier : 1f;
            var multiplier = rich * configured;
            var vanillaName = item.info?.shortname ?? string.Empty;
            var vanillaHit = Mathf.Max(0, item.amount);
            var startForItem = session.VanillaStartByItem.TryGetValue(vanillaName, out var itemStart) ? itemStart : 0f;
            var fraction = startForItem > 0f
                ? vanillaHit / startForItem
                : (session.VanillaStartTotal > 0f ? vanillaHit / session.VanillaStartTotal : 0f);

            OreGatherOutput match = FindGatherOutput(outputs, vanillaName);
            if (match == null && oreData.Type == OreType.HQM && vanillaName == METAL_ORE_SHORTNAME)
            {
                match = FindGatherOutput(outputs, HQM_ORE_SHORTNAME);
                if (match != null)
                {
                    var hqmDef = ItemManager.FindItemDefinition(HQM_ORE_SHORTNAME);
                    if (hqmDef != null)
                        item.info = hqmDef;
                    vanillaName = HQM_ORE_SHORTNAME;
                }
            }

            if (match != null)
            {
                var target = Mathf.RoundToInt(GetConfiguredToolAmount(match, tool) * multiplier);
                session.GivenByItem.TryGetValue(match.Shortname, out var already);
                var remaining = Mathf.Max(0, target - already);
                var give = isBonus ? remaining : Mathf.Clamp(Mathf.RoundToInt(target * fraction), 0, remaining);
                item.amount = give;
                session.GivenByItem[match.Shortname] = already + give;
            }
            else
            {
                item.amount = 0;
            }

            foreach (var output in outputs)
            {
                if (output == null || string.IsNullOrEmpty(output.Shortname)) continue;
                if (match != null && string.Equals(output.Shortname, match.Shortname, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (session.VanillaStartByItem.ContainsKey(output.Shortname))
                    continue;
                if (!session.ExtraGrantedThisFrame.Add(output.Shortname))
                    continue;

                var target = Mathf.RoundToInt(GetConfiguredToolAmount(output, tool) * multiplier);
                session.GivenByItem.TryGetValue(output.Shortname, out var already);
                var remaining = Mathf.Max(0, target - already);
                var give = isBonus ? remaining : Mathf.Clamp(Mathf.RoundToInt(target * fraction), 0, remaining);
                if (give <= 0) continue;

                var def = ItemManager.FindItemDefinition(output.Shortname);
                if (def == null) continue;
                var extra = ItemManager.Create(def, give);
                if (extra == null) continue;
                player.GiveItem(extra, BaseEntity.GiveItemReason.ResourceHarvested);
                session.GivenByItem[output.Shortname] = already + give;
            }

            if (oreData.GatherMultiplier > 1f && Ore.RichVein.PlayEffect)
                Effect.server.Run(RICH_VEIN_EFFECT, entity.transform.position, Vector3.up);

            return true;
        }

        private OreGatherSession GetOrCreateGatherSession(BaseEntity entity, ResourceDispenser dispenser, Item item)
        {
            if (_oreGatherSessions.TryGetValue(entity, out var session))
                return session;

            session = new OreGatherSession();
            if (dispenser.containedItems != null)
            {
                foreach (var contained in dispenser.containedItems)
                {
                    if (contained?.itemDef == null) continue;
                    var amount = contained.amount;
                    session.VanillaStartTotal += amount;
                    if (!session.VanillaStartByItem.ContainsKey(contained.itemDef.shortname))
                        session.VanillaStartByItem[contained.itemDef.shortname] = 0f;
                    session.VanillaStartByItem[contained.itemDef.shortname] += amount;
                }
            }

            var hitName = item.info?.shortname;
            if (!string.IsNullOrEmpty(hitName))
            {
                session.VanillaStartTotal += item.amount;
                if (!session.VanillaStartByItem.ContainsKey(hitName))
                    session.VanillaStartByItem[hitName] = 0f;
                session.VanillaStartByItem[hitName] += item.amount;
            }

            session.VanillaStartTotal = Mathf.Max(1f, session.VanillaStartTotal);
            _oreGatherSessions[entity] = session;
            return session;
        }

        private static OreGatherOutput FindGatherOutput(List<OreGatherOutput> outputs, string shortname)
        {
            if (outputs == null || string.IsNullOrEmpty(shortname)) return null;
            for (var i = 0; i < outputs.Count; i++)
            {
                var output = outputs[i];
                if (output != null && string.Equals(output.Shortname, shortname, StringComparison.OrdinalIgnoreCase))
                    return output;
            }
            return null;
        }

        private static int GetConfiguredToolAmount(OreGatherOutput output, string tool)
        {
            if (output?.AmountsByTool == null || output.AmountsByTool.Count == 0)
                return 0;
            if (!string.IsNullOrEmpty(tool) && output.AmountsByTool.TryGetValue(tool, out var exact))
                return Math.Max(0, exact);
            if (output.AmountsByTool.TryGetValue("pickaxe", out var pickaxe))
                return Math.Max(0, pickaxe);
            if (output.AmountsByTool.TryGetValue("hatchet", out var hatchet))
                return Math.Max(0, hatchet);
            if (output.AmountsByTool.TryGetValue("knife.skinning", out var skinning))
                return Math.Max(0, skinning);
            if (output.AmountsByTool.TryGetValue("knife.bone", out var boneKnife))
                return Math.Max(0, boneKnife);
            foreach (var amount in output.AmountsByTool.Values)
                return Math.Max(0, amount);
            return 0;
        }

        private static string GetHeldToolShortname(BasePlayer player)
        {
            var held = player.GetActiveItem();
            var shortname = held?.info?.shortname;
            return string.IsNullOrEmpty(shortname) ? "rock" : shortname;
        }

        private bool TryGetCollectablePickupContext(CollectibleEntity collectible, out OreType type, out OreTypeSettings settings)
        {
            type = default;
            settings = null;
            if (collectible is null) return false;

            if (spawnedOres.TryGetValue(collectible, out var oreData))
            {
                type = oreData.Type;
                settings = GetOreTypeSettings(oreData.Type);
                return settings != null;
            }

            var inferred = InferOreTypeFromPrefab(collectible.PrefabName);
            if (!inferred.HasValue || !IsCollectableType(inferred.Value)) return false;
            type = inferred.Value;
            settings = GetOreTypeSettings(type);
            return settings != null;
        }

        private object OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (collectible is null || player is null) return null;
            if (!TryGetCollectablePickupContext(collectible, out var type, out var settings))
                return null;

            if (ShouldSkipCollectablePickupOverride(collectible, player))
            {
                if (spawnedOres.ContainsKey(collectible))
                    ProcessBonusLoot(player, type);
                return null;
            }

            if (HasPickupOutputOverride(settings))
            {
                GiveConfiguredPickupOutputs(player, settings);
                ProcessBonusLoot(player, type);
                NextTick(() =>
                {
                    if (collectible != null && !collectible.IsDestroyed)
                        collectible.Kill();
                });
                return true;
            }

            if (spawnedOres.ContainsKey(collectible))
                ProcessBonusLoot(player, type);
            return null;
        }

        private void OnCollectiblePickup(Item item, BasePlayer player, CollectibleEntity collectible)
        {
            if (!TryGetCollectablePickupContext(collectible, out _, out var settings))
                return;
            if (ShouldSkipCollectablePickupOverride(collectible, player))
                return;
            if (HasPickupOutputOverride(settings))
                return;

            ApplyCollectablePickupAmount(item, collectible, settings);
        }

        private void ApplyCollectablePickupAmount(Item item, CollectibleEntity collectible, OreTypeSettings settings)
        {
            if (item == null || collectible is null || settings == null || settings.PickupMaxAmount <= 0) return;

            var max = settings.PickupMaxAmount;
            var min = settings.PickupMinAmount > 0 ? settings.PickupMinAmount : max;
            if (max < min) max = min;
            item.amount = UnityEngine.Random.Range(min, max + 1);
        }

        private static void GiveConfiguredPickupOutputs(BasePlayer player, OreTypeSettings settings)
        {
            if (player is null || settings?.PickupOutputs == null) return;

            foreach (var output in settings.PickupOutputs)
            {
                if (output == null || string.IsNullOrWhiteSpace(output.Shortname)) continue;

                var itemDef = ItemManager.FindItemDefinition(output.Shortname);
                if (itemDef is null) continue;

                var min = Math.Max(0, output.MinAmount);
                var max = Math.Max(min, output.MaxAmount);
                if (max <= 0) continue;

                var amount = UnityEngine.Random.Range(min, max + 1);
                if (amount <= 0) continue;

                var given = ItemManager.Create(itemDef, amount);
                if (given != null)
                    player.GiveItem(given, BaseEntity.GiveItemReason.ResourceHarvested);
            }
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            if (entity is LootableCorpse && entity.net != null && _corpseNpcPrefabs.Count > 0)
                _corpseNpcPrefabs.Remove(entity.net.ID.Value);

            if (entity is not BaseEntity baseEntity) return;
            if (_oreGatherSessions.Count > 0)
                _oreGatherSessions.Remove(baseEntity);

            if (spawnedOres.Count == 0 || !spawnedOres.TryGetValue(baseEntity, out var oreData)) return;
            if (!IsCollectableType(oreData.Type)) return;

            spawnedOres.Remove(baseEntity);
            _oreGatherSessions.Remove(baseEntity);
            DecrementOreCount(oreData.Type, oreData.GatherMultiplier);

            var settings = GetOreTypeSettings(oreData.Type);
            if (settings is null || !settings.Enabled) return;

            var respawnDelay = Mathf.Max(1f, Ore.RespawnIntervalMinutes) * 60f * UnityEngine.Random.Range(0.85f, 1.15f);
            timer.Once(respawnDelay, () =>
            {
                if (_instance is null) return;
                TryRespawnResource(oreData, settings);
            });
        }

        private void TryRespawnResource(OreNodeData oreData, OreTypeSettings settings)
        {
            if ((!Ore.PerResourceSpawning && !Ore.Enabled) || !settings.Enabled ||
                !ReferenceEquals(settings, GetOreTypeSettings(oreData.Type)))
                return;

            var maxOffset = settings.GridSize * 0.4f;
            var basePos = oreData.Position;

            for (var i = 0; i < 10; i++)
            {
                var position = new Vector3(
                    basePos.x + UnityEngine.Random.Range(-maxOffset, maxOffset),
                    0f,
                    basePos.z + UnityEngine.Random.Range(-maxOffset, maxOffset)
                );

                if (!IsValidOreSpawnPosition(position, settings, out var adjustedPos, out var biome))
                    continue;

                SpawnOre(new OreSpawnPosition(adjustedPos, oreData.Type, oreData.Prefab, biome));

                if (Ore.Debug)
                    Puts($"Respawned {oreData.Type} near {basePos:F0}");
                return;
            }

            if (Ore.Debug)
                PrintWarning($"Failed to find valid respawn position for {oreData.Type} near {basePos:F0}");
        }

        private void ProcessBonusLoot(BasePlayer player, OreType oreType)
        {
            var bonusLoot = GetOreTypeSettings(oreType)?.BonusLoot;

            if (bonusLoot == null || bonusLoot.Count == 0) return;

            foreach (var entry in bonusLoot)
            {
                if (UnityEngine.Random.Range(0f, 1f) > entry.Chance) continue;

                var itemDef = ItemManager.FindItemDefinition(entry.Shortname);
                if (itemDef is null) continue;

                var amount = UnityEngine.Random.Range(entry.MinAmount, entry.MaxAmount + 1);
                var bonusItem = ItemManager.Create(itemDef, amount);

                if (bonusItem != null)
                {
                    player.GiveItem(bonusItem, BaseEntity.GiveItemReason.ResourceHarvested);
                }
            }
        }
        #endregion

        #region Map Markers
        private void StopShowCoroutine(ulong userId)
        {
            if (!activeShowCoroutines.TryGetValue(userId, out var coroutine)) return;
            if (coroutine != null)
                ServerMgr.Instance.StopCoroutine(coroutine);
            activeShowCoroutines.Remove(userId);
        }

        private void StopAllShowCoroutines()
        {
            foreach (var coroutine in activeShowCoroutines.Values)
            {
                if (coroutine != null)
                    ServerMgr.Instance.StopCoroutine(coroutine);
            }
            activeShowCoroutines.Clear();
        }

        private IEnumerator ShowNodesCoroutine(BasePlayer player, OreShowKind kind)
        {
            var userId = player.userID;
            AdoptUntrackedWorldResources();

            var stoneCount = 0;
            var metalCount = 0;
            var sulfurCount = 0;
            var hqmCount = 0;
            var harvestCount = 0;
            var collectableCount = 0;
            var richCount = 0;
            var pluginCount = 0;

            foreach (var kvp in spawnedOres)
            {
                if (kvp.Key is null || kvp.Key.IsDestroyed) continue;
                if (!MatchesShowKind(kvp.Value.Type, kind)) continue;
                pluginCount++;
                switch (kvp.Value.Type)
                {
                    case OreType.Stone: stoneCount++; break;
                    case OreType.Metal: metalCount++; break;
                    case OreType.Sulfur: sulfurCount++; break;
                    case OreType.HQM: hqmCount++; break;
                    case OreType.WoodPile:
                    case OreType.Cactus:
                    case OreType.Tree:
                        harvestCount++;
                        break;
                    default:
                        if (IsCollectableType(kvp.Value.Type))
                            collectableCount++;
                        break;
                }
                if (kvp.Value.GatherMultiplier > 1f) richCount++;
            }

            List<OreNodeData> worldMarkers = null;
            if (pluginCount == 0)
                worldMarkers = CollectWorldResourceMarkers(kind);

            var totalCount = pluginCount > 0 ? pluginCount : worldMarkers?.Count ?? 0;
            var label = ShowKindLabel(kind);

            if (totalCount == 0)
            {
                if (spawnCoroutine != null)
                    PrintToChat(player, $"Plugin {label} are still spawning. Try again in a moment.");
                else if (!IsOreSystemEnabled())
                    PrintToChat(player, "Custom spawning is off. Enable it on a node or collectable in the Looty Editor via the Ores tab, then deploy with the Looty command.");
                else
                    PrintToChat(player, $"No plugin-spawned {label} found.");
                activeShowCoroutines.Remove(userId);
                yield break;
            }

            if (pluginCount > 0)
            {
                var shown = UpdateOreMarkersForPlayer(player, null, kind, out var found);
                PrintToChat(player, FormatShowCountMessage(shown, found, $"plugin {label}"));
                switch (kind)
                {
                    case OreShowKind.Collectable:
                        PrintToChat(player, $"<color=#7dce82>Collectables</color>: {collectableCount}");
                        break;
                    case OreShowKind.Harvest:
                        PrintToChat(player, $"<color=#c4a484>Piles/Trees</color>: {harvestCount}");
                        break;
                    default:
                        PrintToChat(player, $"<color=#c0c0c0>Stone</color>: {stoneCount} | <color=#f2d338>Metal</color>: {metalCount} | <color=#d1d138>Sulfur</color>: {sulfurCount} | <color=#9ad0ff>HQM</color>: {hqmCount}{(kind == OreShowKind.All ? $" | <color=#c4a484>Piles/Trees</color>: {harvestCount} | <color=#7dce82>Collectables</color>: {collectableCount}" : "")} | <color=#ff6600>Rich Veins</color>: {richCount}");
                        break;
                }
            }
            else
            {
                var shown = UpdateOreMarkersForPlayer(player, worldMarkers, kind, out var found);
                PrintToChat(player, $"No plugin-tracked {label} yet. {FormatShowCountMessage(shown, found, $"world {label}")}");
            }

            PrintToChat(player, "Check your map (G) to see the locations!");

            yield return CoroutineEx.waitForSeconds(30f);

            if (player.IsDestroyed)
            {
                activeShowCoroutines.Remove(userId);
                yield break;
            }

            ClearOreMarkersForPlayer(player);
            SendClearPingsToPlayer(player);
            PrintToChat(player, "Markers removed from map.");
            activeShowCoroutines.Remove(userId);
        }

        private static ProtoBuf.MapNote CreateOreMarker(Vector3 worldPosition, OreType oreType, float gatherMultiplier)
        {
            var note = Pool.Get<ProtoBuf.MapNote>();
            note.isPing = true;
            note.worldPosition = worldPosition;
            note.icon = gatherMultiplier > 1f ? 1 : 10;
            note.colourIndex = oreType switch
            {
                OreType.Stone => 0,
                OreType.Metal => 2,
                OreType.Sulfur => 4,
                OreType.HQM => 1,
                _ => IsCollectableType(oreType) ? 3 : 0
            };
            var typeName = (int)oreType >= 0 && (int)oreType < OreTypeNames.Length ? OreTypeNames[(int)oreType] : oreType.ToString();
            note.label = gatherMultiplier > 1f && IsNodeType(oreType) ? $"RICH {typeName}" : typeName;
            return note;
        }

        private static void SendPingsToPlayer(BasePlayer player, List<ProtoBuf.MapNote> extraNotes = null)
        {
            if (!player.IsConnected) return;

            var mapNoteList = Pool.Get<ProtoBuf.MapNoteList>();
            mapNoteList.notes = Pool.Get<List<ProtoBuf.MapNote>>();
            mapNoteList.notes.Clear();

            if (player.State.pings != null)
            {
                foreach (var ping in player.State.pings)
                    mapNoteList.notes.Add(ping);
            }

            if (extraNotes != null)
            {
                foreach (var note in extraNotes)
                    mapNoteList.notes.Add(note);
            }

            player.ClientRPC(Player("Client_ReceivePings", player), mapNoteList);

            mapNoteList.notes.Clear();
            mapNoteList.Dispose();
        }

        private static string FormatShowCountMessage(int shown, int found, string label)
        {
            if (shown < found)
                return $"Showing the {shown} closest of {found} {label} on your map for 30 seconds.";
            return $"Showing {found} {label} on your map for 30 seconds...";
        }

        private int UpdateOreMarkersForPlayer(BasePlayer player, List<OreNodeData> extraMarkers, OreShowKind kind, out int totalFound)
        {
            ClearOreMarkersForPlayer(player);

            if (!playerMarkers.TryGetValue(player.userID, out var currentMarkers))
            {
                currentMarkers = new List<ProtoBuf.MapNote>();
                playerMarkers[player.userID] = currentMarkers;
            }

            var origin = player.transform.position;
            var scored = new List<(float Dist, Vector3 Pos, OreType Type, float Mult)>(256);

            foreach (var kvp in spawnedOres)
            {
                if (kvp.Key is null || kvp.Key.IsDestroyed) continue;
                if (!MatchesShowKind(kvp.Value.Type, kind)) continue;
                var pos = kvp.Value.Position;
                scored.Add(((pos - origin).sqrMagnitude, pos, kvp.Value.Type, kvp.Value.GatherMultiplier));
            }

            if (extraMarkers != null)
            {
                foreach (var data in extraMarkers)
                {
                    if (!MatchesShowKind(data.Type, kind)) continue;
                    scored.Add(((data.Position - origin).sqrMagnitude, data.Position, data.Type, data.GatherMultiplier));
                }
            }

            totalFound = scored.Count;
            if (totalFound > MAX_SHOW_PINGS)
                scored.Sort((a, b) => a.Dist.CompareTo(b.Dist));

            var take = Math.Min(MAX_SHOW_PINGS, totalFound);
            for (var i = 0; i < take; i++)
            {
                var item = scored[i];
                currentMarkers.Add(CreateOreMarker(item.Pos, item.Type, item.Mult));
            }

            SendPingsToPlayer(player, currentMarkers);
            return take;
        }

        private void ClearOreMarkersForPlayer(BasePlayer player)
        {
            if (!playerMarkers.TryGetValue(player.userID, out var markers)) return;
            foreach (var marker in markers)
                marker.Dispose();
            markers.Clear();
        }

        private static void SendClearPingsToPlayer(BasePlayer player)
        {
            SendPingsToPlayer(player);
        }

        private void CleanupAllMarkers()
        {
            foreach (var markers in playerMarkers.Values)
            {
                foreach (var marker in markers)
                    marker.Dispose();
            }
            playerMarkers.Clear();
        }
        #endregion

        #endregion // Ore Spawn System

    }
}

namespace Oxide.Plugins.BetterLootExtensions
{
    public static class BetterLootExtensions
    {
        public static void ChangeConditionPercentage(this Item item, int conditionPercentage)
            => item.condition = (conditionPercentage / 100f) * item.maxCondition;

        public static bool ContainsPartial(this List<string> list, string partialString)
            => list.Any(partialString.Contains);
        public static bool IsDefault<T>(this T obj)
            => EqualityComparer<T>.Default.Equals(obj, default);
        public static int RemoveAll<TKey, TValue>(this IDictionary<TKey, TValue> dict, Func<TKey, bool> predicate)
        {
            int removeCount = 0;
            var keys = dict.Keys.Where(k => predicate(k)).ToList();
            foreach (var key in keys)
                if (dict.Remove(key))
                    removeCount++;
            return removeCount;
        }
    }
}
