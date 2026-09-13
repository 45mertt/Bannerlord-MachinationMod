# Bannerlord: Machination Mod

An extensive, C#-based simulation and gameplay mechanics overhaul for **Mount & Blade II: Bannerlord**. Built with over **50,000+ lines of optimized C# code**, this mod introduces deep socio-economic, political, demographic, and innovative 3D traversal systems to Calradia.

---

## 🌍 Dynamic Demographics & Population System
*   **True Population Mechanic:** Settlement populations are dynamically calculated using realistic Birth and Death rates, driven by local stability, food security, and prosperity.
*   **Multi-Cultural Settlements:** Towns are no longer mono-cultural. They possess a fluid demographic mix (Empire, Battania, Vlandia, etc.) reflecting their historical ownership and geographical location.
*   **Global Culture Shift:** The dominant population culture dictates global settlement identity. If a foreign culture becomes the absolute majority, the settlement's culture shifts permanently.
*   **Migration & Refugees:** Features a fully simulated Rural-to-Urban migration engine. During sieges or conquests, populations actively flee as refugees to culturally aligned, safe havens.

## 👑 The Grand Council, Governance & Courts
*   **The Grand Council:** Faction Rulers can summon a dynamic Grand Council in their keep (45k Gold / 200 Influence). Players can approach lords via 3D interaction (`F` key) to hear grievances and resolve kingdom-wide internal crises.
*   **Clan Stress & Civil Wars:** Clans accumulate a "Stress Score" based on realm stability. High stress triggers rebellion risks, forcing rulers to pay millions in tributes or face a devastating Civil War.
*   **Corruption & Town Council:** Notables and Governors can embezzle funds. Players can "Walk Incognito" in towns to gather rumors, expose corruption, and put the culprits on trial in the newly designed Town Council hub scene.
*   **Garrison Mutinies (Strikes):** Unpaid wages or severe neglect trigger garrison strikes. Rulers must choose to suppress the mutiny via an intense urban Street Battle or pay them off to restore order.

## ⚔️ Military Realism & Village Policies
*   **Conscription (Drafting):** Rulers can declare a wartime draft to instantly pull recruits directly from a settlement's civilian population, sacrificing town Loyalty for raw military numbers.
*   **Village Militia Training:** Players can train local rural militia, progressively converting them into professional standing troops of the local culture.
*   **Shadow Garrison:** Advanced troops can be hidden via the "Manage Guards" UI. They remain invisible in the standard UI count but physically spawn to defend the village during raids.
*   **3D Troop Inspection:** Conduct a "Show of Force" in village scenes using dedicated hotkeys (`H`, `K`, `T`, `M`) to order soldiers to Salute, Cheer, or Taunt in front of reacting villagers.

## 💀 Epidemics, Starvation & Realistic Quarantine
*   **Logic-Driven Plagues:** Outbreak probabilities are mathematically calculated based on population density and garrison overcrowding metrics. Severe starvation occurs instantly when food stocks hit zero.
*   **Physical Quarantines:** During outbreaks, settlement guards will physically intercept the player at the gates. Internal access to the Tavern, City Center, and Lord's Hall is completely blocked.

## 📜 Advanced Diplomacy, Espionage & UI
*   **Incite Rebellion & Alliances:** Financing a rival realm's rebellion (restricted to Clan Leaders) automatically triggers a dynamic Alliance and Trade Agreement with the newly formed rebel faction while declaring war on the target.
*   **War Cabinet & Council:** Convene a War Council to coordinate strategic targets, or use a deep persuasion system to convince reluctant lords to support a war declaration, gaining cohesion and influence.
*   **War Room Dashboard:** Rulers can press `CTRL + U` to open a custom UI overlay displaying real-time lord locations, status, and precise troop counts across the realm.
*   **Field Command Scenes:** Approach any allied lord on the campaign map and press `CTRL + SHIFT + K` to open a 3D face-to-face command scene to dictate tactical map orders (Patrol, Siege, Join Army).

## 🧭 Innovative Traversal & Map Mechanics
*   **Synchronized 3D Map Traversal:** Pressing `CTRL + SHIFT + J` on the 2D campaign map seamlessly transitions the player into a fully explorable 3D scene synchronized with Calradia's geometry.
*   **3D Map Encounters & Treasures:** Interact with fully functional Looters (requiring a Denar payoff to escape or initiating a 2D battlefield encounter). Hidden, breakable treasure caches guarded by hostile troops are scattered across the 3D map.
*   **Battlefield Salvage:** Passing by recent historical battles alerts the player to active battlefield loot. Players enter the scene to fight dynamic guards and smash caches to secure high-tier loot scaled to Clan Tier.

## 📈 Workshop & Trade Empire Overhaul
*   **Levels & Progression:** Workshops can now be upgraded up to Level 5, directly feeding into host city prosperity at higher tiers.
*   **Production Strategies:**
    *   *Standard/Guild:* Safe, predictable, baseline income.
    *   *War Production:* Skyrockets profits during active wars; plummets during peacetime.
    *   *Black Market & Smuggling:* Extreme profit margins balanced by high risks of city raids and asset confiscation.
    *   *Sweatshop:* Maximizes financial yields at the cost of high worker riot risks.
    *   *Hoarding:* Strategic stockpiling of local goods for late-game market manipulation.

## 🎭 Dynamic Historical Timeline: Neretzes's Folly (1077 Start)
*   Starts the campaign in the year **1077** instead of 1084, immersing the player in the historical events of *Neretzes's Folly*.
*   Features custom dream sequence scenes and reactive town rumors regarding the unified Empire.
*   **The Great Collapse (1082):** Upon reaching the year 1082, the unified Empire dynamically fractures into three separate competing factions, transitioning seamlessly into the base-game sandbox timeline and unlocking the player's brother companion.

---

## 🛠️ Requirements & Technical Notes
*   Developed in C# targeting TaleWorlds' official API.
*   Fully compatible with the Mod Configuration Menu (MCM) for granular AI toggles.
*   Designed exclusively for advanced campaign depth, political strategy, and realistic resource management.
