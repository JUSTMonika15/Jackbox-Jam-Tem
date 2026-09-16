# Host-Authoritative Jam Gameplay Design

## Goal

All players entering `MainGame` must see one match state. The listen host/server is the only authority for the countdown, timer, money, laps, properties, coins, event draws, jail/work effects, scoring, and results. Clients may control their own movement and submit gameplay intents, but may not directly decide outcome-affecting state.

This work uses the installed PurrNet API from the project. It does not modify package source or replace the lobby/template transport.

## Scope

### Included

- Automatically begin a synchronized three-second countdown after the lobby's players have spawned in `MainGame`.
- Keep the existing owner-authoritative `NetworkTransform` movement.
- Make the host/server authoritative for match state and all score-affecting values.
- Synchronize player money/laps/effects, property ownership, coin availability, event outcomes, timer, rankings, and winner.
- Validate client requests on the server, including caller identity, match state, proximity/zone eligibility, funds, and current ownership/availability.
- Send a full current snapshot to newly observing or recovering clients.
- Preserve offline/single-editor Play Mode as a development fallback.

### Deferred

- Host migration during an active match.
- Server rewind, prediction, lag compensation, and anti-cheat beyond request validation.
- Replacing the existing lobby, relay, authentication, movement synchronization, or transport.
- The city-name and board-art polish pass. That follows once two clients share the same state.

## Architecture

`GameManager` becomes the one scene-level PurrNet network authority. It owns an authoritative revisioned snapshot and uses the scene's existing network lifecycle. All rule components remain focused on their current jobs, but mutation entry points become server-only. Clients apply received values as presentation state.

The player prefab retains its existing network identity/ownership and `NetworkTransform`. A player is identified by the owner `PlayerID` already assigned by PurrNet, not by list order or display name. Properties and coins receive stable serialized board IDs so snapshot references do not depend on Unity discovery order.

The authority broadcasts small transition updates immediately after accepted mutations and periodically sends the global clock. A newly added observer receives one targeted full snapshot. This avoids adding independent network identities to every property and coin while still giving late clients the complete board.

## Match Start and Clock

Only the server may start or reset the match. On entering `MainGame`, it waits until the expected lobby members have spawned. If the lobby count cannot be read, it uses a short stable-player-count grace period rather than waiting forever. It then resets authoritative state and broadcasts `Countdown` with three seconds remaining.

The server ticks the clock and transitions `Waiting -> Countdown -> Playing -> Results`. Clients never advance the authoritative clock. They display the latest server value and may interpolate it visually between updates, but the next server update always wins.

The old Start button becomes a development/offline fallback. In a live session it is hidden or disabled, because the server starts automatically.

## Requests and Validation

Clients send intent through `ServerRpc(requireOwnership: false)`. Every request accepts `RPCInfo` and uses `info.sender` to resolve the caller's owned player; a client-provided player reference is never trusted.

- Buy: server checks Playing, caller can act, caller is inside the property, property is unowned, hold completed, and money is sufficient.
- Push: movement/knockback remains the existing prototype behavior initially; only the push statistic is accepted after server validation. Full server-authoritative combat is deferred.
- Coins, tolls, checkpoints, event zones, jail traps: evaluated on the server from server-observed player transforms and zone contacts. Remote clients do not draw random events or mutate wallets locally.
- Random events: the server draws once, applies once, and broadcasts the resolved event plus resulting state/message.
- Restart: only the server accepts it, resets all authoritative state, then broadcasts a fresh countdown.

Rejected requests make no state change. The requesting client receives the next authoritative snapshot, which corrects stale UI.

## Snapshot Contents

The revisioned snapshot contains:

- global state, countdown/remaining time, and match revision;
- per player: owner ID, money, lap/checkpoint progress, jail/work/speed/card state, statistics, color, score, and result rank;
- per property: stable ID and owner ID;
- per coin: stable ID, available flag, and server respawn time;
- most recent resolved event/message when presentation needs it.

Snapshots are applied only when their revision is newer. UI, labels, property colors, coin renderers, and result screens read the applied snapshot. They do not calculate an alternative outcome.

## Existing Component Changes

- `GameManager`: network lifecycle, automatic countdown, authoritative clock, request RPCs, snapshot send/apply, late-observer recovery.
- `PlayerState`: separates authoritative mutation from read-only presentation application; exposes stable owner identity.
- `LapTracker` and `PlayerEffects`: server mutation guards and snapshot application hooks.
- `PropertyZone`, `CoinPickup`, `BoardEventZone`, `JailZone`: server-only outcome calculation, stable IDs where required, and view updates from authority.
- `GameHud`: consumes replicated state and removes the live-session Start button.
- Editor setup/validation: safely adds or verifies the scene network authority and stable board IDs without duplicating objects.

No PurrNet package files are edited.

## Offline Compatibility

When no PurrNet server/client is active, `GameManager` treats the local process as authority. Existing Play Mode checks and fast iteration continue to work. Network-only RPC paths are bypassed in this mode.

## Failure Handling

- Missing or duplicate stable board IDs log an error and block network match start rather than silently mapping the wrong property.
- Missing owner/player mappings reject the request and trigger a corrective snapshot.
- A disconnected client loses control of its player; host migration is not attempted in this Jam version.
- If the host leaves, the existing lobby/session behavior ends the game for clients.

## Validation

1. Plain C# tests cover authority decisions, request rejection, snapshot revisions, reset, and deterministic state application.
2. Unity compilation and current single-process gameplay smoke tests remain green.
3. A two-instance test creates a lobby and enters `MainGame` with host and client.
4. Both instances must show the same countdown and timer (within one update interval), money, laps, property owner/colors, coin availability, event result, score, ranking, and winner.
5. A client purchase request and a server-triggered random event must be observed identically on both instances.
6. No client may locally mutate a score-affecting value after receiving a newer server revision.

## Follow-up Visual Pass

After multiplayer state equality passes, presentation can be improved without changing authority: large fortune/chance/jail signs, property city names with price/rent placards, clearer purchase cards, stronger board color grouping, and improved hierarchy/spacing. Existing primitives and TextMesh/UI are enough for a readable Jam version; external art is optional polish, not a prerequisite.
