# Schedule I – Sample Mod Menu
A fully-featured **MelonLoader mod menu** for *Schedule I*, designed for development, debugging, 
reverse engineering, and gameplay experimentation.  
This project demonstrates advanced interaction with internal game systems including:
NPC logic, item frameworks, player controllers, product definitions, and runtime UI manipulation.

---

## 🚀 Features

### 🧠 NPC Manipulation
- **One Punch Kill** – Instantly kill NPCs with LMB
- **Throwback NPC** – Ragdoll & launch NPCs using RMB (force slider included)
- **Pet Mode (Mind Control)** – Middle-click an NPC to turn it into a follower  
  → Click again to remove  
  → Pet NPCs appear *cyan* in ESP
- **Black Hole Ability** – Create a gravity well that pulls NPCs in and destroys them
- **Brain Override** – Freeze, Panic, or Normalize enemy AI speed multipliers
- **Delete NPC** – Remove NPC with X key  
- **Nuke All NPCs** – Instantly kill every NPC in the scene  
- **Revive All NPCs** – Reset all NPCs back to life

---

## 🔍 ESP System
A clean, optimized ESP renderer over Unity GUI:
- NPC Name
- Distance in meters
- Health bar (with HP %)
- Bounding box
- Distinguishes **pets (cyan)** and **normal NPCs (green)**  
- Scans NPCs dynamically (refresh every 0.2 sec)

---

## 🎁 Item Spawner
Supports all `ItemDefinition` assets loaded in Resources:
- Search by name or ID
- Category filtering
- Give item (N quantity)
- Drop item into the world (spawn real ItemPickup)
- Auto-equip item (via inventory reflection workaround)

---

## 🧪 Product Spawner
Full control over `ProductDefinition` objects:
- Give / Drop / Equip products
- Search by ID or name
- Quantity slider
- **Discover product**
- **Hide product**
- **List / Delist product**
- Bulk tools:
  - Discover ALL
  - Hide ALL
  - List ALL
  - Delist ALL

---

## 🧍 Player Modifiers
- Walk speed slider  
- Jump multiplier slider  
- F1 hotkey to toggle UI  
- Includes Classic, Dark Mode, and Neon UI skins  

---

## 💰 Money Tools
- Modify cash instantly via `MoneyManager.ChangeCashBalance()`

---

## 🛠 Utility
- Manual **Save Game** (calls Schedule I SaveManager)
- Debug logs for every major action

---

## 📦 Installation

1. Install **MelonLoader 0.6.x**
2. Build the project in **Release**
3. Copy output DLL: into: Schedule I/GameFolder/Mods/
4. Launch the game  
5. Press **F1** to open the menu

---

## 🧬 Technical Notes

### Uses:
- Runtime reflection to bypass protected inventory setters  
- IMGUI-based rendering for ESP & menu  
- Raycast-based NPC picking  
- Dynamic resource scanning for ItemDefinition / ProductDefinition  
- Custom spawning of ItemPickup clones with overridden metadata  

### Compatible with:
- MelonLoader
- Schedule I IL2CPP build
- FishNet runtime
- Unity 2021.x internal classes

---

## 📜 Disclaimer

This software is provided **strictly for educational, debugging, and research purposes**.  
Do **not** use in online environments, competitive scenarios, or to harm the intended gameplay experience.  
You are responsible for ensuring compliance with all applicable terms and laws.

---

## ⭐ Credits
Mod Menu development, reverse engineering, and research by **Kristóf (Kristof6769)**
