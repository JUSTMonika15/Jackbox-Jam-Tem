# Host-Authoritative Gameplay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the listen host the only writer of match and score-affecting state, automatically start one shared countdown, and replicate the same board state to every client.

**Architecture:** `GameManager` becomes a PurrNet `NetworkIdentity` and remains the single gameplay coordinator. Server RPCs accept client intent and identify the caller from `RPCInfo.sender`; observer/target RPCs replicate global, player, property, coin, effect, and result state. Offline Play Mode treats the local process as authority.

**Tech Stack:** Unity 6.6, C#, PurrNet 1.23 beta, existing owner-authoritative `NetworkTransform`, IMGUI HUD, JamValidation.

**Spec:** `Docs/superpowers/specs/2026-09-15-host-authority-design.md`

## Global Constraints

- Do not modify PurrNet package source, lobby transport, authentication, player spawning, or existing `NetworkTransform` ownership.
- The server is the only writer of match and score-affecting state in a live session.
- Preserve offline/single-editor Play Mode as an authority fallback.
- Use `RPCInfo.sender` to resolve a requesting player; never trust a client-supplied player identity.
- Host migration, prediction, rewind, and full server-authoritative combat remain out of scope.
- Preserve existing dirty-worktree changes.

---

### Task 1: Authority and Revision Rules

**Files:**
- Create: `Assets/Script/NetworkAuthorityRules.cs`
- Modify: `JamValidation/JamValidation.csproj`
- Modify: `JamValidation/Program.cs`

**Interfaces:**
- Produces: `AuthorityGate.IsAuthoritative(bool isSpawned, bool isServer) -> bool`
- Produces: `SnapshotRevision.TryAccept(int incomingRevision) -> bool`
- Produces: `RequestValidation.CanBuy(...) -> bool`

- [ ] **Step 1: Add failing authority tests**

```csharp
Assert(AuthorityGate.IsAuthoritative(false, false), "offline must be authoritative");
Assert(AuthorityGate.IsAuthoritative(true, true), "server must be authoritative");
Assert(!AuthorityGate.IsAuthoritative(true, false), "client must not be authoritative");
var revisions = new SnapshotRevision();
Assert(revisions.TryAccept(1) && !revisions.TryAccept(1) && !revisions.TryAccept(0) && revisions.TryAccept(2),
    "snapshot revisions must be strictly increasing");
Assert(RequestValidation.CanBuy(true,true,true,true,true,50,20), "valid buy rejected");
Assert(!RequestValidation.CanBuy(true,true,true,true,true,10,20), "insufficient buy accepted");
```

- [ ] **Step 2: Include the missing source and run the harness to verify failure**

Run: `dotnet run --project JamValidation/JamValidation.csproj --no-restore`

Expected: compilation failure because the three rule types do not exist.

- [ ] **Step 3: Implement the minimal plain-C# rules**

```csharp
public static class AuthorityGate
{
    public static bool IsAuthoritative(bool spawned, bool server) => !spawned || server;
}
public sealed class SnapshotRevision
{
    int accepted = -1;
    public bool TryAccept(int value) { if (value <= accepted) return false; accepted = value; return true; }
    public void Reset() => accepted = -1;
}
public static class RequestValidation
{
    public static bool CanBuy(bool playing, bool canAct, bool inZone, bool unowned,
        bool holdReady, int money, int price) => playing && canAct && inZone && unowned
        && holdReady && price >= 0 && money >= price;
}
```

- [ ] **Step 4: Run the harness**

Run: `dotnet run --project JamValidation/JamValidation.csproj --no-restore`

Expected: all existing 40 tests plus the new authority tests pass.

### Task 2: Networked Match Lifecycle and Automatic Countdown

**Files:**
- Modify: `Assets/Script/GameManager.cs`
- Modify: `Assets/Script/GameRules.cs`
- Modify: `Assets/Script/GameHud.cs`
- Modify: `Assets/Editor/JamEventSmoke.cs`

**Interfaces:**
- Consumes: `AuthorityGate.IsAuthoritative`
- Produces: `GameManager.IsAuthoritative`, `GameManager.IsLiveNetworkSession`
- Produces RPCs: `RequestRestart(RPCInfo info)`, `SyncMatch(int revision, int state, float remaining, float countdown)`

- [ ] **Step 1: Add smoke assertions for offline authority and synchronized lifecycle.**
- [ ] **Step 2: Run the Unity smoke and confirm the new assertions fail before implementation.**
- [ ] **Step 3: Convert `GameManager` to `PurrNet.NetworkIdentity`.**

```csharp
public class GameManager : PurrNet.NetworkIdentity
{
    public bool IsAuthoritative => AuthorityGate.IsAuthoritative(isSpawned, isServer);
    public bool IsLiveNetworkSession => isSpawned;
    int revision;
    float stablePlayersSince = -1f;
    float nextClockBroadcast;
}
```

Only the authority ticks `MatchClock`, resets the match, calculates results, and auto-starts after a non-zero player count stays stable for 1.5 seconds.

- [ ] **Step 4: Add global synchronization RPCs.**

```csharp
[PurrNet.ObserversRpc(runLocally: true, bufferLast: true)]
void SyncMatch(int incomingRevision, int state, float remaining, float countdown)
{
    if (IsAuthoritative || !snapshotRevision.TryAccept(incomingRevision)) return;
    clock.ApplyNetwork((GameState)state, remaining, countdown);
}

[PurrNet.ServerRpc(requireOwnership: false)]
void RequestRestart(PurrNet.RPCInfo info = default)
{
    if (!isServer || CurrentState != GameState.Results) return;
    StartAuthoritativeMatch();
}
```

Broadcast transitions immediately and clock state four times per second. Add `MatchClock.ApplyNetwork` for non-authoritative views.

- [ ] **Step 5: Hide the live Start button and route Play Again to the server.**
- [ ] **Step 6: Run Unity compilation, smoke, and JamValidation.**

### Task 3: Player State and Secure Buy Requests

**Files:**
- Modify: `Assets/Script/PlayerState.cs`
- Modify: `Assets/Script/LapTracker.cs`
- Modify: `Assets/Script/PlayerEffects.cs`
- Modify: `Assets/Script/GameManager.cs`
- Modify: `Assets/Script/PropertyZone.cs`
- Modify: `JamValidation/Program.cs`

**Interfaces:**
- Produces: `PlayerState.OwnerId -> PlayerID?`
- Produces: `PlayerState.ApplyAuthoritativeState(...)`
- Produces: `LapTracker.ApplyAuthoritativeState(int lap, int checkpoint)`
- Produces: `PropertyZone.BoardId`, `PropertyZone.ApplyAuthoritativeOwner(PlayerState owner)`
- Produces RPCs: `RequestBuy(int propertyId, RPCInfo info)`, `SyncPlayer(...)`, `SyncProperty(...)`

- [ ] **Step 1: Add failing tests for invalid caller, stale hold, out-of-zone, insufficient funds, and owned land.**
- [ ] **Step 2: Run JamValidation and confirm failure.**
- [ ] **Step 3: Guard player mutations on a live non-authority client.**
- [ ] **Step 4: Add caller resolution and buy RPC.**

```csharp
PlayerState FindOwnedPlayer(PurrNet.PlayerID sender)
{
    foreach (PlayerState player in players)
        if (player != null && player.OwnerId == sender) return player;
    return null;
}

[PurrNet.ServerRpc(requireOwnership: false)]
void RequestBuy(int propertyId, PurrNet.RPCInfo info = default)
{
    PlayerState player = FindOwnedPlayer(info.sender);
    PropertyZone land = FindProperty(propertyId);
    if (player == null || land == null || !land.CanAuthoritativeBuy(player)) return;
    land.BuyAuthoritative(player);
    PublishPlayer(player);
    PublishProperty(land);
}
```

The two-second hold sends intent and never spends on the client.

- [ ] **Step 5: Add player/property observer RPCs using `PlayerID` and stable integer board IDs.**
- [ ] **Step 6: Run rules and Unity smoke tests.**

### Task 4: Server-Owned Board Outcomes and Catch-up

**Files:**
- Modify: `Assets/Script/CoinPickup.cs`
- Modify: `Assets/Script/BoardEventZone.cs`
- Modify: `Assets/Script/JailZone.cs`
- Modify: `Assets/Script/Checkpoint.cs`
- Modify: `Assets/Script/GameManager.cs`
- Modify: `Assets/Script/PropertyZone.cs`
- Modify: `Assets/Editor/JamCoreSetup.cs`
- Modify: `Assets/Editor/JamEventSmoke.cs`

**Interfaces:**
- Produces stable board IDs for coins/events/properties.
- Produces `CoinPickup.ApplyAuthoritativeAvailability(bool available, float respawnRemaining)`.
- Produces RPCs: `SyncCoin`, `SyncResolvedEvent`, `SyncEffect`, `SyncResults`.

- [ ] **Step 1: Add smoke assertions that a client-view snapshot never awards twice.**
- [ ] **Step 2: Guard coin, toll, checkpoint, event, jail, and liquidation outcomes to server/offline authority.**
- [ ] **Step 3: Broadcast affected player and board state after every accepted outcome.**
- [ ] **Step 4: Override `OnObserverAdded(PlayerID)` and send targeted full catch-up state.**
- [ ] **Step 5: Make `JamCoreSetup` assign positive unique IDs idempotently and make live start fail loudly on duplicates.**
- [ ] **Step 6: Run the plain tests and full Unity event/gameplay smoke.**

### Task 5: Network Preflight and Two-Instance Verification

**Files:**
- Create: `Assets/Editor/JamNetworkValidation.cs`
- Modify: `Assets/Script/GameHud.cs`
- Modify: `Docs/AI/JamCore.md`

**Interfaces:**
- Produces menu: `Game Jam/Validate Network Authority Setup`
- Produces structured log prefix: `[Network Authority Check]`

- [ ] **Step 1: Validate one GameManager, prepared scene identity, unique IDs, owned player identity/transform, and zone authority mappings.**
- [ ] **Step 2: Run preflight and clean Unity compilation.**
- [ ] **Step 3: Start host/client, verify the same automatic countdown, then buy one property and resolve one event from the client.**
- [ ] **Step 4: Compare revision, state/time, player money/laps/score/effects, property owners, coin flags, event, ranking, and winner.**
- [ ] **Step 5: Show a synchronization warning if no snapshot arrives for two seconds.**
- [ ] **Step 6: Re-run JamValidation, offline smoke, network preflight, and two-instance comparison; document external-service limitations.**
