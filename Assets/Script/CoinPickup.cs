using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class CoinPickup : MonoBehaviour
{
    [SerializeField, Min(1)] private int value = 5;
    [SerializeField, Min(0.1f)] private float respawnDelay = 10f;
    [SerializeField] private Renderer coinRenderer;
    [SerializeField, Min(0f)] private float networkRequestTolerance = 1.25f;
    private bool available = true;
    private float respawnAt;
    private float nextClientRequestTime;
    private GameManager game;
    public bool IsAvailable => available;
    private void Awake()
    {
        GetComponent<SphereCollider>().isTrigger = true;
        if (coinRenderer == null) coinRenderer = GetComponentInChildren<Renderer>();
        game = FindAnyObjectByType<GameManager>();
    }
    private void Update()
    {
        if (game == null) game = FindAnyObjectByType<GameManager>();
        if (game == null || !game.IsAuthoritative || !game.CanPlay) return;
        if (!available)
        {
            if (Time.time >= respawnAt) ResetPickup();
            return;
        }
        // Remote CharacterControllers are moved by NetworkTransform on the host, so their
        // trigger callbacks are not guaranteed. The host also polls its replicated positions.
        foreach (PlayerState player in game.Players)
        {
            if (TryCollectFromNetwork(player)) break;
        }
    }
    private void OnTriggerEnter(Collider other) => TryCollect(other);
    private void OnTriggerStay(Collider other) => TryCollect(other);
    private void TryCollect(Collider other)
    {
        if (!available || game == null) return;
        PlayerState player = other.GetComponentInParent<PlayerState>();
        if (player == null || !player.CanAct) return;
        if (!game.IsAuthoritative)
        {
            if (!player.IsLocalPlayer || Time.unscaledTime < nextClientRequestTime) return;
            nextClientRequestTime = Time.unscaledTime + .25f;
            player.RequestCollectCoin(this);
            return;
        }
        CollectAuthoritative(player);
    }
    internal bool TryCollectFromNetwork(PlayerState player)
    {
        if (game == null) game = FindAnyObjectByType<GameManager>();
        if (!available || game == null || !game.IsAuthoritative || player == null || !player.CanAct) return false;
        SphereCollider trigger = GetComponent<SphereCollider>();
        if (trigger == null) return false;
        Vector3 closest = trigger.ClosestPoint(player.transform.position);
        if (!RequestValidation.WithinRange((player.transform.position-closest).sqrMagnitude,
            networkRequestTolerance)) return false;
        CollectAuthoritative(player);
        return true;
    }
    private void CollectAuthoritative(PlayerState player)
    {
        if (!available || player == null || !player.CanAct) return;
        available = false;
        respawnAt = Time.time + Mathf.Max(0.1f, respawnDelay);
        if (coinRenderer != null) coinRenderer.enabled = false;
        player.AddMoney(Mathf.Max(1, value));
        game.BroadcastCoinState(this);
    }
    public void ResetPickup()
    {
        available = true;
        if (coinRenderer != null) coinRenderer.enabled = true;
        if (game != null && game.IsAuthoritative) game.BroadcastCoinState(this);
    }
    internal void ApplyNetworkAvailable(bool isAvailable)
    {
        available = isAvailable;
        if (isAvailable) nextClientRequestTime = 0f;
        if (coinRenderer != null) coinRenderer.enabled = isAvailable;
    }
}
