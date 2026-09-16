using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class JailZone : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float sentenceSeconds = 5f;
    [SerializeField] float safeFeetHeight = 0.4f;
    BoxCollider trigger;
    Bounds activationBounds;
    GameManager game;
    void Awake()
    {
        trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
        activationBounds = trigger.bounds;
        // Keep this collider as an authoring volume only. CharacterController can stop at a
        // trigger edge without dispatching OnTrigger callbacks in this setup.
        trigger.enabled = false;
        game = FindAnyObjectByType<GameManager>();
    }
    // CharacterController-only players do not guarantee trigger callbacks when neither side has a
    // Rigidbody. Poll the tiny player list so the board trap remains non-blocking and reliable.
    void Update()
    {
        if (game == null) game = FindAnyObjectByType<GameManager>();
        if (game == null || !game.IsAuthoritative) return;
        foreach (PlayerState player in game.Players)
        {
            if (player == null) continue;
            if (player.CanAct && player.IsStandingOn(activationBounds, safeFeetHeight)) Check(player);
        }
    }
    void Check(PlayerState player)
    {
        if (player == null || !player.IsStateAuthority || !player.CanAct) return;
        PlayerEffects effects = player.GetComponent<PlayerEffects>();
        if (effects != null) effects.TryJail(sentenceSeconds);
    }
}
