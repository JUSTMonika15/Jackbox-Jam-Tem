using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerState), typeof(PlayerKnockback))]
public class PushAbility : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float range = 2.2f;
    [SerializeField, Range(-1f, 1f)] private float minimumForwardDot = 0.35f;
    [SerializeField, Min(0.1f)] private float strength = 12f;
    [SerializeField, Min(0f)] private float cooldownSeconds = 3f;
    [SerializeField] private LayerMask hitMask = ~0;
    private readonly HashSet<PlayerKnockback> hitPlayers = new HashSet<PlayerKnockback>();
    private PlayerState player;
    private FollowCamPlayer movement;
    private CharacterController controller;
    private InputAction action;
    private PushCooldown cooldown;
    public float CooldownRemaining => cooldown == null ? 0f : cooldown.Remaining(Time.time);
    private void Awake()
    {
        player = GetComponent<PlayerState>();
        movement = GetComponent<FollowCamPlayer>();
        controller = GetComponent<CharacterController>();
        cooldown = new PushCooldown(cooldownSeconds);
    }
    private void Start()
    {
        PlayerInput input = GetComponent<PlayerInput>();
        if (input != null && input.actions != null)
        {
            action = input.actions.FindAction("Push", false);
            // Template Attack already binds mouse left and the gamepad west button.
            if (action == null) action = input.actions.FindAction("Attack", false);
        }
    }
    private void Update()
    {
        if (!player.IsLocalPlayer || !player.CanAct) return;
        bool pressed = action != null && action.enabled ? action.WasPressedThisFrame()
            : Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (pressed) TryPush();
    }
    public int TryPush()
    {
        if (player == null || !player.IsLocalPlayer || !cooldown.TryUse(player.CanAct, Time.time)) return 0;
        Vector3 requestedForward = movement != null ? movement.FacingDirection : Vector3.forward;
        if (!player.IsStateAuthority)
        {
            player.RequestPush(requestedForward);
            player.ShowMessage("Push requested");
            return 0;
        }
        return TryPushAuthoritative(requestedForward, true);
    }

    internal int TryPushAuthoritative(Vector3 requestedForward, bool cooldownAlreadyUsed = false)
    {
        if (player == null || !player.IsStateAuthority) return 0;
        if (!cooldownAlreadyUsed && !cooldown.TryUse(player.CanAct, Time.time)) return 0;
        Vector3 origin = controller != null ? controller.bounds.center : transform.position;
        requestedForward.y = 0f;
        Vector3 forward = requestedForward.sqrMagnitude > .001f ? requestedForward.normalized : Vector3.forward;
        hitPlayers.Clear();
        int pushed = 0;
        // Allocation occurs only once per ability use, not every frame; avoids silently truncating crowded hits.
        foreach (Collider hit in Physics.OverlapSphere(origin, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            PlayerKnockback target = hit.GetComponentInParent<PlayerKnockback>();
            if (target == null || target.gameObject == gameObject || hitPlayers.Contains(target)) continue;
            CharacterController targetController = target.GetComponent<CharacterController>();
            Vector3 targetCenter = targetController.bounds.center;
            Vector3 delta = targetCenter - origin;
            delta.y = 0f;
            if (delta.sqrMagnitude > range * range) continue;
            Vector3 direction = delta.sqrMagnitude > 0.001f ? delta.normalized : forward;
            if (Vector3.Dot(forward, direction) < minimumForwardDot) continue;
            // Walls stop pushes. Trigger zones and coins do not block the line of sight.
            Vector3 ray = targetCenter - origin;
            if (ray.sqrMagnitude > 0.001f && Physics.Raycast(origin, ray.normalized, out RaycastHit blocker,
                ray.magnitude, hitMask, QueryTriggerInteraction.Ignore)
                && blocker.collider.GetComponentInParent<PlayerKnockback>() != target) continue;
            hitPlayers.Add(target);
            PlayerState targetPlayer = target.GetComponent<PlayerState>();
            if (targetPlayer != null && targetPlayer.ApplyPushFromAuthority(direction, strength)) pushed++;
        }
        player.RecordPush(pushed);
        player.ShowMessage(pushed > 0 ? "Pushed " + pushed + " player(s)!" : "Push missed");
        return pushed;
    }
    public void ResetAbility()
    {
        if (cooldown != null) cooldown.Reset();
        hitPlayers.Clear();
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
