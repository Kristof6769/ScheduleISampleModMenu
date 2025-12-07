using FishNet;
using FishNet.Managing;
using MelonLoader;
using ScheduleOne.DevUtilities;
using ScheduleOne.ItemFramework;
using ScheduleOne.Money;
using ScheduleOne.NPCs;
using ScheduleOne.Persistence;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Product;
using ScheduleOne.Variables;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ScheduleOne.PlayerScripts.Health;

// CREATED BY KRISTOF67
// MOD MENU FOR SCHEDULE I – FREE SAMPLE

namespace ScheduleIMod
{
    public class ModMenuClass : MelonMod
    {
        private bool showGUI = false;
        private int selectedTab = 0;

        // Window position & size
        private Rect windowRect = new Rect(40, 40, 750, 450);

        // Resizing
        private bool isResizing = false;
        private Vector2 resizeStartMouse;
        private Vector2 resizeStartSize;
        private const float MinWindowWidth = 600f;
        private const float MinWindowHeight = 350f;

        // Theme
        private enum SkinType { Classic, Dark, Neon }
        private SkinType currentSkin = SkinType.Dark; // default to dark minimal

        private GUIStyle labelStyle;
        private GUIStyle headerLabelStyle;
        private GUIStyle sidebarButtonStyle;
        private GUIStyle sidebarActiveButtonStyle;
        private GUIStyle tabHeaderStyle;
        private GUIStyle espStyle;
        private Texture2D darkBgTex;
        private Texture2D darkerBgTex;
        private Texture2D accentTex;
        private Texture2D resizeHandleTex;

        // Product debug scroll
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

        // ===== PET NPCs =====
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

        // Original NPCMovement.MoveSpeedMultiplier
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
        private readonly Dictionary<string, ItemDefinition> allItemDefs = new Dictionary<string, ItemDefinition>();
        private bool itemListInitialized = false;

        // Category filter
        private bool itemCategoryFilterEnabled = false;
        private EItemCategory selectedItemCategory = EItemCategory.Product;

        // ===== PICKUP TEMPLATE (used for real item drops) =====
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

        // Find pickup template (first ItemPickup in scene)
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

        // Spawn a real ItemPickup on the ground
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

            // Start position: a bit in front of the player
            Vector3 origin = player.position + player.forward * 1.5f + Vector3.up * 0.5f;
            Vector3 spawnPos = origin;

            // Raycast down to place on ground
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                spawnPos = hit.point + Vector3.up * 0.05f;
            }

            GameObject go = UnityEngine.Object.Instantiate(pickupTemplate.gameObject, spawnPos, Quaternion.identity);
            ItemPickup ip = go.GetComponent<ItemPickup>();
            if (ip != null)
            {
                ip.ItemToGive = def;
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

        // ===== PLAYER HEALTH HELPERS =====
        private PlayerHealth GetLocalPlayerHealth()
        {
            if (Player.Local == null)
                return null;

            return Player.Local.GetComponent<PlayerHealth>();
        }

        private float GetPlayerHealth()
        {
            var h = GetLocalPlayerHealth();
            return h != null ? h.CurrentHealth : 0f;
        }

        private void SetPlayerHealth(float value)
        {
            var h = GetLocalPlayerHealth();
            if (h == null) return;

            h.SetHealth(value);
        }

        private void HealPlayerFull()
        {
            var h = GetLocalPlayerHealth();
            if (h == null) return;

            h.SetHealth(PlayerHealth.MAX_HEALTH);
        }

        private void KillPlayer()
        {
            var h = GetLocalPlayerHealth();
            if (h == null) return;

            h.SetHealth(0f);
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

                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

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
        // GUI
        // ======================
        public override void OnGUI()
        {
            if (Event.current.type == EventType.Layout || Event.current.type == EventType.Repaint)
            {
                ApplySkin();
                InitStylesIfNeeded();
            }

            if (!showGUI)
            {
                if (npcEspEnabled)
                    DrawNPCESP();
                    DrawOverlay();

                return;
            }

            // Draw window
            windowRect = GUI.Window(1, windowRect, DrawWindow, GUIContent.none);

            if (npcEspEnabled)
                DrawNPCESP();
        }

        // ======================
        // FPS COUNTER + KEYBIND OVERLAY
        // ======================

        private float fpsDeltaTime = 0.0f;

        public override void OnLateUpdate()
        {
            // FPS calculation
            fpsDeltaTime += (Time.unscaledDeltaTime - fpsDeltaTime) * 0.1f;
        }

        // Draw overlay elements ON TOP of everything
        private void DrawOverlay()
        {
            DrawFPSCounter();
            DrawKeybindHelper();
        }


        // ---------------- FPS COUNTER (Top-Right) ----------------
        private void DrawFPSCounter()
        {
            float fps = 1.0f / fpsDeltaTime;
            string text = $"{fps:0} FPS";

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 18;
            style.normal.textColor = Color.cyan;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.UpperRight;

            GUI.color = new Color(0, 0, 0, 0.35f);
            GUI.DrawTexture(new Rect(Screen.width - 140, 10, 130, 32), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(Screen.width - 140, 10, 130, 32), text, style);
        }


        // ---------------- KEYBIND HELP BOX (Top-Left) ----------------
        private void DrawKeybindHelper()
        {
            List<string> lines = new List<string>();

            lines.Add("F1 - Toggle Menu");

            if (onePunchEnabled)
                lines.Add("LMB - One Punch");

            if (mindControlEnabled)
                lines.Add("MMB - Pet NPC");

            if (blackHoleEnabled)
                lines.Add("B - Black Hole");

            if (deleteNpcEnabled)
                lines.Add("X - Delete NPC");

            if (throwbackEnabled)
                lines.Add("RMB - Throw NPC");

            if (lines.Count == 0)
                return;

            float boxWidth = 210;
            float lineHeight = 22;
            float boxHeight = lineHeight * lines.Count + 10;

            // Background
            GUI.color = new Color(0, 0, 0, 0.45f);
            GUI.DrawTexture(new Rect(10, 10, boxWidth, boxHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Border
            DrawBorder(new Rect(10, 10, boxWidth, boxHeight), 2, Color.cyan);

            // Text
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 14;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleLeft;

            float y = 15;
            foreach (string line in lines)
            {
                GUI.Label(new Rect(20, y, boxWidth - 20, lineHeight), line, style);
                y += lineHeight;
            }
        }


        // Draw box border
        private void DrawBorder(Rect rect, int thickness, Color color)
        {
            GUI.color = color;

            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture); // Top
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture); // Bottom
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture); // Left
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture); // Right

            GUI.color = Color.white;
        }


        private void DrawWindow(int id)
        {
            // Dark background for the whole window
            GUI.DrawTexture(new Rect(0, 0, windowRect.width, windowRect.height), darkerBgTex);

            // Header
            GUILayout.BeginVertical(tabHeaderStyle);
            GUILayout.Space(4);
            GUILayout.Label("SCHEDULE I – MOD MENU", headerLabelStyle);
            GUILayout.Space(4);
            GUILayout.EndVertical();

            GUILayout.Space(4);

            GUILayout.BeginHorizontal();

            // Sidebar – fixed width
            GUILayout.BeginVertical(GUILayout.Width(170));
            DrawSidebar();
            GUILayout.EndVertical();

            // Content – flexible width
            GUILayout.BeginVertical();
            DrawSelectedTab();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            // Draw resize handle (bottom-right)
            DrawResizeHandle();

            // Make the header draggable (excluding the resize corner)
            GUI.DragWindow(new Rect(0, 0, windowRect.width - 24f, 24f));
        }

        // ======================
        // RESIZE HANDLE
        // ======================
        private void DrawResizeHandle()
        {
            Event e = Event.current;

            Rect handleRect = new Rect(
                windowRect.width - 18f,
                windowRect.height - 18f,
                16f,
                16f
            );

            // Small diagonal triangle-ish handle
            GUI.DrawTexture(handleRect, resizeHandleTex);

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && handleRect.Contains(e.mousePosition))
                    {
                        isResizing = true;
                        resizeStartMouse = e.mousePosition;
                        resizeStartSize = new Vector2(windowRect.width, windowRect.height);
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (isResizing && e.button == 0)
                    {
                        Vector2 delta = e.mousePosition - resizeStartMouse;

                        float newWidth = Mathf.Clamp(resizeStartSize.x + delta.x, MinWindowWidth, Screen.width);
                        float newHeight = Mathf.Clamp(resizeStartSize.y + delta.y, MinWindowHeight, Screen.height);

                        windowRect.width = newWidth;
                        windowRect.height = newHeight;

                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (isResizing && e.button == 0)
                    {
                        isResizing = false;
                        e.Use();
                    }
                    break;
            }
        }

        // ======================
        // SIDEBAR – CATEGORIES
        // ======================
        private void DrawSidebar()
        {
            GUILayout.Label("<b>CATEGORIES</b>", labelStyle);
            GUILayout.Space(4);

            DrawSidebarButton("Player", 0);
            DrawSidebarButton("Money", 1);
            DrawSidebarButton("NPC", 2);
            DrawSidebarButton("ESP", 3);
            DrawSidebarButton("Utility", 4);
            DrawSidebarButton("UI Skin", 5);
            DrawSidebarButton("Items", 6);
            DrawSidebarButton("Products", 7);
        }

        private void DrawSidebarButton(string title, int tabIndex)
        {
            GUIStyle style = selectedTab == tabIndex ? sidebarActiveButtonStyle : sidebarButtonStyle;
            if (GUILayout.Button(title, style))
                selectedTab = tabIndex;
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
                case 7: DrawProductTab(); break;
            }
        }

        // ======================
        // PLAYER TAB (MOVEMENT)
        // ======================
        private void DrawPlayerTab()
        {
            GUILayout.Label("<size=16><b>Player Movement</b></size>", labelStyle);
            GUILayout.Space(10);

            // ----- Movement -----
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

            GUILayout.Space(25);
            GUILayout.Label("<size=14><b>Player Health</b></size>", labelStyle);
            GUILayout.Space(5);

            var health = GetPlayerHealth();
            GUILayout.Label($"Health: {health:0}/{PlayerHealth.MAX_HEALTH:0}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Heal to Full", GUILayout.Width(120)))
            {
                HealPlayerFull();
                MelonLogger.Msg("[Health] Healed player to full.");
            }

            if (GUILayout.Button("Kill Player", GUILayout.Width(120)))
            {
                KillPlayer();
                MelonLogger.Msg("[Health] Killed player.");
            }
            GUILayout.EndHorizontal();
        }


        // ======================
        // MONEY TAB
        // ======================
        private void DrawMoneyTab()
        {
            GUILayout.Label("<size=16><b>Money Editor</b></size>", labelStyle);
            GUILayout.Space(10);

            GUILayout.Label("Give Money:", labelStyle);
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

            GUILayout.Label($"Throw Force: {throwForce:F0}", labelStyle);
            throwForce = GUILayout.HorizontalSlider(throwForce, 1f, 300f);

            GUILayout.Label($"Throw Cooldown: {throwCooldown:0.00} s", labelStyle);
            throwCooldown = GUILayout.HorizontalSlider(throwCooldown, 0f, 1f);

            GUILayout.Space(10);

            if (brainOverrideEnabled)
            {
                GUILayout.Label("Brain Mode:", labelStyle);
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

            npcEspEnabled = GUILayout.Toggle(
                npcEspEnabled,
                " NPC ESP (Name + Distance + Box + HP)\nPets = cyan, normal NPC = green"
            );
        }

        // ======================
        // UTILITY TAB
        // ======================
        private static bool godMode = false;
        private static bool freezeTime = false;

        private void DrawUtilityTab()
        {
            GUILayout.Label("<size=16><b>Utility</b></size>", labelStyle);
            GUILayout.Space(15);

            // === SAVE GAME ===
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

            GUILayout.Space(10);

            // === FREEZE TIME ===
            if (GUILayout.Button(freezeTime ? "Unfreeze Time" : "Freeze Time"))
            {
                freezeTime = !freezeTime;

                NPCMovement[] npcs = GameObject.FindObjectsOfType<NPCMovement>();
                foreach (var m in npcs)
                {
                    if (m == null) continue;
                    m.MoveSpeedMultiplier = freezeTime ? 0f : 1f;
                }

                MelonLogger.Msg("Freeze Time = " + freezeTime);
            }

            GUILayout.Space(10);

            // === RESTART SCENE ===
            if (GUILayout.Button("Restart Scene"))
            {
                MelonLogger.Msg("Restarting scene...");
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                );
            }

            GUILayout.Space(10);

            // === QUIT GAME ===
            if (GUILayout.Button("Quit Game"))
            {
                MelonLogger.Msg("Quitting game...");
                Application.Quit();
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

        // ======================
        // PRODUCT TAB (Product Giver)
        // ======================
        private void DrawProductTab()
        {
            GUILayout.Label("<size=16><b>Product Giver</b></size>", labelStyle);
            GUILayout.Space(10);

            var pm = NetworkSingleton<ProductManager>.Instance;
            if (pm == null)
            {
                GUILayout.Label("ProductManager not available (not in this scene?)", labelStyle);
                return;
            }

            // Quantity slider
            GUILayout.Label($"Quantity: {productQuantity}", labelStyle);
            productQuantity = (int)GUILayout.HorizontalSlider(productQuantity, 1, 500);

            GUILayout.Space(10);
            GUILayout.Label("<b>Filters</b>", labelStyle);

            filterDiscoveredOnly = GUILayout.Toggle(filterDiscoveredOnly, " Show only DISCOVERED products");
            filterListedOnly = GUILayout.Toggle(filterListedOnly, " Show only LISTED products");

            GUILayout.Space(10);

            GUILayout.Label("Search:", labelStyle);
            productSearch = GUILayout.TextField(productSearch, 50);

            GUILayout.Space(10);

            productScrollPos = GUILayout.BeginScrollView(productScrollPos, GUILayout.Height(300));

            List<ProductDefinition> products = pm.AllProducts;
            string searchLower = string.IsNullOrEmpty(productSearch) ? null : productSearch.ToLower();

            foreach (var prod in products)
            {
                if (prod == null) continue;

                // SEARCH filter
                if (!string.IsNullOrEmpty(searchLower))
                {
                    if (!prod.Name.ToLower().Contains(searchLower) &&
                        !prod.ID.ToLower().Contains(searchLower))
                        continue;
                }

                // DISCOVERED filter
                if (filterDiscoveredOnly && !ProductManager.DiscoveredProducts.Contains(prod))
                    continue;

                // LISTED filter
                if (filterListedOnly && !ProductManager.ListedProducts.Contains(prod))
                    continue;

                GUILayout.BeginHorizontal("box");

                // Icon
                Texture2D iconTex = prod.Icon != null ? prod.Icon.texture : Texture2D.blackTexture;
                GUILayout.Label(iconTex, GUILayout.Width(40), GUILayout.Height(40));

                // Text info
                GUILayout.BeginVertical();
                GUILayout.Label("<b>" + prod.Name + "</b>", labelStyle);
                GUILayout.Label("<size=11>" + prod.ID + "</size>");
                GUILayout.Label($"Type: {prod.DrugType}   BaseValue: {prod.MarketValue}", labelStyle);
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                // GIVE (inventory)
                if (GUILayout.Button("Give", GUILayout.Width(70)))
                {
                    try
                    {
                        var inv = PlayerSingleton<PlayerInventory>.Instance;
                        if (inv == null)
                        {
                            MelonLogger.Warning("[ProductGiver] PlayerInventory.Instance is null.");
                        }
                        else
                        {
                            var inst = prod.GetDefaultInstance(productQuantity);
                            inv.AddItemToInventory(inst);
                            MelonLogger.Msg($"[ProductGiver] Gave {productQuantity}x {prod.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning("[ProductGiver] Give failed: " + ex.Message);
                    }
                }

                // DROP
                if (GUILayout.Button("Drop", GUILayout.Width(60)))
                {
                    SpawnPickup(prod); // ProductDefinition : ItemDefinition
                }

                // EQUIP
                if (GUILayout.Button("Equip", GUILayout.Width(60)))
                {
                    EquipItemViaInventory(prod);
                }

                // DISCOVER (server-side), autoList = false
                if (GUILayout.Button("Discover", GUILayout.Width(80)))
                {
                    try
                    {
                        pm.SetProductDiscovered(null, prod.ID, false);
                        MelonLogger.Msg($"[Product] DISCOVERED: {prod.Name}");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning("[Product] Discover failed: " + ex.Message);
                    }
                }

                // HIDE (local only)
                if (GUILayout.Button("Hide", GUILayout.Width(80)))
                {
                    ProductManager.DiscoveredProducts.Remove(prod);
                    ProductManager.ListedProducts.Remove(prod);
                    pm.HasChanged = true;
                    MelonLogger.Msg($"[Product] HIDDEN (local): {prod.Name}");
                }

                if (GUILayout.Button("List", GUILayout.Width(80)))
                {
                    try
                    {
                        pm.SetProductListed(prod.ID, true);
                        MelonLogger.Msg($"[Product] LISTED: {prod.Name}");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning("[Product] List failed: " + ex.Message);
                    }
                }

                if (GUILayout.Button("Delist", GUILayout.Width(80)))
                {
                    try
                    {
                        pm.SetProductListed(prod.ID, false);
                        MelonLogger.Msg($"[Product] DELISTED: {prod.Name}");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning("[Product] Delist failed: " + ex.Message);
                    }
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(20);
            GUILayout.Label("<b>Global Product Tools</b>", labelStyle);

            if (GUILayout.Button("Discover + List ALL Products"))
            {
                try
                {
                    foreach (var p in pm.AllProducts)
                    {
                        if (p == null) continue;
                        pm.SetProductDiscovered(null, p.ID, true);
                    }

                    MelonLogger.Msg("[Product] All products DISCOVERED + LISTED (via SetProductDiscovered autoList=true).");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning("[Product] Discover/List ALL failed: " + ex.Message);
                }
            }

            if (GUILayout.Button("Hide ALL Products (local)"))
            {
                ProductManager.DiscoveredProducts.Clear();
                ProductManager.ListedProducts.Clear();
                pm.HasChanged = true;
                MelonLogger.Msg("[Product] All products HIDDEN locally (lists cleared).");
            }

            if (GUILayout.Button("List ALL Products"))
            {
                try
                {
                    foreach (var p in pm.AllProducts)
                    {
                        if (p == null) continue;
                        pm.SetProductListed(p.ID, true);
                    }

                    MelonLogger.Msg("[Product] All products LISTED.");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning("[Product] List ALL failed: " + ex.Message);
                }
            }

            if (GUILayout.Button("Delist ALL Products"))
            {
                try
                {
                    foreach (var p in pm.AllProducts)
                    {
                        if (p == null) continue;
                        pm.SetProductListed(p.ID, false);
                    }

                    MelonLogger.Msg("[Product] All products DELISTED.");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning("[Product] Delist ALL failed: " + ex.Message);
                }
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
            GUILayout.Label("Search:", labelStyle);
            itemSearch = GUILayout.TextField(itemSearch, 50);

            GUILayout.Space(5);

            // Quantity slider (for Give)
            GUILayout.Label($"Quantity (Give): {itemQuantity}", labelStyle);
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
            itemScrollPos = GUILayout.BeginScrollView(itemScrollPos, GUILayout.Height(300));

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

                GUILayout.Label(def.Icon != null ? def.Icon.texture : Texture2D.blackTexture,
                    GUILayout.Width(40), GUILayout.Height(40));

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

                // DROP
                if (GUILayout.Button("Drop", GUILayout.Width(60)))
                {
                    SpawnPickup(def);
                }

                // EQUIP
                if (GUILayout.Button("Equip", GUILayout.Width(60)))
                {
                    EquipItemViaInventory(def);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        // ======================
        // SKIN / STYLES
        // ======================
        private void ApplySkin()
        {
            switch (currentSkin)
            {
                case SkinType.Classic:
                    GUI.backgroundColor = Color.white;
                    GUI.contentColor = Color.black;
                    break;

                case SkinType.Dark:
                    GUI.backgroundColor = new Color(0.14f, 0.14f, 0.16f);
                    GUI.contentColor = new Color(0.85f, 0.85f, 0.9f);
                    break;

                case SkinType.Neon:
                    GUI.backgroundColor = new Color(0.02f, 0.15f, 0.08f);
                    GUI.contentColor = new Color(0.7f, 1f, 0.7f);
                    break;
            }
        }

        private void InitStylesIfNeeded()
        {
            if (labelStyle != null) return;

            darkBgTex = MakeTex(new Color(0.16f, 0.16f, 0.18f, 0.98f));
            darkerBgTex = MakeTex(new Color(0.09f, 0.09f, 0.11f, 0.98f));
            accentTex = MakeTex(new Color(0.22f, 0.5f, 0.96f, 1f));
            resizeHandleTex = MakeDiagonalHandleTex();

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                normal = { textColor = GUI.contentColor }
            };

            headerLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                richText = true,
                normal = { textColor = new Color(0.9f, 0.9f, 0.95f) }
            };

            tabHeaderStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 4, 4),
                normal = { background = darkBgTex }
            };

            sidebarButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 4, 4),
                margin = new RectOffset(0, 0, 2, 2),
                normal =
                {
                    textColor = new Color(0.85f, 0.85f, 0.9f),
                    background = darkBgTex
                },
                hover =
                {
                    textColor = Color.white,
                    background = accentTex
                },
                active =
                {
                    textColor = Color.white,
                    background = accentTex
                }
            };

            sidebarActiveButtonStyle = new GUIStyle(sidebarButtonStyle)
            {
                normal =
                {
                    textColor = Color.white,
                    background = accentTex
                }
            };

            espStyle = new GUIStyle
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }

        private Texture2D MakeTex(Color color)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeDiagonalHandleTex()
        {
            const int size = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

            Color clear = new Color(0, 0, 0, 0);
            Color line = new Color(0.7f, 0.7f, 0.75f, 0.8f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, clear);
                }
            }

            for (int i = 0; i < size; i += 3)
            {
                tex.SetPixel(size - 1 - i, i, line);
            }

            tex.Apply();
            return tex;
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

                // Add to inventory
                inv.AddItemToInventory(inst);

                // Find which hotbar slot it was placed in
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

                // If something is already equipped, unequip it
                if (inv.EquippedSlotIndex >= 0 &&
                    inv.EquippedSlotIndex < slots.Count &&
                    slots[inv.EquippedSlotIndex] != null)
                {
                    slots[inv.EquippedSlotIndex].Unequip();
                }

                // Equip new item
                slots[index].Equip();

                // Set EquippedSlotIndex via reflection
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
                espStyle = new GUIStyle
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
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
