using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(PlayerState))]
public class PlayerKnockback : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float deceleration = 24f;
    private CharacterController controller;
    private FollowCamPlayer movement;
    private PlayerState player;
    public Vector3 Velocity { get; private set; }
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        movement = GetComponent<FollowCamPlayer>();
        player = GetComponent<PlayerState>();
    }
    public bool ReceivePush(Vector3 direction, float strength)
    {
        if (!isActiveAndEnabled || player == null || !player.CanAct || strength <= 0f) return false;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return false;
        player.InterruptPurchase();
        Velocity = direction.normalized * strength;
        player.ShowMessage("Pushed!");
        return true;
    }
    private void LateUpdate()
    {
        if (player == null || !player.CanAct) { ResetKnockback(); return; }
        // Local movement combines this velocity into its single Move call.
        // A passive character can still be displaced in the local gameplay prototype.
        if ((movement == null || !movement.enabled) && controller.enabled && Velocity.sqrMagnitude > 0.001f)
            controller.Move(Velocity * Time.deltaTime);
        Velocity = Vector3.MoveTowards(Velocity, Vector3.zero, Mathf.Max(0.1f, deceleration) * Time.deltaTime);
    }
    public void ResetKnockback() { Velocity = Vector3.zero; }
    private void OnDisable() { ResetKnockback(); }
}
