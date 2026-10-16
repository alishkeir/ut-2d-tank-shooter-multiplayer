# Tank Shooter Multiplayer (2D Networked Tank Shooter)

## 1. Project Overview

- **Purpose:** Demonstrate a complete multiplayer networking stack for a 2D top-down tank shooter, including host/client and dedicated server support, server-authoritative gameplay with client-side prediction, and networked UI systems (leaderboard, chat, nameplates).
- **Portfolio Goal:** Prove capability in building networked multiplayer games with Unity Netcode for GameObjects, Unity Services (Relay, Lobby, Authentication), and client/server architecture.
- **Target Role:** Multiplayer Programmer / Network Programmer / Gameplay Programmer.
- **Project Scope:** 2D top-down arena, tank player characters, coin-based economy, health/respawn, projectile combat, chat, leaderboard, and matchmaking via Unity Relay and Lobby. Supports up to 20 players per host, plus dedicated server deployment.

---

## 2. High-Level Game Concept

- **Example Session:** The player enters their display name, reaches the main menu, and either hosts a lobby (generating a join code) or joins an existing lobby via join code. Once in the game, they control a tank in a shared arena, drive around, aim their turret at the mouse cursor, and fire projectiles at other players. Firing costs coins, which are collected from pickups around the map. When a player is defeated, they drop bounty coins and respawn at a random spawn point. The leaderboard tracks coin counts in real time, and players can chat with each other.
- **Player Experience:** Fast and responsive tank combat in a shared arena. Tank-style movement (drive forward/back, rotate body) with independent turret aiming. Networked multiplayer with client-side prediction for shooting. Economy-driven combat where every shot costs coins, encouraging careful aim and coin collection.
- **Core Gameplay Loop:** Name Selector -> Main Menu (Host / Join) -> Arena Combat -> Collect Coins -> Fire at Opponents -> Die / Respawn -> Repeat.


---

## 3. Design Goals

- **Primary Goals:**
  - Networked multiplayer tank combat using Unity Netcode for GameObjects:
    - Host/client model (up to 20 players per host)
    - Dedicated server support (Multiplay allocation + Unity 6 Session)
  - Unity Services integration:
    - Unity Authentication (anonymous sign-in)
    - Unity Relay (NAT traversal via join codes)
    - Unity Lobby (public matchmaking browser)
  - Server-authoritative gameplay with client-side prediction:
    - Server validates coin cost and damage
    - Client predicts projectiles for instant feedback
  - Owner-authoritative player movement for responsive controls

- **Secondary Goals:**
  - Networked UI systems:
    - Live leaderboard ranked by coin count
    - Server-authoritative chat
    - Player nameplates above tanks
    - Health bar UI
  - Coin-based economy:
    - Coin pickups scattered in the arena
    - Bounty coins dropped on death
    - Coin cost for firing and healing
  - Between-match flow:
    - Name selection screen
    - Main menu with host/join/quit
    - Lobby browser

- **Technical Goals:**
  - Stable networked gameplay with client-side prediction for shooting
  - Server-authoritative validation to prevent cheating (coin cost, self-damage)
  - Reusable networking architecture (singletons, GameManagers, connection approval)
  - Data-driven coin values, fire rates, health values via inspector fields
  - Support for dedicated server deployment (headless mode detection)


---

## 4. Gameplay Systems

### Player Systems:

- Tank-style 2D movement (forward/back drive + body rotation)
- 360-degree cursor-following turret rotation
- Client-side camera following (Cinemachine per-player camera)
- Minimap icon with player-specific color
- Network-synced display name (FixedString32Bytes NetworkVariable)
- Health system (NetworkVariable<int>, clamped to [0, MaxHealth])
- Coin collection (NetworkVariable<int>, synced to all clients)
- Respawn at random SpawnPoint after death (keeps percentage of coins)
- Player spawn/despawn events (static events on TankPlayer)

### Combat Systems:

- Projectile-based weapon system
- Coin cost per shot (server-validated, cannot fire without enough coins)
- Client-side prediction (dummy projectiles for instant visual feedback)
- Server-authoritative projectile spawn
- Self-damage prevention (projectile owner tracked via DamageDealer)
- Muzzle flash visual effect
- Projectile speed, fire rate, and damage configurable via inspector
- Health display UI (health bar fill amount)

### Economy Systems:

- Coin pickup system (RespawningCoin pickups scattered in arena)
- CoinSpawner maintains fixed number of active coins, respawns at random locations
- Bounty coins dropped on death (percentage of carried coins, scatter around position)
- CoinCollector handles pickup and spending (SpendCoins for firing/healing)
- TotalCoins is a server-authoritative NetworkVariable<int>

### Healing Systems:

- HealingZone: server-authoritative area that restores health over time
- Cost: coins per tick + heal power resource
- Heal power depletes and recharges after cooldown
- UI fill bar shows remaining heal power

### Networking Systems:

- Unity Netcode for GameObjects (NetworkBehaviour, NetworkVariable, NetworkList, ServerRpc, ClientRpc)
- Connection approval with UserData payload (display name + auth ID)
- ClientNetworkTransform (owner-authoritative transform replication)
- NetworkServer for connection tracking and disconnect handling
- HostGameManager: Relay allocation, join code, Unity Lobby creation, Netcode host start
- ClientGameManager: Authentication, Relay join, client start, UserData attachment
- ServerGameManager: Multiplay allocation, Unity 6 Session, dedicated server start
- ApplicationController: bootstrapper that decides dedicated server vs host/client

### UI Systems:

- NameSelector: first screen, sets and saves display name (PlayerPrefs)
- MainMenu: Host (creates lobby + starts host), Join (enter join code), Quit
- LobbiesList: queries Unity Lobby for public lobbies with available slots
- LobbyItem: renders lobby name + player count, join button
- GameHUD: leave game button (host shutdown or client disconnect)
- Leaderboard: ranks players by coin count, top N + local player always visible
- ChatUI: T to open, ENTER to send, mouse wheel to scroll, colorized messages
- ChatManager: server-authoritative chat relay via RPCs
- HealthDisplay: UI health bar bound to Health component
- PlayerNameDisplay: world-space text showing player name above tank


---

## 5. Technical Architecture

### Scene Flow:

```
Bootstrap (ApplicationController)
    │
    ▼
NameSelector (display name input)  ──►  Menu (Host / Join / Quit)
    │                                               │
    ▼                                               ▼
Game (arena combat) ◄───────────────────────────────┘
```

- **Bootstrap:** ApplicationController detects dedicated server mode (no graphics device) and spawns the appropriate singleton (ServerSingleton / HostSingleton + ClientSingleton).
- **NameSelector:** Player enters display name (1-20 chars), saved to PlayerPrefs, advances to Menu.
- **Menu:** Host creates a Relay allocation + Unity Lobby, gets a join code, starts the host and loads Game. Client enters a join code, joins Relay, and loads Game.
- **Game:** Networked arena combat scene with all gameplay systems.

### Networking Architecture:

```
Client (Host)                              Client (Joiner)
       │                                       │
   HostGameManager                       ClientGameManager
   - Relay CreateAllocation              - Relay JoinAllocation (join code)
   - Get Join Code                       - Configure Unity Transport
   - Create Unity Lobby (public)         - Attach UserData payload
   - Start Netcode Host                  - Start Netcode Client
   - Load Game Scene                     - Load Game Scene
       │                                       │
       └──────── Netcode for GameObjects ──────┘
             (Relay transport, NAT traversal)
```

- **Connection Approval:** NetworkServer parses the UserData JSON payload from each connecting client, stores ClientId ↔ AuthId ↔ UserData mappings, approves all connections, and assigns random spawn positions.
- **Player Spawning:** OnNetworkSpawn, the server sets the player's name from UserData and fires OnPlayerSpawned. The owner client configures its Cinemachine camera and minimap icon color.
- **Object Replication:** Health, coins, and player name use NetworkVariables. Leaderboard uses NetworkList<LeaderboardEntityState>. Chat uses ServerRpc + ClientRpc. Movement uses owner-authoritative ClientNetworkTransform.

### Input System:

- Unity Input System (Input Action Asset: Controls.inputactions)
- InputReader ScriptableObject decouples gameplay from input backend:
  - Move (Vector2) → MoveEvent
  - Primary Fire (Button) → PrimaryFireEvent(bool)
  - Aim (Vector2) → AimPosition property
- PlayerMovement, PlayerAiming, and ProjectileLauncher subscribe to InputReader events


---

## 6. MVP Version

- Playable 2D top-down tank arena
- Responsive tank-style movement (drive + rotate)
- 360-degree turret aiming at cursor
- Projectile combat with coin cost per shot
- Client-side prediction (dummy projectiles) + server-authoritative spawn
- Health system with death and respawn at random spawn points
- Coin pickup system (respawning coins in arena)
- Bounty coins dropped on death (percentage of carried coins)
- Healing zones (spend coins to restore health)
- Unity Netcode host/client (up to 20 players)
- Unity Relay join codes for friend connections
- Unity Lobby public matchmaking browser
- Unity Authentication (anonymous sign-in)
- Server-authoritative connection approval with UserData
- Live leaderboard ranked by coin count (local player always shown)
- Server-authoritative chat (colorized by player)
- Player nameplates above tanks
- Health bar UI per player
- Minimap icons with player colors
- Name selector screen (first launch)
- Main menu (Host / Join / Quit)
- Dedicated server support (headless detection, Multiplay allocation)

---

## 7. Learning Objectives / Technical Concepts Demonstrated

- **Networked Multiplayer Architecture**
  - Host/client model with Unity Netcode for GameObjects
  - Server-authoritative validation (coin cost, damage, self-damage prevention)
  - Client-side prediction for responsive shooting (dummy projectiles)
  - Owner-authoritative transform replication (ClientNetworkTransform)
  - NetworkVariables for synced state (health, coins, player name)
  - NetworkList for leaderboard state synchronization

- **Unity Services Integration**
  - Unity Authentication (anonymous sign-in, session management)
  - Unity Relay (NAT traversal, join codes for friend connections)
  - Unity Lobby (public matchmaking, lobby browser, join code sharing)
  - Unity 6 Session system (dedicated server session management)

- **Dedicated Server Support**
  - Headless mode detection (GraphicsDeviceType.Null)
  - ServerSingleton / ServerGameManager for dedicated server lifecycle
  - Multiplay allocation + Session creation
  - Server-authoritative connection approval and spawn assignment

- **Client-Server RPC Patterns**
  - ServerRpc for client requests (fire weapon, send chat message)
  - ClientRpc for server broadcasts (dummy projectile spawn, chat messages)
  - RPC validation on the server (reject empty messages, insufficient coins)

- **Networked UI Systems**
  - NetworkVariable-driven UI (health bars, coin counts)
  - NetworkList-driven UI (leaderboard rows)
  - Server-authoritative chat relay with rich-text formatting
  - World-space nameplates bound to networked player names

- **Economy-Driven Gameplay**
  - Coin as core currency (collection, spending, bounty drops on death)
  - Coin cost gates combat actions (firing, healing)
  - Server-authoritative economy to prevent exploitation

- **Input System Decoupling**
  - ScriptableObject InputReader pattern
  - Event-driven input (MoveEvent, PrimaryFireEvent, AimPosition)
  - Gameplay systems independent of specific input bindings

- **Scene & Singleton Architecture**
  - DontDestroyOnLoad singletons surviving scene loads
  - ApplicationController bootstrapper for dedicated server vs client/host
  - Scene flow: Bootstrap → NameSelector → Menu → Game

