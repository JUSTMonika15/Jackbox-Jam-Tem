using System.Collections.Generic;
using PurrNet;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(LapTracker))]
public class PlayerState : NetworkBehaviour
{
    [SerializeField, Min(0)] private int startingMoney = 50;
    [SerializeField, Min(0)] private int lapReward = 20;
    [SerializeField] private string displayName;
    [SerializeField] private Color playerColor = Color.cyan;
    private readonly List<PropertyZone> nearby = new List<PropertyZone>();
    private EconomyAccount account;
    private FollowCamPlayer movement;
    private PlayerInput input;
    private InputAction interact;
    private LapTracker laps;
    private GameManager game;
    private PurrNet.NetworkIdentity identity;
    private PushAbility push;
    private PlayerKnockback knockback;
    private PlayerEffects effects;
    private CharacterController characterController;
    private readonly RaceRules raceRules = new RaceRules();
    private readonly PurchaseHoldRules purchase = new PurchaseHoldRules();
    private readonly SnapshotRevision stateSnapshotRevision = new SnapshotRevision();
    private int authorityRevision;
    public float PurchaseProgress => purchase.Progress;
    public bool IsPurchaseReadyFor(PropertyZone land) => purchase.IsReadyFor(land);
    private string message;
    private float messageUntil;
    private PlayerFeedbackKind feedbackKind;
    private int feedbackSequence;
    private float nextIdentityRefresh;
    private Vector3 roundStartPosition;
    private Quaternion roundStartRotation;

    internal EconomyAccount Account => account;
    public int Money => account == null ? startingMoney : account.Money;
    public int LapCount => laps == null ? 0 : laps.LapCount;
    public long Score => raceRules.Score(LapCount, Money);
    public int RentEarned { get; private set; }
    public int PropertiesClaimed { get; private set; }
    public int PlayersPushed { get; private set; }
    public float PushCooldownRemaining => push == null ? 0f : push.CooldownRemaining;
    public string DisplayName
    {
        get
        {
            if (TryGetLobbyDisplayName(out string lobbyName)) return lobbyName;
            return string.IsNullOrEmpty(displayName) ? "Player" : displayName;
        }
    }
    public Color PlayerColor => playerColor;
    public string Message => Time.unscaledTime < messageUntil ? message : string.Empty;
    public PlayerFeedbackKind FeedbackKind => feedbackKind;
    public int FeedbackSequence => feedbackSequence;
    public bool CanPlay => game != null && game.CanPlay;
    public bool CanAct => CanPlay && (effects == null || (!effects.IsJailed && !effects.IsWorking));
    // Player state belongs to this network identity.  Do not borrow the scene
    // GameManager's spawn state: if the scene identity initializes later, a
    // client could briefly treat its own wallet/laps as authoritative.
    public bool IsStateAuthority => !isSpawned || isServer;
    public bool IsGrounded => characterController != null && characterController.isGrounded;
    // Reuse the template's existing ownership toggle instead of introducing networking code.
    public bool IsLocalPlayer => movement != null && movement.enabled && input != null && input.enabled;
    public PropertyZone NearbyProperty
    {
        get
        {
            for (int i = nearby.Count - 1; i >= 0; i--)
                if (nearby[i] != null && nearby[i].isActiveAndEnabled) return nearby[i];
            return null;
        }
    }

    public bool IsStandingOn(Collider surface, float verticalTolerance = 0.15f)
        => surface != null && IsStandingOn(surface.bounds, verticalTolerance);

    public bool IsStandingOn(Bounds surface, float verticalTolerance = 0.15f)
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (characterController == null || !characterController.enabled) return false;
        Bounds playerBounds = characterController.bounds;
        bool overlapsHorizontally = BoardContactRules.IsPointWithin(playerBounds.center.x,
            playerBounds.center.z, surface.min.x, surface.max.x, surface.min.z, surface.max.z);
        return overlapsHorizontally && BoardContactRules.IsStandingOnSurface(
            playerBounds.min.y, surface.min.y, surface.max.y, verticalTolerance);
    }

    public bool IsInside(Bounds volume)
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
        return characterController != null && characterController.enabled
            && volume.Intersects(characterController.bounds);
    }

    private void Awake()
    {
        roundStartPosition = transform.position;
        roundStartRotation = transform.rotation;
        account = new EconomyAccount(startingMoney);
        movement = GetComponent<FollowCamPlayer>();
        input = GetComponent<PlayerInput>();
        laps = GetComponent<LapTracker>();
        game = FindAnyObjectByType<GameManager>();
        // This prefab has several NetworkIdentity-derived components.  Using
        // GetComponent<NetworkIdentity>() can select NetworkOwnershipToggle or
        // NetworkTransform instead of this PlayerState and resolve the wrong
        // owner/cookie for the displayed lobby name.
        identity = this;
        push = GetComponent<PushAbility>();
        knockback = GetComponent<PlayerKnockback>();
        effects = GetComponent<PlayerEffects>();
        characterController = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        if (game != null) game.RegisterPlayer(this);
    }

    protected override void OnSpawned()
    {
        base.OnSpawned();
        stateSnapshotRevision.Reset();
        if (isServer) NotifyStateChanged();
    }

    private void Start()
    {
        if (game == null)
        {
            game = FindAnyObjectByType<GameManager>();
            if (game != null) game.RegisterPlayer(this);
            else Debug.LogWarning("PlayerState needs one GameManager in the gameplay scene.", this);
        }
        if (input != null && input.actions != null)
            interact = input.actions.FindAction("Interact", false);
    }

    private void OnDisable()
    {
        if (game != null) game.UnregisterPlayer(this);
        nearby.Clear();
        purchase.Reset();
    }

    private void Update()
    {
        if (isSpawned && isServer && Time.unscaledTime >= nextIdentityRefresh)
        {
            nextIdentityRefresh = Time.unscaledTime + 1f;
            if (TryGetLobbyDisplayName(out string lobbyName) && displayName != lobbyName)
            {
                displayName = lobbyName;
                NotifyStateChanged();
            }
        }
        // If no Interact action exists in the template, E is the prototype fallback.
        bool held = IsLocalPlayer && (interact != null && interact.enabled ? interact.IsPressed()
            : Keyboard.current != null && Keyboard.current.eKey.isPressed);
        UpdatePurchaseHold(held, Time.deltaTime);
    }

    private bool TryGetLobbyDisplayName(out string lobbyName)
    {
        lobbyName = null;
        var orchestrator = PurrNet.Lobby.GameOrchestrator.active;
        var lobby = orchestrator == null ? null : orchestrator.activeLobby;
        // Authentication cookies map each network owner to the matching lobby member. They can
        // arrive after the player object spawns, so the host refreshes this value periodically.
        if (lobby != null && identity != null && identity.owner.HasValue
            && identity.networkManager != null
            && identity.networkManager.playerModule.TryGetCookie(identity.owner.Value, out var lobbyId)
            && PurrNet.Lobby.ILobbyExtensions.TryGetPlayer(lobby, lobbyId, out var member)
            && !string.IsNullOrWhiteSpace(member.displayName))
        {
            lobbyName = member.displayName;
            return true;
        }
        if (IsLocalPlayer && lobby != null && lobby.localPlayer != null
            && !string.IsNullOrWhiteSpace(lobby.localPlayer.displayName))
        {
            lobbyName = lobby.localPlayer.displayName;
            return true;
        }
        if (IsLocalPlayer && orchestrator != null && orchestrator.sessionProvider != null
            && !string.IsNullOrWhiteSpace(orchestrator.sessionProvider.playerName))
        {
            lobbyName = orchestrator.sessionProvider.playerName;
            return true;
        }
        return false;
    }
    public void UpdatePurchaseHold(bool held, float deltaTime)
    {
        PropertyZone land = NearbyProperty;
        bool eligible = IsLocalPlayer && CanAct && land != null && land.Owner == null && Money >= land.Price;
        if (purchase.Tick(land, held, eligible, deltaTime)) TryBuyNearby();
    }
    public void InterruptPurchase()
    {
        purchase.Interrupt();
        ShowMessage("Purchase interrupted — release E to try again");
    }

    public void SetDisplayIdentity(int number)
    {
        if (string.IsNullOrEmpty(displayName)) displayName = "Player " + number;
        playerColor = Color.HSVToRGB((number * 0.23f) % 1f, 0.65f, 1f);
        // Tint only the body; the existing front marker stays distinct for aiming.
        Transform body = transform.Find("Graphics/PlayerBody");
        Renderer bodyRenderer = body == null ? null : body.GetComponent<Renderer>();
        if (bodyRenderer != null)
        {
            var block = new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", playerColor);
            block.SetColor("_Color", playerColor);
            bodyRenderer.SetPropertyBlock(block);
        }
        NotifyStateChanged();
    }

    public void ResetForMatch()
    {
        if (!IsStateAuthority) return;
        purchase.Reset();
        account = new EconomyAccount(startingMoney);
        RentEarned = 0;
        PropertiesClaimed = 0;
        PlayersPushed = 0;
        if (push == null) push = GetComponent<PushAbility>();
        if (knockback == null) knockback = GetComponent<PlayerKnockback>();
        if (push != null) push.ResetAbility();
        if (knockback != null) knockback.ResetKnockback();
        if (effects == null) effects = GetComponent<PlayerEffects>();
        if (effects != null) effects.ResetEffects();
        nearby.Clear();
        laps.ResetLaps();
        message = string.Empty;
        TeleportToRoundStart(roundStartPosition, roundStartRotation);
        if (isSpawned && isServer) ResetRoundClientRpc(roundStartPosition.x, roundStartPosition.y,
            roundStartPosition.z, roundStartRotation.x, roundStartRotation.y, roundStartRotation.z,
            roundStartRotation.w);
        NotifyStateChanged();
    }

    private void TeleportToRoundStart(Vector3 position, Quaternion rotation)
    {
        if (effects == null) effects = GetComponent<PlayerEffects>();
        if (effects != null) effects.Teleport(position);
        else
        {
            if (characterController == null) characterController = GetComponent<CharacterController>();
            bool wasEnabled = characterController != null && characterController.enabled;
            if (characterController != null) characterController.enabled = false;
            transform.position = position;
            if (characterController != null) characterController.enabled = wasEnabled;
        }
        transform.rotation = rotation;
    }

    public void ReturnToSpawnPoint()
    {
        if (!IsStateAuthority) return;
        TeleportToRoundStart(roundStartPosition, roundStartRotation);
    }

    public void AddMoney(int amount)
    {
        if (!CanPlay || !IsStateAuthority) return;
        account.AddMoney(amount);
        ShowFeedback((amount >= 0 ? "+$" : "-$") + System.Math.Abs((long)amount),
            amount >= 0 ? PlayerFeedbackKind.Positive : PlayerFeedbackKind.Negative);
        NotifyStateChanged();
    }

    public bool TrySpend(int amount)
    {
        if (!CanPlay || !IsStateAuthority || !account.TrySpend(amount)) return false;
        NotifyStateChanged();
        return true;
    }
    public int PayMandatory(int amount, PlayerState recipient = null)
    {
        if (!CanPlay || !IsStateAuthority || amount <= 0 || recipient == this) return 0;
        var lands = new List<PropertyZone>();
        var propertyRules = new List<PropertyRules>();
        foreach (PropertyZone land in FindObjectsByType<PropertyZone>())
        {
            if (land.Owner != this) continue;
            lands.Add(land); propertyRules.Add(land.Rules);
        }
        ForcedPaymentResult result = new ForcedPaymentRules().Settle(account, amount,
            propertyRules, recipient != null ? recipient.Account : null);
        int sold = 0;
        foreach (PropertyZone land in lands)
            if (land.RefreshBankSale()) sold++;
        if (sold > 0) ShowMessage("Sold " + sold + " properties to bank at half price");
        if (result.NeedsWork)
        {
            if (effects == null) effects = GetComponent<PlayerEffects>();
            if (effects != null) effects.RequestWork();
            if (isSpawned && isServer) StartWorkClientRpc();
        }
        NotifyStateChanged();
        if (recipient != null) recipient.NotifyStateChanged();
        return result.Paid;
    }
    public bool ConsumeTollPass()
    {
        if (!IsStateAuthority || effects == null || !effects.ConsumeTollPass()) return false;
        if (isSpawned && isServer) ConsumeTollPassClientRpc();
        return true;
    }
    public void ClearNearbyProperties() { nearby.Clear(); purchase.Cancel(); }
    public void OnLapCompleted() => AddMoney(lapReward);
    public void EnterProperty(PropertyZone property)
    {
        if (property != null && !nearby.Contains(property)) nearby.Add(property);
    }
    public void ExitProperty(PropertyZone property)
    {
        if (NearbyProperty == property) purchase.Cancel();
        nearby.Remove(property);
    }
    public void TryBuyNearby()
    {
        PropertyZone property = NearbyProperty;
        if (property == null) return;
        if (IsStateAuthority) property.TryBuy(this);
        else if (isSpawned && game != null) RequestBuyRpc(game.GetPropertyKey(property));
        purchase.Cancel();
    }
    [ServerRpc]
    private void RequestBuyRpc(string propertyKey)
    {
        if (!isServer || game == null) return;
        PropertyZone property = game.FindProperty(propertyKey);
        if (property != null) property.TryBuyFromNetwork(this);
    }
    public void RequestCollectCoin(CoinPickup coin)
    {
        if (coin == null || game == null) return;
        if (IsStateAuthority) coin.TryCollectFromNetwork(this);
        else if (isSpawned) RequestCollectCoinRpc(game.GetCoinKey(coin));
    }
    [ServerRpc]
    private void RequestCollectCoinRpc(string coinKey)
    {
        if (!isServer || game == null) return;
        CoinPickup coin = game.FindCoin(coinKey);
        if (coin != null) coin.TryCollectFromNetwork(this);
    }
    public void RequestPush(Vector3 forward)
    {
        if (push == null) push = GetComponent<PushAbility>();
        if (push == null) return;
        if (IsStateAuthority) push.TryPushAuthoritative(forward);
        else if (isSpawned) RequestPushRpc(forward.x, forward.z);
    }

    [ServerRpc]
    private void RequestPushRpc(float forwardX, float forwardZ)
    {
        if (!isServer) return;
        if (push == null) push = GetComponent<PushAbility>();
        if (push != null) push.TryPushAuthoritative(new Vector3(forwardX, 0f, forwardZ));
    }

    public bool ApplyPushFromAuthority(Vector3 direction, float strength)
    {
        if (!IsStateAuthority) return false;
        if (knockback == null) knockback = GetComponent<PlayerKnockback>();
        bool applied = knockback != null && knockback.ReceivePush(direction, strength);
        if (applied && isSpawned && isServer)
            ApplyPushClientRpc(direction.x, direction.z, strength);
        return applied;
    }

    [ObserversRpc(runLocally: false)]
    private void ApplyPushClientRpc(float directionX, float directionZ, float strength)
    {
        if (knockback == null) knockback = GetComponent<PlayerKnockback>();
        if (knockback != null) knockback.ReceivePush(new Vector3(directionX, 0f, directionZ), strength);
    }
    internal void RecordPurchase() { PropertiesClaimed++; NotifyStateChanged(); }
    internal void RecordPush(int count)
    {
        PlayersPushed = (int)System.Math.Min(int.MaxValue, (long)PlayersPushed + count);
        NotifyStateChanged();
    }
    internal void RecordRent(int amount)
    {
        RentEarned = (int)System.Math.Min(int.MaxValue, (long)RentEarned + amount);
        ShowFeedback("RENT RECEIVED\n+$" + amount, PlayerFeedbackKind.Positive);
        NotifyStateChanged();
    }
    public void ShowMessage(string text) => ShowFeedback(text, PlayerFeedbackKind.Neutral);

    public void ShowFeedback(string text, PlayerFeedbackKind kind)
    {
        if (isSpawned && isServer)
        {
            ApplyMessageRpc(text ?? string.Empty, (int)kind);
            return;
        }
        ApplyMessageLocal(text, kind);
    }

    private void ApplyMessageLocal(string text, PlayerFeedbackKind kind)
    {
        message = text;
        feedbackKind = kind;
        feedbackSequence++;
        messageUntil = Time.unscaledTime + new PlayerFeedbackRules().Duration(kind);
    }

    [ObserversRpc(runLocally: true)]
    private void ApplyMessageRpc(string text, int kind)
    {
        PlayerFeedbackKind resolved = kind >= (int)PlayerFeedbackKind.Neutral
            && kind <= (int)PlayerFeedbackKind.Pass
            ? (PlayerFeedbackKind)kind : PlayerFeedbackKind.Neutral;
        ApplyMessageLocal(text, resolved);
    }

    public void ApplyBoardEffect(BoardEventKind kind)
    {
        if (!IsStateAuthority) return;
        ApplyBoardEffectLocal(kind);
        if (isSpawned && isServer) ApplyBoardEffectClientRpc((int)kind);
    }

    public void ResetPushCooldownFromBoard()
    {
        if (!IsStateAuthority) return;
        if (push == null) push = GetComponent<PushAbility>();
        if (push != null) push.ResetAbility();
        if (isSpawned && isServer) ResetPushCooldownClientRpc();
    }

    [ObserversRpc(runLocally: false)]
    private void ResetPushCooldownClientRpc()
    {
        if (push == null) push = GetComponent<PushAbility>();
        if (push != null) push.ResetAbility();
    }

    private void ApplyBoardEffectLocal(BoardEventKind kind)
    {
        if (effects == null) effects = GetComponent<PlayerEffects>();
        if (effects == null) return;
        switch (kind)
        {
            case BoardEventKind.JailPass: effects.GrantJailPass(); break;
            case BoardEventKind.ReturnStart: effects.ReturnToStart(); break;
            case BoardEventKind.Jail: effects.TryJail(); break;
            case BoardEventKind.SpeedUp: effects.SetSpeed(1.5f, 5f); break;
            case BoardEventKind.SlowDown: effects.SetSpeed(0.6f, 3f); break;
            case BoardEventKind.TollPass: effects.GrantTollPass(); break;
        }
    }

    [ObserversRpc(runLocally: false)]
    private void ApplyBoardEffectClientRpc(int kind)
    {
        if (kind < (int)BoardEventKind.JailPass || kind > (int)BoardEventKind.TollPass) return;
        ApplyBoardEffectLocal((BoardEventKind)kind);
    }

    [ObserversRpc(runLocally: false)]
    private void ConsumeTollPassClientRpc()
    {
        if (effects == null) effects = GetComponent<PlayerEffects>();
        if (effects != null) effects.ConsumeTollPass();
    }

    [ObserversRpc(runLocally: false)]
    private void StartWorkClientRpc()
    {
        if (effects == null) effects = GetComponent<PlayerEffects>();
        if (effects != null) effects.RequestWork();
    }

    [ObserversRpc(runLocally: false)]
    private void ResetRoundClientRpc(float positionX, float positionY, float positionZ,
        float rotationX, float rotationY, float rotationZ, float rotationW)
    {
        if (effects == null) effects = GetComponent<PlayerEffects>();
        if (effects != null) effects.ResetEffects();
        if (push == null) push = GetComponent<PushAbility>();
        if (push != null) push.ResetAbility();
        purchase.Reset();
        TeleportToRoundStart(new Vector3(positionX, positionY, positionZ),
            new Quaternion(rotationX, rotationY, rotationZ, rotationW));
    }

    public void NotifyStateChanged()
    {
        if (!IsStateAuthority || !isSpawned || !isServer || account == null || laps == null) return;
        ApplyPlayerStateRpc(++authorityRevision, account.Money, laps.LapCount, laps.NextCheckpoint,
            RentEarned, PropertiesClaimed, PlayersPushed, displayName,
            playerColor.r, playerColor.g, playerColor.b);
    }

    [ObserversRpc(runLocally: true, bufferLast: true)]
    private void ApplyPlayerStateRpc(int revision, int money, int lapCount, int nextCheckpoint,
        int rentEarned, int propertiesClaimed, int playersPushed, string syncedName,
        float red, float green, float blue)
    {
        if (!stateSnapshotRevision.TryAccept(revision)) return;
        if (account == null) account = new EconomyAccount(0);
        if (laps == null) laps = GetComponent<LapTracker>();
        account.SetMoney(money);
        if (laps != null) laps.ApplyNetworkState(lapCount, nextCheckpoint);
        RentEarned = Mathf.Max(0, rentEarned);
        PropertiesClaimed = Mathf.Max(0, propertiesClaimed);
        PlayersPushed = Mathf.Max(0, playersPushed);
        if (!string.IsNullOrWhiteSpace(syncedName)) displayName = syncedName;
        playerColor = new Color(Mathf.Clamp01(red), Mathf.Clamp01(green), Mathf.Clamp01(blue));
        ApplyPlayerColor();
        if (game != null) game.RefreshResults();
    }

    private void ApplyPlayerColor()
    {
        Transform body = transform.Find("Graphics/PlayerBody");
        Renderer bodyRenderer = body == null ? null : body.GetComponent<Renderer>();
        if (bodyRenderer == null) return;
        var block = new MaterialPropertyBlock();
        bodyRenderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", playerColor);
        block.SetColor("_Color", playerColor);
        bodyRenderer.SetPropertyBlock(block);
    }
}
