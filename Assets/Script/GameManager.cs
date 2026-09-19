using System.Collections.Generic;
using PurrNet;
using UnityEngine;

public class GameManager : NetworkIdentity
{
    [SerializeField, Min(1f)] private float matchDuration = 180f;
    [SerializeField, Min(0f)] private float countdownDuration = 3f;
    [SerializeField] private bool autoStartWhenPlayerArrives;
    [SerializeField] private Transform raceStart;
    [SerializeField] private Transform jailPosition;
    [SerializeField] private Transform jailExit;
    [SerializeField, Min(.5f)] private float networkAutoStartDelay = 2f;
    private readonly List<PlayerState> players = new List<PlayerState>();
    private readonly List<PlayerState> results = new List<PlayerState>();
    private MatchClock clock;
    private readonly SnapshotRevision matchSnapshotRevision = new SnapshotRevision();
    private int authorityRevision;
    private int observedPlayerCount = -1;
    private float playerCountStableSince;
    private float nextClockBroadcast;
    private float lastNetworkSnapshotTime;
    private GameState lastBroadcastState = (GameState)(-1);
    private bool protocolVersionReceived;
    private bool protocolCompatible = true;
    private float networkSpawnedAt;

    public GameState CurrentState => clock == null ? GameState.Waiting : clock.State;
    public bool CanPlay => CurrentState == GameState.Playing;
    public float TimeRemaining => clock == null ? matchDuration : clock.Remaining;
    public float CountdownRemaining => clock == null ? countdownDuration : clock.CountdownRemaining;
    public IReadOnlyList<PlayerState> Players => players;
    public IReadOnlyList<PlayerState> Results => results;
    public bool IsNetworked => isSpawned;
    public bool IsAuthoritative => AuthorityGate.IsAuthoritative(isSpawned, isServer);
    public bool CanControlMatch => IsAuthoritative;
    public float NetworkSnapshotAge => !isSpawned || isServer ? 0f : Time.unscaledTime - lastNetworkSnapshotTime;
    public bool HasRaceStart => raceStart != null;
    public bool HasJail => jailPosition != null && jailExit != null;
    public string NetworkVersionLabel => "NET v" + NetworkProtocolRules.CurrentVersion;
    public bool HasProtocolWarning => isSpawned && !isServer
        && ((!protocolVersionReceived && Time.unscaledTime - networkSpawnedAt > 2f) || !protocolCompatible);
    public Vector3 RaceStartPosition => raceStart.position;
    public Vector3 JailPosition => jailPosition.position;
    public Vector3 JailExitPosition => jailExit.position;

    private void Awake()
    {
        clock = new MatchClock(countdownDuration, matchDuration);
        if (GetComponent<ContinuousBoard>() == null) gameObject.AddComponent<ContinuousBoard>();
        if (GetComponent<BoardCamera>() == null) gameObject.AddComponent<BoardCamera>();
        if (GetComponent<GameHud>() == null) gameObject.AddComponent<GameHud>();
    }

    private void Update()
    {
        if (!IsAuthoritative) return;

        if (players.Count != observedPlayerCount)
        {
            observedPlayerCount = players.Count;
            playerCountStableSince = Time.unscaledTime;
        }

        int expectedPlayers = players.Count;
        var orchestrator = PurrNet.Lobby.GameOrchestrator.active;
        if (orchestrator != null && orchestrator.activeLobby != null)
            expectedPlayers = Mathf.Max(1, orchestrator.activeLobby.players.Count);
        bool shouldAutoStart = isSpawned
            ? players.Count >= expectedPlayers && players.Count > 0
                && Time.unscaledTime - playerCountStableSince >= networkAutoStartDelay
            : autoStartWhenPlayerArrives && GetLocalPlayer() != null;
        if (CurrentState == GameState.Waiting && shouldAutoStart) StartAuthoritativeMatch();

        GameState previous = clock.State;
        clock.Tick(Time.deltaTime);
        if (previous != GameState.Results && clock.State == GameState.Results)
            BuildResults();

        if (isSpawned && (clock.State != lastBroadcastState || Time.unscaledTime >= nextClockBroadcast))
            BroadcastMatchState();
    }

    protected override void OnSpawned()
    {
        base.OnSpawned();
        matchSnapshotRevision.Reset();
        lastNetworkSnapshotTime = Time.unscaledTime;
        networkSpawnedAt = Time.unscaledTime;
        protocolVersionReceived = isServer;
        protocolCompatible = true;
        observedPlayerCount = players.Count;
        playerCountStableSince = Time.unscaledTime;
        if (isServer)
        {
            ApplyProtocolVersionRpc(NetworkProtocolRules.CurrentVersion);
            BroadcastMatchState();
        }
    }

    [ObserversRpc(runLocally: true, bufferLast: true)]
    private void ApplyProtocolVersionRpc(int version)
    {
        protocolVersionReceived = true;
        protocolCompatible = NetworkProtocolRules.IsCompatible(NetworkProtocolRules.CurrentVersion, version);
    }

    protected override void OnObserverAdded(PlayerID player)
    {
        base.OnObserverAdded(player);
        if (!isServer) return;
        ApplyProtocolVersionTargetRpc(player, NetworkProtocolRules.CurrentVersion);
        foreach (PropertyZone property in FindObjectsByType<PropertyZone>())
        {
            PlayerState owner = property.Owner;
            Color color = owner == null ? Color.clear : owner.PlayerColor;
            ApplyPropertyStateTargetRpc(player, GetPropertyKey(property), owner == null ? null : owner.transform,
                color.r, color.g, color.b);
        }
        foreach (CoinPickup coin in FindObjectsByType<CoinPickup>())
            ApplyCoinStateTargetRpc(player, GetSceneKey(coin.transform), coin.IsAvailable);
    }

    [TargetRpc]
    private void ApplyProtocolVersionTargetRpc(PlayerID target, int version)
    {
        protocolVersionReceived = true;
        protocolCompatible = NetworkProtocolRules.IsCompatible(NetworkProtocolRules.CurrentVersion, version);
    }

    public void RegisterPlayer(PlayerState player)
    {
        if (player == null || players.Contains(player)) return;
        players.Add(player);
        player.SetDisplayIdentity(players.Count);
    }

    public void UnregisterPlayer(PlayerState player)
    {
        players.Remove(player);
        results.Remove(player);
    }

    public PlayerState GetLocalPlayer()
    {
        foreach (PlayerState player in players)
            if (player != null && player.IsLocalPlayer) return player;
        return null;
    }

    public string GetPropertyKey(PropertyZone property)
    {
        return property == null ? string.Empty : GetSceneKey(property.transform);
    }

    private static string GetSceneKey(Transform target)
    {
        if (target == null) return string.Empty;
        var segments = new List<string>();
        Transform current = target;
        while (current != null)
        {
            int sameNameOrdinal = 0;
            Transform parent = current.parent;
            int siblingIndex = current.GetSiblingIndex();
            if (parent != null)
            {
                for (int i = 0; i < siblingIndex; i++)
                    if (parent.GetChild(i).name == current.name) sameNameOrdinal++;
            }
            segments.Add(NetworkObjectKeyRules.Segment(current.name,sameNameOrdinal));
            current = current.parent;
        }
        segments.Reverse();
        return string.Join("/", segments);
    }

    public PropertyZone FindProperty(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (PropertyZone property in FindObjectsByType<PropertyZone>())
            if (GetPropertyKey(property) == key) return property;
        return null;
    }

    public string GetCoinKey(CoinPickup coin)
    {
        return coin == null ? string.Empty : GetSceneKey(coin.transform);
    }

    internal CoinPickup FindCoin(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (CoinPickup coin in FindObjectsByType<CoinPickup>())
            if (GetSceneKey(coin.transform) == key) return coin;
        return null;
    }

    public void StartMatch()
    {
        if (!IsAuthoritative) return;
        StartAuthoritativeMatch();
    }

    private void StartAuthoritativeMatch()
    {
        if (CurrentState != GameState.Waiting && CurrentState != GameState.Results) return;
        if (GetLocalPlayer() == null) return;
        results.Clear();
        foreach (PropertyZone property in FindObjectsByType<PropertyZone>())
            property.ResetProperty();
        foreach (CoinPickup coin in FindObjectsByType<CoinPickup>())
            coin.ResetPickup();
        foreach (BoardEventZone zone in FindObjectsByType<BoardEventZone>()) zone.ResetZone();
        foreach (SpecialBoardTile tile in FindObjectsByType<SpecialBoardTile>()) tile.ResetZone();
        foreach (PlayerState player in players)
            if (player != null) player.ResetForMatch();
        clock = new MatchClock(countdownDuration, matchDuration);
        clock.Begin();
        if (isSpawned) BroadcastMatchState();
    }

    private void BroadcastMatchState()
    {
        if (!isSpawned || !isServer) return;
        lastBroadcastState = clock.State;
        nextClockBroadcast = Time.unscaledTime + .25f;
        ApplyMatchStateRpc(++authorityRevision, (int)clock.State, clock.Remaining, clock.CountdownRemaining);
    }

    [ObserversRpc(runLocally: true, bufferLast: true)]
    private void ApplyMatchStateRpc(int revision, int state, float remaining, float countdownRemaining)
    {
        if (!matchSnapshotRevision.TryAccept(revision)) return;
        if (state < (int)GameState.Waiting || state > (int)GameState.Results) return;
        clock.ApplyNetwork((GameState)state, remaining, countdownRemaining);
        lastNetworkSnapshotTime = Time.unscaledTime;
        if (clock.State == GameState.Results) BuildResults();
        else results.Clear();
    }

    private void BuildResults()
    {
        results.Clear();
        foreach (PlayerState player in players)
            if (player != null) results.Add(player);
        results.Sort((a, b) => b.Score.CompareTo(a.Score));
    }

    public void RefreshResults()
    {
        if (CurrentState == GameState.Results) BuildResults();
    }

    public void BroadcastPropertyState(PropertyZone property)
    {
        if (property == null || !isSpawned || !isServer) return;
        PlayerState owner = property.Owner;
        Color color = owner == null ? Color.clear : owner.PlayerColor;
        ApplyPropertyStateRpc(GetPropertyKey(property), owner == null ? null : owner.transform,
            color.r, color.g, color.b);
    }

    [ObserversRpc(runLocally: false)]
    private void ApplyPropertyStateRpc(string key, Transform ownerTransform, float red, float green, float blue)
    {
        ApplyPropertyState(key, ownerTransform, red, green, blue);
    }

    [TargetRpc]
    private void ApplyPropertyStateTargetRpc(PlayerID target, string key, Transform ownerTransform,
        float red, float green, float blue)
    {
        ApplyPropertyState(key, ownerTransform, red, green, blue);
    }

    private void ApplyPropertyState(string key, Transform ownerTransform, float red, float green, float blue)
    {
        PropertyZone property = FindProperty(key);
        if (property == null) return;
        PlayerState owner = ownerTransform == null ? null : ownerTransform.GetComponent<PlayerState>();
        property.ApplyNetworkOwner(owner, new Color(red, green, blue));
    }

    public void BroadcastCoinState(CoinPickup coin)
    {
        if (coin == null || !isSpawned || !isServer) return;
        ApplyCoinStateRpc(GetSceneKey(coin.transform), coin.IsAvailable);
    }

    [ObserversRpc(runLocally: false)]
    private void ApplyCoinStateRpc(string key, bool available)
    {
        CoinPickup coin = FindCoin(key);
        if (coin != null) coin.ApplyNetworkAvailable(available);
    }

    [TargetRpc]
    private void ApplyCoinStateTargetRpc(PlayerID target, string key, bool available)
    {
        CoinPickup coin = FindCoin(key);
        if (coin != null) coin.ApplyNetworkAvailable(available);
    }
    public void ForgetZoneContacts(PlayerState player)
    {
        foreach (PropertyZone property in FindObjectsByType<PropertyZone>()) property.ForgetVisitor(player);
        foreach (BoardEventZone zone in FindObjectsByType<BoardEventZone>()) zone.ForgetVisitor(player);
        foreach (SpecialBoardTile tile in FindObjectsByType<SpecialBoardTile>()) tile.ForgetVisitor(player);
        player.ClearNearbyProperties();
    }
}
