# Schedule I – Advanced Mod Menu
A fully-featured **MelonLoader mod menu** for *Schedule I*, created for development, debugging, modding, reverse engineering, and gameplay sandboxing.

This version introduces a redesigned modern UI system, improved NPC tools, polished item/product features, FPS counter, and keybind helper.

---

## 🚀 Features

### 🧠 NPC Manipulation
- **One Punch Kill** – Instantly kill NPCs using LMB  
- **Throwback NPC** – Ragdoll + launch NPCs with RMB (force & cooldown sliders)  
- **Pet Mode (Mind Control)** – Toggle an NPC as a follower with MMB  
  - Pet NPCs show as **cyan** in ESP  
- **Black Hole Ability** – Creates a gravitational sphere that kills nearby NPCs  
- **Brain Override** – Freeze, Panic (3× speed), or reset all AI  
- **Delete NPC (X)** – Removes any NPC you aim at  
- **Nuke All NPCs** – Instantly kills all NPCs in the scene  
- **Revive All NPCs** – Fully revives every NPC  

---

## 🔍 ESP System (Optimized)
Custom IMGUI-based ESP:
- NPC name  
- Distance  
- HP bar  
- Box outlines  
- Color-coded pets (cyan)  
- Refresh rate: **0.2s**  

---

## 🎁 Item Spawner
Loads all `ItemDefinition` assets:
- Search bar  
- Category filters  
- Give items (custom quantity)  
- **Drop real item pickups** into the world  
- Auto-equip via reflection bypass  

---

## 🧪 Product Spawner
Full control over `ProductDefinition`:
- Give / Drop / Equip  
- List / Delist  
- Discover / Hide  
- Search by ID/name  
- Bulk actions:  
  - Discover All  
  - Hide All  
  - List All  
  - Delist All  

---

## 🧍 Player Tools
- Walk speed slider  
- Jump multiplier  
- Modify health (Set/Heal/Zero)  
- F1 → Toggle menu visibility  

---

## 💰 Money Tools
- Instant cash modification using `MoneyManager.ChangeCashBalance()`  

---

## ⚙️ Utility
- **Save Game**  
- **Quit Game**  
- **FPS Counter** (top-right)  
- **Keybind Helper** window  
- Extra debugging helpers  

---

## 🎨 UI & Styling
Modern IMGUI menu:
- Classic / Dark / Neon skins  
- Draggable window  
- Corner-based window resizing  
- Clean, organized layout  

---

## 📦 Installation

1. Install **MelonLoader 0.6.x**  
2. Build this project in **Release**  
3. Copy the DLL into:  Schedule I/Mods/
4. Launch the game  
5. Press **F1** to open/close the menu  

---

## 🧬 Technical Notes

---

## 🔓 Bypassed Systems

This mod bypasses several internal restrictions from Schedule I to allow full sandbox access:

### ✔ Inventory Restrictions
- Bypassed protected `EquippedSlotIndex` setter (reflection)
- Equip-anywhere logic unlocked
- Restricted items can be forced into hotbar

### ✔ Item & Product Restrictions
- Product discovery limits fully bypassed
- Product listing delist restrictions removed
- Items/products can be dropped or equipped regardless of legality
- Forced spawn of ItemPickup clones using template duplication

### ✔ Player Restrictions
- Local player can revive without server permissions
- Full heal / set health bypass
- Movement stats (walk/jump) unrestricted

### ✔ NPC Restrictions
- AI MoveSpeedMultiplier override
- Forced ragdoll activation on NPCs
- Mind-control mode bypasses AI ownership
- Delete-any-NPC ability, no server validation

### ✔ Money Restrictions
- Unlimited money editing via direct `MoneyManager.ChangeCashBalance` call

### ✔ UI / Engine Restrictions
- IMGUI rendering on top of Schedule I locked UI layers
- Hotkey-based menu bypassing internal UI input locks

This list updates as new bypass techniques are added.

### Internals Used
- PlayerHealth internal APIs (SetHealth, Revive, Die)  
- Reflection to bypass protected inventory setters  
- ESP rendering with GUI primitives  
- Full item/product resource scanning  
- Raycast-based NPC controls  
- Safe fallbacks for missing components  

### Compatible With
- MelonLoader  
- Schedule I (IL2CPP)  
- Unity 2021.x  
- FishNet networking  

---

## ⚠️ Disclaimer
This project is for **educational, debugging, and research purposes only**.  
Do not use it online or in any disruptive scenario. You are responsible for your actions.

---

## ⭐ Credits
Mod Menu Development: **Kristóf (Kristof6769)**
