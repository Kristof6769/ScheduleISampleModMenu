# Schedule I – Advanced Mod Menu
A fully-featured **MelonLoader mod menu** for *Schedule I*, designed for development, debugging, modding, reverse engineering, and sandbox gameplay.

This release includes a completely redesigned modern UI, advanced NPC tools, improved spawners, FPS counter, keybind helper window, and multiple built-in bypass techniques for internal systems.

## 🚀 Features

### 🧠 NPC Manipulation
- **One Punch Kill** – Instantly kill NPCs using LMB  
- **Throwback NPC** – Ragdoll + launch NPCs with RMB  
  - Adjustable **Force (1–300)**  
  - Adjustable **Cooldown (0–1s)**  
- **Pet Mode (Mind Control)** – MMB toggles followers  
  - Pet NPCs are highlighted **cyan** in ESP  
- **Black Hole Ability (B)**  
  - Creates a dynamic gravity field that pulls NPCs inward  
  - Auto-kills at core radius  
- **Brain Override**  
  - Freeze AI  
  - Panic Mode (3× speed)  
  - Reset AI  
- **Delete NPC (X)** – Remove any NPC from scene  
- **Nuke All NPCs** – Instantly kill all NPCs  
- **Revive All NPCs** – Reset every NPC to full health  

---

## 🔍 NPC ESP (Optimized)
Custom IMGUI-based ESP renderer:
- Name  
- Distance  
- HP bar  
- 2D box  
- Pet NPCs = **cyan**  
- Normal NPCs = **green**  
- Refresh rate: **0.20s**

Extremely lightweight, uses cached NPC lists and avoids per-frame object scans.

---

## 🎁 Item Spawner
Loads every `ItemDefinition` in the game:
- Search bar  
- Category filtering  
- Give (quantity slider)  
- **Drop real ItemPickup objects** to world  
- **Auto-equip** via reflection bypass

---

## 🧪 Product Spawner
Powerful control over `ProductDefinition` objects:
- Search by name / ID  
- Give  
- Drop  
- Equip  
- Discover  
- Hide  
- List / Delist  
- Bulk administration:
  - Discover ALL  
  - Hide ALL  
  - List ALL  
  - Delist ALL  

---

## 🧍 Player Tools
- Walk Speed slider  
- Jump Multiplier slider  
- Full Health Control: Set / Heal / Zero  
- Modern UI with theme support  
- **F1 → Toggle Menu**  

---

## 💰 Money Tools
Instant cash editor using direct `MoneyManager.ChangeCashBalance()` API calls.

---

## ⚙️ Utility Tools
- **Save Game** (safe call to SaveManager)  
- **Quit Game** (clean exit)  
- **FPS Counter** (top right corner)  
- **Keybind Helper** window  
- Debug logs for all systems  

---

## 🎨 UI & Styling
Modern IMGUI Menu:
- **Draggable window**  
- **Resizable window (corner-based)**  
- Smooth scaling / snapping  
- Classic / Dark / Neon themes  
- Organized sidebar with tabs  

---

# 🔓 Bypassed Systems
This mod bypasses multiple internal restrictions to unlock full sandbox capabilities:

### ✔ Inventory Restrictions
- Bypassed protected `EquippedSlotIndex` setter via reflection  
- Forced-equippable items regardless of legality  
- Inventory capacity checks override for spawner tools  

### ✔ Item & Product Restrictions
- Product “discovered” & “listed” flags forced ON/OFF  
- Delist restrictions removed  
- Items can be dropped even if not normally droppable  
- ItemPickup cloning using template duplication  

### ✔ Player Restrictions
- Full health control with no internal limits  
- Revive without server permission  
- Unlimited movement modifiers  

### ✔ NPC Restrictions
- Override of AI MoveSpeedMultiplier  
- Forced ragdoll activation  
- Delete NPC without validation  
- Black Hole damage ignoring resistances  

### ✔ Money Restrictions
- Unlimited money editing without server verification  

### ✔ UI / Engine Restrictions
- IMGUI overlays used on top of locked UI layers  
- Internal error suppression for some camera/UI handlers  

---

## 🏗️ Architecture Overview
The project is organized into clean modules:

### **Core**
Menu rendering, skins, dragging, resizing, input handling.

### **NPC Module**
OnePunch, Throwback, Pet AI, Brain Override, Black Hole, Delete/Nuke logic.

### **ESP Module**
NPC caching, 2D rendering, performance optimization.

### **Item Module**
Resource scanning, ItemPickup template cloning, inventory reflection.

### **Product Module**
Full ProductManager API wrapper with bypass toggles.

### **Player Module**
Health system manipulation, movement overrides, stat updates.

### **Utility Module**
Save, quit, debug, FPS counter, keybind helper, UI helpers.

This makes the mod highly maintainable and easily expandable.

---

## ⚡ Performance Notes
- ESP updates at **5Hz** instead of every frame  
- NPC scanning heavily optimized  
- Reflection used only when absolutely needed  
- All expensive operations cached  
- Throwback & Black Hole fallback to simple physics if ragdoll components missing  

---

## 🔐 Security Considerations
- No networking  
- No remote code execution  
- No data upload  
- No external dependencies  
- All bypass methods are internal-only and offline-safe  

---

## ❓ FAQ

### **Q: Does this work online?**  
❌ No. This is for offline sandbox/debugging only.

### **Q: Does it modify game files?**  
No, it injects a runtime DLL via MelonLoader.

### **Q: Can this break saves?**  
Only if abusing destructive tools. Normal use is safe.

### **Q: Can I contribute?**  
Absolutely — pull requests are welcome.

---

## 📦 Installation
1. Install **MelonLoader 0.6.x**  
2. Build the mod in **Release**  
3. Place the DLL into:  
   `Schedule I/Mods/`  
4. Launch the game  
5. Press **F1** to toggle the menu  

---

## 📄 Changelog

### **v1.3 – Modern UI Edition**
- Added draggable & resizable IMGUI window  
- Added FPS Counter  
- Added Keybind Helper  
- Improved NPC ESP  
- Added Delete NPC  
- Added product/admin bulk actions  
- Full PlayerHealth integration  
- UI skin system updated  

### **v1.2 – ESP & NPC Toolkit**
- Added ESP  
- Added Pet Mode  
- Added Brain Override  
- Added Black Hole  
- Added Throwback NPC  

---

## ⭐ Credits
Mod Menu Development: **Kristóf (Kristof6769)**  
Reverse Engineering, Research, UX & Systems Design

---

