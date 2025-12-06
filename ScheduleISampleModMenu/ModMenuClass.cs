using MelonLoader;
using ScheduleOne.DevUtilities;
using ScheduleOne.ItemFramework;
using ScheduleOne.Money;
using ScheduleOne.NPCs;
using ScheduleOne.Persistence;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Variables;
using FishNet;
using FishNet.Managing;
using ScheduleOne.Product;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;


namespace ScheduleIMod
{
    public class ModMenuClass : MelonMod
    {
        private bool showGUI = true;
        private int selectedTab = 0;

        private Rect windowRect = new Rect(20, 20, 750, 450);

        private enum SkinType { Classic, Dark, Neon }
        private SkinType currentSkin = SkinType.Classic;

        private GUIStyle labelStyle;
        private GUIStyle espStyle;

        private Vector2 _productScroll;

        // ===== FLAGS =====
        private bool npcEspEnabled = false;

        private bool onePunchEnabled = false;
        private bool mindControlEnabled = false;
        private bool blackHoleEnabled = false;
        private bool brainOverrideEnabled = false;
        private bool deleteNpcEnabled = false;
        private bool throwbackEnabled = false;

        // ===== NPC CACHE (ESP / brain) =====
        private List<NPCHealth> cachedNPCs = new List<NPCHealth>();
        private float nextScanTime = 0f;

        // ===== PET NPC-K =====
        private List<NPCHealth> controlledNpcs = new List<NPCHealth>();
        private float petFollowSpeed = 5f;

        // ===== BLACK HOLE =====
        private bool blackHoleActive = false;
        private Vector3 blackHoleCenter;
        private float blackHoleRadius = 20f;
        private float blackHoleForce = 30f;
        private float blackHoleDuration = 3f;
        private float blackHoleEndTime = 0f;

        // ===== BRAIN OVERRIDE =====
        private enum BrainMode { Normal, Frozen, Panic }
        private BrainMode brainMode = BrainMode.Normal;

        // NPCMovement → eredeti MoveSpeedMultiplier
        private Dictionary<NPCMovement, float> originalMoveMultipliers = new Dictionary<NPCMovement, float>();

        // ===== THROWBACK NPC (RMB) =====
        private float throwForce = 100f;       // slider 1–300
        private float throwCooldown = 0f;      // slider 0–1
        private float nextThrowTime = 0f;

        // ===== MOVEMENT =====
        private float walkSpeed = 5f;
        private float jumpMultiplier = 1f;

        // ===== MONEY =====
        private string moneyInput = "";

        // ===== ITEM SPAWNER =====
        private Vector2 itemScrollPos = Vector2.zero;
        private string itemSearch = "";
        private int itemQuantity = 1;
        private Dictionary<string, ItemDefinition> allItemDefs = new Dictionary<string, ItemDefinition>();
        private bool itemListInitialized = false;

        // Kategória filter (nincs EItemCategory.None, ezért külön bool)
        private bool itemCategoryFilterEnabled = false;
        private EItemCategory selectedItemCategory = EItemCategory.Product;

        // ===== PICKUP TEMPLATE (valódi ItemPickup mintája Drop-hoz) =====
        private ItemPickup pickupTemplate;

        // ===== PRODUCT GIVER =====
        private Vector2 productScrollPos = Vector2.zero;
        private string productSearch = "";
        private int productQuantity = 1;
        private bool filterDiscoveredOnly = false;
        private bool filterListedOnly = false;


        // ======================
        // UPDATE LOOP
        // ======================
        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F1))
                showGUI = !showGUI;

            if (npcEspEnabled && Time.time >= nextScanTime)
            {
                cachedNPCs = new List<NPCHealth>(GameObject.FindObjectsOfType<NPCHealth>());
                nextScanTime = Time.time + 0.2f;
            }

            if (!itemListInitialized)
            {
                LoadAllItemDefinitions();
                itemListInitialized = true;
            }

            HandleOnePunch();
            HandleMindControl();
            HandleBlackHole();
            HandleThrowbackNPC();
            HandleDeleteNPC();
        }

        // ======================
        // ITEMS
        // ======================

        private void LoadAllItemDefinitions()
        {
            allItemDefs.Clear();

            ItemDefinition[] defs = Resources.FindObjectsOfTypeAll<ItemDefinition>();

            foreach (var def in defs)
            {
                if (def == null) continue;
                if (string.IsNullOrEmpty(def.ID)) continue;

                if (!allItemDefs.ContainsKey(def.ID))
                    allItemDefs.Add(def.ID, def);
            }

            MelonLogger.Msg($"[ItemSpawner] Loaded {allItemDefs.Count} items.");
        }

        // Pickup template keresése (első ItemPickup a pályán)
        private void EnsurePickupTemplate()
        {
            if (pickupTemplate != null) return;

            pickupTemplate = UnityEngine.Object.FindObjectOfType<ItemPickup>();
            if (pickupTemplate != null)
            {
                MelonLogger.Msg($"[ItemSpawner] Using pickup template: {pickupTemplate.gameObject.name}");
            }
            else
            {
                MelonLogger.Warning("[ItemSpawner] No ItemPickup found in scene. Drop will not work until one exists.");
            }
        }

        // Valódi Drop: 1 db ItemPickup spawn a földre
        private void SpawnPickup(ItemDefinition def)
        {
            if (def == null)
            {
                MelonLogger.Warning("[ItemSpawner] SpawnPickup: def is null.");
                return;
            }

            if (PlayerMovement.Instance == null)
            {
                MelonLogger.Warning("[ItemSpawner] PlayerMovement.Instance is null, cannot drop item.");
                return;
            }

            EnsurePickupTemplate();
            if (pickupTemplate == null)
            {
                MelonLogger.Warning("[ItemSpawner] No pickupTemplate available, cannot Drop.");
                return;
            }

            Transform player = PlayerMovement.Instance.transform;

            // Kiinduló pozíció: játékos előtt kicsit
            Vector3 origin = player.position + player.forward * 1.5f + Vector3.up * 0.5f;
            Vector3 spawnPos = origin;

            // Raycast lefele, hogy a talajra tegye
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                spawnPos = hit.point + Vector3.up * 0.05f;
            }

            GameObject go = UnityEngine.Object.Instantiate(pickupTemplate.gameObject, spawnPos, Quaternion.identity);
            ItemPickup ip = go.GetComponent<ItemPickup>();
            if (ip != null)
            {
                ip.ItemToGive = def;           // mit adjon felvételkor
                ip.DestroyOnPickup = true;
                ip.Networked = pickupTemplate.Networked;
            }

            MelonLogger.Msg($"[ItemSpawner] Dropped 1x {def.Name} at {spawnPos}");
        }

        // ======================
        // ONE PUNCH (LMB)
        // ======================
        private void HandleOnePunch()
        {
            if (!onePunchEnabled) return;

            if (Input.GetMouseButtonDown(0))
            {
                Camera cam = Camera.main;
                if (cam == null) return;

                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
                {
                    NPCHealth npc = hit.collider.GetComponentInParent<NPCHealth>();
                    if (npc != null)
                    {
                        npc.TakeDamage(999999f, true);
                        MelonLogger.Msg($"[OnePunch] KO: {npc.gameObject.name}");
                    }
                    else
                    {
                        MelonLogger.Msg("[OnePunch] No NPCHealth found on hit.");
                    }
                }
            }
        }

        // ======================
        // PET MODE (MMB)
        // ======================
        private void HandleMindControl()
        {
            if (!mindControlEnabled) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            // Toggle pet
            if (Input.GetMouseButtonDown(2))
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
                {
                    NPCHealth npc = hit.collider.GetComponentInParent<NPCHealth>();

                    if (npc != null)
                    {
                        if (controlledNpcs.Contains(npc))
                        {
                            controlledNpcs.Remove(npc);
                            MelonLogger.Msg($"[PET] Removed: {npc.gameObject.name}");
                        }
                        else
                        {
                            controlledNpcs.Add(npc);
                            MelonLogger.Msg($"[PET] Added: {npc.gameObject.name}");
                        }
                    }
                }
            }

            if (PlayerMovement.Instance == null) return;

            Transform playerT = PlayerMovement.Instance.transform;

            List<NPCHealth> toRemove = new List<NPCHealth>();

            foreach (var npc in controlledNpcs)
            {
                if (npc == null)
                {
                    toRemove.Add(npc);
                    continue;
                }

                if (npc.IsDead)
                    npc.Revive();

                Vector3 target = playerT.position - playerT.forward * 2f;
                npc.transform.position = Vector3.MoveTowards(
                    npc.transform.position,
                    target,
                    petFollowSpeed * Time.deltaTime
                );
            }

            foreach (var n in toRemove)
                controlledNpcs.Remove(n);
        }

        // ======================
        // BLACK HOLE (B)
        // ======================
        private void HandleBlackHole()
        {
            if (blackHoleEnabled && Input.GetKeyDown(KeyCode.B))
            {
                Camera cam = Camera.main;
                if (cam == null) return;

                blackHoleCenter = cam.transform.position + cam.transform.forward * 12f;
                blackHoleEndTime = Time.time + blackHoleDuration;
                blackHoleActive = true;
                MelonLogger.Msg("[BlackHole] Activated");
            }

            if (!blackHoleActive) return;
            if (Time.time >= blackHoleEndTime)
            {
                blackHoleActive = false;
                return;
            }

            NPCHealth[] all = GameObject.FindObjectsOfType<NPCHealth>();

            foreach (var npc in all)
            {
                if (npc == null) continue;

                float dist = Vector3.Distance(npc.transform.position, blackHoleCenter);
                if (dist > blackHoleRadius) continue;

                Vector3 dir = (blackHoleCenter - npc.transform.position).normalized;
                npc.transform.position += dir * (blackHoleForce * Time.deltaTime);

                if (dist < 1.5f)
                    npc.TakeDamage(999999f, true);
            }
        }

        // ======================
        // THROWBACK NPC (RMB → ragdoll + force)
        // ======================
        private void HandleThrowbackNPC()
        {
            if (!throwbackEnabled) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            if (Input.GetMouseButtonDown(1))
            {
                MelonLogger.Msg("[Throwback] RMB pressed.");

                if (Time.time < nextThrowTime)
                {
                    MelonLogger.Msg("[Throwback] On cooldown.");
                    return;
                }

                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
                {
                    NPCHealth npc = hit.collider.GetComponentInParent<NPCHealth>();
                    if (npc == null)
                    {
                        MelonLogger.Msg("[Throwback] No NPCHealth on hit.");
                        return;
                    }

                    NPCMovement move =
                        npc.GetComponent<NPCMovement>() ??
                        npc.GetComponentInChildren<NPCMovement>() ??
                        npc.GetComponentInParent<NPCMovement>();

                    Vector3 dir = cam.transform.forward;
                    Vector3 forcePoint = hit.point != Vector3.zero ? hit.point : npc.transform.position + Vector3.up * 1f;

                    if (move != null)
                    {
                        move.ActivateRagdoll(forcePoint, dir, throwForce);
                        MelonLogger.Msg($"[Throwback] Ragdoll force → {npc.gameObject.name}, F={throwForce}");
                    }
                    else
                    {
                        Rigidbody rb = hit.rigidbody;
                        if (rb == null)
                            rb = npc.GetComponent<Rigidbody>();

                        if (rb != null)
                        {
                            rb.AddForce(dir * throwForce, ForceMode.Impulse);
                            MelonLogger.Msg($"[Throwback] Rigidbody force (fallback) → {npc.gameObject.name}, F={throwForce}");
                        }
                        else
                        {
                            float distance = throwForce * 0.3f;
                            npc.transform.position += dir * distance;
                            MelonLogger.Msg($"[Throwback] Transform push (fallback) → {npc.gameObject.name}, Dist={distance}");
                        }
                    }

                    nextThrowTime = Time.time + throwCooldown;
                }
                else
                {
                    MelonLogger.Msg("[Throwback] Raycast hit nothing.");
                }
            }
        }

        // ======================
        // DELETE NPC (X)
        // ======================
        private void HandleDeleteNPC()
        {
            if (!deleteNpcEnabled) return;

            if (Input.GetKeyDown(KeyCode.X))
            {
                Camera cam = Camera.main;
                if (cam == null) return;

                Ray ray = cam.ScreenPointToRay(Input.mousePosition);

                if (Physics.Raycast(ray, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
                {
                    NPCHealth npc = hit.collider.GetComponentInParent<NPCHealth>();
                    if (npc != null)
                    {
                        MelonLogger.Msg($"[DeleteNPC] Deleted {npc.gameObject.name}");
                        UnityEngine.Object.Destroy(npc.gameObject);
                    }
                }
            }
        }

        // ======================
        // GUI + MENÜ
        // ======================
        public override void OnGUI()
        {
            if (!showGUI)
            {
                if (npcEspEnabled)
                    DrawNPCESP();


                return;
            }

            ApplySkin();

            windowRect = GUI.Window(1, windowRect, DrawWindow, "Schedule I Mod Menu");

            if (npcEspEnabled)
                DrawNPCESP();
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(160));
            DrawSidebar();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(560));
            DrawSelectedTab();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        // ======================
        // SIDEBAR – KATEGÓRIÁK
        // ======================
        private void DrawSidebar()
        {
            GUILayout.Label("<b>Categories</b>", labelStyle);

            if (GUILayout.Button("Player")) selectedTab = 0;
            if (GUILayout.Button("Money")) selectedTab = 1;
            if (GUILayout.Button("NPC")) selectedTab = 2;
            if (GUILayout.Button("ESP")) selectedTab = 3;
            if (GUILayout.Button("Utility")) selectedTab = 4;
            if (GUILayout.Button("UI Skin")) selectedTab = 5;
            if (GUILayout.Button("Items")) selectedTab = 6;
            if (GUILayout.Button("Products")) selectedTab = 7;
        }

        private void DrawSelectedTab()
        {
            switch (selectedTab)
            {
                case 0: DrawPlayerTab(); break;
                case 1: DrawMoneyTab(); break;
                case 2: DrawNPCTab(); break;
                case 3: DrawESPTab(); break;
                case 4: DrawUtilityTab(); break;
                case 5: DrawSkinTab(); break;
                case 6: DrawItemSpawnerTab(); break;
                case 7: DrawProductDebugTab(); break;
            }
        }

        // ======================
        // PLAYER TAB (MOVEMENT)
        // ======================
        private void DrawPlayerTab()
        {
            GUILayout.Label("<size=16><b>Player Movement</b></size>", labelStyle);
            GUILayout.Space(10);

            GUILayout.Label($"Walk Speed: {walkSpeed:F1}");
            walkSpeed = GUILayout.HorizontalSlider(walkSpeed, 1f, 50f);
            if (GUILayout.Button("Apply Speed"))
            {
                PlayerMovement.WalkSpeed = walkSpeed;
                MelonLogger.Msg($"WalkSpeed set to {walkSpeed}");
            }

            GUILayout.Space(20);

            GUILayout.Label($"Jump Multiplier: {jumpMultiplier:F1}");
            jumpMultiplier = GUILayout.HorizontalSlider(jumpMultiplier, 1f, 50f);
            if (GUILayout.Button("Apply Jump"))
            {
                PlayerMovement.JumpMultiplier = jumpMultiplier;
                MelonLogger.Msg($"JumpMultiplier set to {jumpMultiplier}");
            }
        }

        // ======================
        // MONEY TAB
        // ======================
        private void DrawMoneyTab()
        {
            GUILayout.Label("<size=16><b>Money Editor</b></size>", labelStyle);
            GUILayout.Space(10);

            GUILayout.Label("Give Money:");
            moneyInput = GUILayout.TextField(moneyInput, 10);

            if (GUILayout.Button("Apply"))
            {
                if (float.TryParse(moneyInput, out float value))
                {
                    NetworkSingleton<MoneyManager>.Instance.ChangeCashBalance(value, true, false);
                    MelonLogger.Msg($"Gave ${value}");
                }
                else MelonLogger.Warning("Invalid value!");
            }
        }

        // ======================
        // NPC TAB
        // ======================
        private void DrawNPCTab()
        {
            GUILayout.Label("<size=16><b>NPC Controls</b></size>", labelStyle);
            GUILayout.Space(10);

            onePunchEnabled = GUILayout.Toggle(onePunchEnabled, " One Punch Kill (LMB)");
            mindControlEnabled = GUILayout.Toggle(mindControlEnabled, " Pet NPC (MMB)");
            blackHoleEnabled = GUILayout.Toggle(blackHoleEnabled, " Black Hole (B)");

            bool newBrainToggle = GUILayout.Toggle(brainOverrideEnabled, " Brain Override (Frozen / Panic)");
            if (newBrainToggle != brainOverrideEnabled)
            {
                brainOverrideEnabled = newBrainToggle;
                if (brainOverrideEnabled)
                    SetBrainMode(brainMode);
                else
                    SetBrainMode(BrainMode.Normal);
            }

            deleteNpcEnabled = GUILayout.Toggle(deleteNpcEnabled, " Delete NPC (X)");
            throwbackEnabled = GUILayout.Toggle(throwbackEnabled, " Throwback NPC (RMB ragdoll push)");

            GUILayout.Space(10);

            GUILayout.Label($"Throw Force: {throwForce:F0}");
            throwForce = GUILayout.HorizontalSlider(throwForce, 1f, 300f);

            GUILayout.Label($"Throw Cooldown: {throwCooldown:0.00} s");
            throwCooldown = GUILayout.HorizontalSlider(throwCooldown, 0f, 1f);

            GUILayout.Space(10);

            if (brainOverrideEnabled)
            {
                GUILayout.Label("Brain Mode:");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Normal")) SetBrainMode(BrainMode.Normal);
                if (GUILayout.Button("Frozen")) SetBrainMode(BrainMode.Frozen);
                if (GUILayout.Button("Panic")) SetBrainMode(BrainMode.Panic);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(15);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("NUKE ALL NPCs"))
                NukeAllNPCs();

            if (GUILayout.Button("REVIVE ALL NPCs"))
                ReviveAllNPCs();
            GUILayout.EndHorizontal();
        }

        // ======================
        // ESP TAB
        // ======================
        private void DrawESPTab()
        {
            GUILayout.Label("<size=16><b>ESP</b></size>", labelStyle);
            GUILayout.Space(10);

            npcEspEnabled = GUILayout.Toggle(npcEspEnabled, " NPC ESP (Name + Dist + Box + HP)\nPets = cyan, normál NPC = green");
        }

        // ======================
        // UTILITY TAB
        // ======================
        private void DrawUtilityTab()
        {
            GUILayout.Label("<size=16><b>Utility</b></size>", labelStyle);
            GUILayout.Space(20);

            if (GUILayout.Button("Save Game Now"))
            {
                try
                {
                    SaveManager.Instance.Save();
                    MelonLogger.Msg("Game Saved!");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning("Save failed: " + ex.Message);
                }
            }
        }

        // ======================
        // SKINS TAB
        // ======================
        private void DrawSkinTab()
        {
            GUILayout.Label("<size=16><b>UI Skins</b></size>", labelStyle);
            GUILayout.Space(15);

            if (GUILayout.Button("Classic Skin")) currentSkin = SkinType.Classic;
            if (GUILayout.Button("Dark Mode")) currentSkin = SkinType.Dark;
            if (GUILayout.Button("Neon Hacker Style")) currentSkin = SkinType.Neon;
        }

        private void DrawProductDebugTab()
        {
            var pm = NetworkSingleton<ProductManager>.Instance;
            if (pm == null)
            {
                GUILayout.Label("ProductManager not found (nincs játék betöltve?)");
                return;
            }

            GUILayout.Label("<b>PRODUCT DEBUG MODE</b>", _headerStyle ?? GUI.skin.label);
            GUILayout.Space(5f);
            GUILayout.Label("Itt mindent fel tudsz oldani / listázni / spawnolni (csak debugra).");

            GUILayout.Space(10f);

            // ─────────────────────────────────────────────
            // GLOBAL DEBUG GOMBOK
            // ─────────────────────────────────────────────

            if (GUILayout.Button("★ Discover + LIST ALL PRODUCTS (full unlock)", GUILayout.Height(30f)))
            {
                foreach (var p in pm.AllProducts)
                {
                    // conn = null, productID = p.ID, autoList = true
                    pm.SetProductDiscovered(null, p.ID, true);
                }

                Debug.Log("[ModMenu] Product Debug: összes termék felfedezve és listázva.");
            }

            if (GUILayout.Button("✖ Clear Discovered/List status (csak local listák)", GUILayout.Height(25f)))
            {
                ProductManager.DiscoveredProducts.Clear();
                ProductManager.ListedProducts.Clear();
                Debug.Log("[ModMenu] Product Debug: DiscoveredProducts + ListedProducts ürítve (local).");
            }

            GUILayout.Space(10f);
            GUILayout.Label("Egyedi termékek:");

            // ─────────────────────────────────────────────
            // PER-PRODUCT LISTA
            // ─────────────────────────────────────────────

            _productScroll = GUILayout.BeginScrollView(_productScroll, GUILayout.Height(400f));

            foreach (var p in pm.AllProducts)
            {
                if (p == null) continue;

                bool discovered = ProductManager.DiscoveredProducts.Contains(p);
                bool listed = ProductManager.ListedProducts.Contains(p);

                GUILayout.BeginHorizontal();

                // Név + ID
                GUILayout.Label($"{p.Name}  ({p.ID})", GUILayout.Width(260f));

                // State label-ek
                GUILayout.Label(discovered ? "Discovered" : "Hidden", GUILayout.Width(80f));
                GUILayout.Label(listed ? "Listed" : "Unlisted", GUILayout.Width(80f));

                // Felfedezés (autoList = false)
                if (GUILayout.Button("Discover", GUILayout.Width(90f)))
                {
                    pm.SetProductDiscovered(null, p.ID, false);
                }

                // Felfedezés + automatikus listázás (autoList = true)
                if (GUILayout.Button("Discover+List", GUILayout.Width(110f)))
                {
                    pm.SetProductDiscovered(null, p.ID, true);
                }

                // Unlist
                if (GUILayout.Button("Unlist", GUILayout.Width(70f)))
                {
                    pm.SetProductListed(p.ID, false);
                }

                // Give 1 db item a playernek
                if (GUILayout.Button("Give x1", GUILayout.Width(70f)))
                {
                    var def = Registry.GetItem<ProductDefinition>(p.ID);
                    if (def != null)
                    {
                        PlayerSingleton<PlayerInventory>.Instance
                            .AddItemToInventory(def.GetDefaultInstance(1));
                    }
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }



        // ======================
        // ITEM SPAWNER TAB
        // ======================
        private void DrawItemSpawnerTab()
        {
            GUILayout.Label("<size=16><b>Item Spawner</b></size>", labelStyle);
            GUILayout.Space(10);

            // Search bar
            GUILayout.Label("Search:");
            itemSearch = GUILayout.TextField(itemSearch, 50);

            GUILayout.Space(5);

            // Quantity slider (Give-hez)
            GUILayout.Label($"Quantity (Give): {itemQuantity}");
            itemQuantity = (int)GUILayout.HorizontalSlider(itemQuantity, 1, 50);

            GUILayout.Space(10);

            // Category filter
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("ALL"))
            {
                itemCategoryFilterEnabled = false;
            }
            if (GUILayout.Button("Product"))
            {
                itemCategoryFilterEnabled = true;
                selectedItemCategory = EItemCategory.Product;
            }
            if (GUILayout.Button("Tools"))
            {
                itemCategoryFilterEnabled = true;
                selectedItemCategory = EItemCategory.Tools;
            }
            if (GUILayout.Button("Consumable"))
            {
                itemCategoryFilterEnabled = true;
                selectedItemCategory = EItemCategory.Consumable;
            }
            if (GUILayout.Button("Equipment"))
            {
                itemCategoryFilterEnabled = true;
                selectedItemCategory = EItemCategory.Equipment;
            }
            if (GUILayout.Button("Cash"))
            {
                itemCategoryFilterEnabled = true;
                selectedItemCategory = EItemCategory.Cash;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Item list (scrollable)
            itemScrollPos = GUILayout.BeginScrollView(itemScrollPos, GUILayout.Width(550), GUILayout.Height(300));

            foreach (var pair in allItemDefs)
            {
                ItemDefinition def = pair.Value;
                if (def == null) continue;

                // Filter: search
                if (!string.IsNullOrEmpty(itemSearch))
                {
                    string s = itemSearch.ToLower();
                    if (!def.Name.ToLower().Contains(s) && !def.ID.ToLower().Contains(s))
                        continue;
                }

                // Filter: category
                if (itemCategoryFilterEnabled && def.Category != selectedItemCategory)
                    continue;

                GUILayout.BeginHorizontal("box");

                GUILayout.Label(def.Icon != null ? def.Icon.texture : Texture2D.blackTexture, GUILayout.Width(40), GUILayout.Height(40));

                GUILayout.BeginVertical();
                GUILayout.Label("<b>" + def.Name + "</b>", labelStyle);
                GUILayout.Label("<size=11>" + def.ID + "</size>");
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                // GIVE
                if (GUILayout.Button("Give", GUILayout.Width(60)))
                {
                    try
                    {
                        var inst = def.GetDefaultInstance(itemQuantity);
                        PlayerSingleton<PlayerInventory>.Instance.AddItemToInventory(inst);
                        MelonLogger.Msg($"[ItemSpawner] Gave {itemQuantity}x {def.Name}");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning("[ItemSpawner] Give failed: " + ex.Message);
                    }
                }

                // DROP (Single Drop – mindig 1 db pickup)
                if (GUILayout.Button("Drop", GUILayout.Width(60)))
                {
                    SpawnPickup(def);
                }

                // EQUIP (inventory + automata equip)
                if (GUILayout.Button("Equip", GUILayout.Width(60)))
                {
                    EquipItemViaInventory(def);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private void ApplySkin()
        {
            labelStyle = new GUIStyle(GUI.skin.label) { richText = true };

            switch (currentSkin)
            {
                case SkinType.Classic:
                    GUI.backgroundColor = Color.white;
                    GUI.contentColor = Color.black;
                    break;
                case SkinType.Dark:
                    GUI.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
                    GUI.contentColor = Color.white;
                    break;
                case SkinType.Neon:
                    GUI.backgroundColor = new Color(0f, 0.8f, 0.1f);
                    GUI.contentColor = Color.black;
                    break;
            }
        }

        // ======================
        // BRAIN OVERRIDE (NPCMovement.MoveSpeedMultiplier)
        // ======================
        private void SetBrainMode(BrainMode mode)
        {
            brainMode = mode;

            NPCHealth[] all = GameObject.FindObjectsOfType<NPCHealth>();

            foreach (var npc in all)
            {
                if (npc == null) continue;

                NPCMovement move =
                    npc.GetComponent<NPCMovement>() ??
                    npc.GetComponentInChildren<NPCMovement>() ??
                    npc.GetComponentInParent<NPCMovement>();

                if (move == null)
                    continue;

                if (!originalMoveMultipliers.ContainsKey(move))
                    originalMoveMultipliers[move] = move.MoveSpeedMultiplier;

                float baseMult = originalMoveMultipliers[move];

                switch (mode)
                {
                    case BrainMode.Normal:
                        move.MoveSpeedMultiplier = baseMult;
                        break;

                    case BrainMode.Frozen:
                        move.MoveSpeedMultiplier = 0f;
                        break;

                    case BrainMode.Panic:
                        move.MoveSpeedMultiplier = baseMult * 3f;
                        break;
                }
            }

            MelonLogger.Msg($"[BrainOverride] Mode set to {brainMode}");
        }

        // ======================
        // EQUIP VIA INVENTORY
        // ======================
        private void EquipItemViaInventory(ItemDefinition def)
        {
            try
            {
                var inv = PlayerSingleton<PlayerInventory>.Instance;
                if (inv == null)
                {
                    MelonLogger.Warning("[ItemSpawner] PlayerInventory.Instance is null, cannot equip.");
                    return;
                }

                var inst = def.GetDefaultInstance(1);
                if (!inv.CanItemFitInInventory(inst, 1))
                {
                    MelonLogger.Warning("[ItemSpawner] Inventory full, cannot equip.");
                    return;
                }

                // Beletesszük az inventoryba
                inv.AddItemToInventory(inst);

                // Megkeressük, melyik slotban van
                var slots = inv.hotbarSlots;
                int index = -1;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i].ItemInstance != null &&
                        string.Equals(slots[i].ItemInstance.ID, def.ID, StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        break;
                    }
                }

                if (index == -1)
                {
                    MelonLogger.Warning("[ItemSpawner] Could not find item in hotbar to equip.");
                    return;
                }

                // Ha már volt valami equipelve, unequip
                if (inv.EquippedSlotIndex >= 0 &&
                    inv.EquippedSlotIndex < slots.Count &&
                    slots[inv.EquippedSlotIndex] != null)
                {
                    slots[inv.EquippedSlotIndex].Unequip();
                }

                // Equip hívása
                inv.Equip(slots[index]);

                // Protected setter megkerülése reflektálással
                PropertyInfo prop = typeof(PlayerInventory).GetProperty(
                    "EquippedSlotIndex",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (prop != null && prop.CanWrite)
                    prop.SetValue(inv, index, null);

                MelonLogger.Msg($"[ItemSpawner] Equipped {def.Name} (slot {index + 1})");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[ItemSpawner] Equip failed: " + ex.Message);
            }
        }

        // ======================
        // NUKE ALL NPCs
        // ======================
        private void NukeAllNPCs()
        {
            NPCHealth[] all = GameObject.FindObjectsOfType<NPCHealth>();
            int cnt = 0;

            foreach (var npc in all)
            {
                if (npc == null) continue;
                npc.TakeDamage(999999f, true);
                cnt++;
            }

            MelonLogger.Msg($"[NUKE] KO'd {cnt} NPC(s).");
        }

        // ======================
        // REVIVE ALL NPCs
        // ======================
        private void ReviveAllNPCs()
        {
            NPCHealth[] all = GameObject.FindObjectsOfType<NPCHealth>();
            int cnt = 0;

            foreach (var npc in all)
            {
                if (npc == null) continue;

                npc.Revive();
                cnt++;
            }

            MelonLogger.Msg($"[ReviveAll] Revived {cnt} NPC(s).");
        }

        // ======================
        // NPC ESP
        // ======================
        private void DrawNPCESP()
        {
            if (espStyle == null)
            {
                espStyle = new GUIStyle();
                espStyle.fontSize = 14;
                espStyle.alignment = TextAnchor.MiddleCenter;
            }

            Camera cam = Camera.main;
            if (cam == null) return;

            foreach (var npc in cachedNPCs)
            {
                if (npc == null) continue;

                Transform t = npc.transform;
                Vector3 pos = t.position;
                Vector3 head = pos + Vector3.up * 1.8f;

                Vector3 screen = cam.WorldToScreenPoint(pos);
                Vector3 screenHead = cam.WorldToScreenPoint(head);

                if (screen.z <= 0) continue;

                float y = Screen.height - screen.y;
                float y2 = Screen.height - screenHead.y;

                float height = Mathf.Abs(y2 - y);
                float width = height * 0.4f;
                float x = screen.x - width / 2f;

                bool isPet = controlledNpcs.Contains(npc);
                Color outline = isPet ? Color.cyan : Color.green;

                DrawBox(x, y2, width, height, outline);

                float dist = Vector3.Distance(cam.transform.position, pos);
                string name = npc.gameObject.name.Replace("(Clone)", "");

                espStyle.normal.textColor = isPet ? Color.cyan : Color.white;

                GUI.Label(new Rect(screen.x - 75, y2 - 20, 150, 20),
                    $"{name} [{dist:0}m]", espStyle);

                float hp = npc.Health;
                float max = npc.MaxHealth > 0 ? npc.MaxHealth : 100f;
                float percent = Mathf.Clamp01(hp / max);

                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(x, y2 - 10, width, 5), Texture2D.whiteTexture);

                GUI.color = Color.red;
                GUI.DrawTexture(new Rect(x, y2 - 10, width * percent, 5), Texture2D.whiteTexture);

                GUI.color = Color.white;
            }
        }

        private void DrawBox(float x, float y, float w, float h, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, w, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + h, w, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y, 1, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + w, y, 1, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
