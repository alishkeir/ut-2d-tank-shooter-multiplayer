# Tank Shooter Multiplayer - 2D Networked Tank Combat

---

## Project Overview

Tank Shooter Multiplayer is a top-down 2D multiplayer tank shooter built in **Unity 6** using **C#** and **Netcode for GameObjects**. Players control tanks in a shared arena, moving with tank-style controls, aiming their turrets at the cursor, and firing projectiles at other players. The game uses a coin-based economy where firing costs coins, coins are collected from pickups around the map, and defeated players drop bounty coins for others to collect.

The project demonstrates a complete multiplayer networking stack using Unity Services (Relay, Lobby, Authentication) with host/client and dedicated server support, server-authoritative gameplay with client-side prediction for responsive shooting, and networked UI systems including a live leaderboard, chat, and player nameplates.

---

# What I Built

### Player Systems

* Tank-style 2D movement (forward/back drive + body rotation)
* 360-degree turret aiming at cursor position
* Client-side camera following (Cinemachine per-player camera)
* Minimap icon with player color
* Network-synced player display name (shown above each tank)
* Health system with respawn at random spawn points

### Combat Systems

* Projectile-based weapon system with coin cost per shot
* Client-side prediction (dummy projectiles for instant feedback) + server-authoritative spawn
* Server-validated firing (prevents firing without enough coins)
* Self-damage prevention (projectiles ignore their owner)
* Damage numbers / health bar UI
* Healing zones (spend coins to restore health over time)

### Economy Systems

* Coin pickup system (respawning coin pickups scattered in the arena)
* Bounty coins dropped when a player dies (percentage of their carried coins)
* Coin collection synced across the network (NetworkVariable<int>)
* Coin cost for firing weapons and healing

### Networking Systems

* Unity Netcode for GameObjects (host/client model)
* Unity Relay for NAT traversal (join codes for friends)
* Unity Lobby for public matchmaking (browse and join lobbies)
* Unity Authentication (anonymous sign-in)
* Dedicated server support (Multiplay allocation + Unity 6 Session system)
* Server-authoritative connection approval (parses UserData payload)
* Owner-authoritative player movement (ClientNetworkTransform)
* NetworkVariables for health, coins, and player name
* NetworkList for leaderboard state

### UI Systems

* Name selector screen (first launch, sets display name)
* Main menu (Host / Join via join code / Quit)
* Public lobby browser (list available lobbies, join with one click)
* In-game HUD (leave game button)
* Live leaderboard (ranked by coin count, local player always visible)
* Server-authoritative chat (T to open, Enter to send, colorized by player)
* Health bar UI per player
* Player nameplates above tanks


---

# How It Works

## 1. Connection Flow

```text
Bootstrap Scene
       │
       ▼
ApplicationController (dedicated server? host? client?)
       │
       ├─ Dedicated Server ──► ServerSingleton → ServerGameManager
       │                       (Multiplay allocation + Session + Game scene)
       │
       └─ Host / Client ─────► HostSingleton + ClientSingleton
                               (Authentication → Relay + Lobby → Game scene)
                               └─ NameSelector → Menu (Host/Join) → Game
```

On boot, `ApplicationController` checks whether the process is running as a dedicated server (no graphics device) or as a client/host. Dedicated servers use `ServerSingleton` to await a Multiplay allocation, create a Unity 6 Session, open the transport, and load the Game scene. Hosts and clients use `HostSingleton` / `ClientSingleton` to authenticate anonymously with Unity Services, then proceed to the Name Selector and Main Menu.


## 2. Networking Architecture

```text
Client (Host)                                    Client (Joiner)
       │                                               │
       ▼                                               ▼
HostGameManager                                 ClientGameManager
       │                                               │
   Create Allocation                              Join Allocation (join code)
   Get Join Code                                  Configure Relay Transport
   Create Unity Lobby                             Attach UserData (name + auth ID)
   Start Netcode Host                             Start Netcode Client
   Load Game Scene                                Load Game Scene
       │                                               │
       └───────────── Netcode for GameObjects ─────────┘
                    (Relay transport, NAT traversal)
```

The host creates a Relay allocation, obtains a short join code, and creates a public Unity Lobby advertising that join code. Clients discover lobbies via the lobby browser, join a lobby to read the join code, then connect through Relay. Every connection carries a `UserData` payload (display name + Unity Auth ID) that the server reads during connection approval to identify the player.

## 3. Player Movement & Aiming

```text
Input (WASD / Mouse)
       │
       ▼
InputReader (ScriptableObject)
       │
   ┌───┴───┐
   │       │
   ▼       ▼
MoveEvent AimPosition
   │       │
   ▼       ▼
PlayerMovement ─────► PlayerAiming
(rotate body)        (rotate turret toward cursor)
   │
   ▼
Rigidbody2D velocity
   │
   ▼
ClientNetworkTransform (owner-authoritative replication)
```

`PlayerMovement` rotates the tank body based on horizontal input and sets Rigidbody2D velocity along the body's up vector based on vertical input. `PlayerAiming` rotates the turret to point at the mouse cursor in LateUpdate. Both run only on the owning client; transform changes replicate to others via `ClientNetworkTransform`, which makes the owner the authority (opposite of Netcode's default server-authoritative transform).

## 4. Combat & Projectile System

```text
Fire Input (owner client)
       │
       ▼
PrimaryFireServerRpc(position, direction)
       │
   ┌───┴───┐
   │       │
   ▼       ▼
Server validates   Client spawns dummy projectile
coin cost          (instant visual feedback)
   │       │
   ▼       ▼
Deduct coins       Muzzle flash effect
Spawn real projectile
ServerRpc → SpawnDummyProjectileClientRpc
   │
   ▼
All other clients spawn dummy projectiles
```

`ProjectileLauncher` uses a hybrid client-prediction / server-authoritative model. The owner presses fire, the server validates the player has enough coins, deducts the cost, instantiates the authoritative projectile, and tells other clients to spawn dummy copies. The owner also spawns a local dummy immediately for responsive feedback. Projectiles carry the firing client's ID so `DamageDealer` can prevent self-damage.


## 5. Health, Death & Respawn

```text
Projectile hits player
       │
       ▼
DamageDealer → Health.TakeDamage()
       │
       ▼
CurrentHealth reaches 0
       │
       ▼
Health.OnDie event (server)
       │
       ▼
RespawnHandler
   - Destroy old player object
   - Keep percentage of coins
   - Spawn new player at random SpawnPoint
   - Re-attach to same owner client
```

`Health` is a networked component with `CurrentHealth` as a `NetworkVariable<int>`. When health reaches 0, `OnDie` fires on the server. `RespawnHandler` listens for player deaths, destroys the old object, keeps a configured percentage of the player's coins, and spawns a fresh player at a random `SpawnPoint` re-attached to the original owner.

## 6. Economy (Coins)

```text
CoinSpawner (server)
       │
   Spawn maxCoins RespawningCoin pickups
       │
   ┌───┴───┐
   │       │
   ▼       ▼
Player collects   Coin disappears visually
coin              Server adds value to TotalCoins
       │
       ▼
Player dies
       │
       ▼
CoinCollector spawns BountyCoins
(percentage of carried coins, scatter around death position)
```

Coins are the core currency. `CoinSpawner` maintains a fixed number of `RespawningCoin` pickups in the arena, moving them to new random locations after collection. `CoinCollector` on each player picks up coins and adds to `TotalCoins` (a synced NetworkVariable). When a player dies, `CoinCollector` spawns `BountyCoin` pickups worth a percentage of the deceased player's coins. Coins are spent to fire weapons and to heal in healing zones.

## 7. Healing Zone

```text
Player enters HealingZone
       │
       ▼
Server tick loop (healTickRate interval)
       │
   ┌───┴───┐
   │       │
   ▼       ▼
Spend coins   Restore health
per tick      (healPerTick)
   │
   ▼
HealPower resource depletes
       │
   ▼
Cooldown period before recharge
```

`HealingZone` is a server-authoritative area where players can restore health by spending coins. It has a limited `healPower` resource that recharges after a cooldown. The UI shows a fill bar for remaining heal power.

## 8. Leaderboard

```text
Player spawns (server)
       │
   ▼
Leaderboard adds entry (NetworkList<LeaderboardEntityState>)
       │
   ▼
Coin changes update entry value
       │
   ▼
Clients re-sort by coin count (descending)
       │
   ▼
Top N rows shown, local player always visible
```

The `Leaderboard` maintains a server-authoritative `NetworkList` of player entries. When a player spawns, an entry is added; when their coins change, the entry value updates. Clients listen to list changes, spawn/remove/update UI rows, sort by coin count descending, and always ensure the local player's row is visible even if outside the top N.

## 9. Chat

```text
Client types message, presses ENTER
       │
       ▼
SendChatMessageRpc(playerName, message) → Server
       │
   ▼
Server validates (non-empty)
       │
   ▼
ReceiveChatMessageRpc (broadcast to all)
       │
   ▼
Each client displays "[PlayerName] message" (colorized)
```

`ChatManager` is a server-authoritative chat relay. Clients send messages via an RPC to the server, which validates and rebroadcasts to all clients including the host. `ChatUI` handles local input (T to open, ESC to close, ENTER to send, mouse wheel to scroll) and displays formatted messages with rich-text colored player names.


---

# Key Takeaways

* Unity Netcode for GameObjects with host/client architecture supports up to 20 players per host.
* Unity Relay + Lobby enables friends to join via join code and public matchmaking via lobby browser.
* Client-side prediction for shooting (dummy projectiles) keeps combat responsive despite network latency.
* Server-authoritative validation prevents cheating (coin cost checks, self-damage prevention).
* Owner-authoritative movement (`ClientNetworkTransform`) provides responsive tank controls.
* NetworkVariables and NetworkLists keep health, coins, player names, and leaderboard state synced.
* Unity Services (Authentication, Relay, Lobby, Matchmaker) handle identity, NAT traversal, and matchmaking.
* Dedicated server support is included via ServerSingleton / ServerGameManager for Multiplay deployment.
* Coin-based economy ties combat (firing cost) and progression (healing, bounty drops on death) together.
* Modular scene structure: Bootstrap → NameSelector → Menu → Game with DontDestroyOnLoad singletons.

