using UnityEngine;
using UnityEngine.InputSystem;

public class FollowCamPlayer : MonoBehaviour
{
    public CharacterController controller;
    public PlayerInput playerInput; 
    public float speed = 4f;
    public float gravity = -20f;
    [SerializeField, Min(0.1f)] private float jumpHeight = 1.5f;
    [SerializeField, Min(1f)] private float shoulderTurnSpeed = 120f;


    private Vector3 playerMovement;
    private InputAction moveAction;
    private InputAction jumpAction;
    private PlayerEffects effects;
    private readonly JumpRules jumpRules = new JumpRules();
    private float verticalSpeed;
    private PlayerState playerState;
    private PlayerKnockback knockback;
    private PlayerSfx sfx;
    private Transform graphics;
    private Camera movementCamera;
    private readonly CameraRelativeMovementRules movementRules = new CameraRelativeMovementRules();
    private readonly ShoulderMovementRules shoulderMovementRules = new ShoulderMovementRules();
    public Vector3 FacingDirection { get; private set; } = Vector3.forward;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerState = GetComponent<PlayerState>();
        knockback = GetComponent<PlayerKnockback>();
        graphics = transform.Find("Graphics");
        // Primitive meshes used as temporary visuals include colliders by default. They must not
        // fight the root CharacterController or leave a moving "static" collider behind.
        if (graphics != null)
            foreach (Collider visualCollider in graphics.GetComponentsInChildren<Collider>(true))
                visualCollider.enabled = false;
        FacingDirection = Vector3.right;
        if (graphics != null) graphics.rotation = Quaternion.LookRotation(FacingDirection);
        effects = GetComponent<PlayerEffects>();
        sfx = GetComponent<PlayerSfx>();
        movementCamera = Camera.main;
        if (sfx == null) sfx = gameObject.AddComponent<PlayerSfx>();
        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }
        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }
        moveAction = playerInput.actions.FindAction("Move", true);
        jumpAction = playerInput.actions.FindAction("Jump", false);

    }

    // Update is called once per frame
    void Update()
    {
        if (playerState != null && !playerState.CanAct) return;
        if (moveAction == null || controller == null) return;
        Vector2 input = moveAction.ReadValue<Vector2>();
        if (movementCamera == null) movementCamera = Camera.main;
        bool shoulderView = movementCamera != null && !movementCamera.orthographic;
        if (shoulderView)
        {
            ShoulderMovementFrame frame = shoulderMovementRules.Resolve(
                FacingDirection.x, FacingDirection.z, input.x, input.y,
                shoulderTurnSpeed * Time.deltaTime);
            FacingDirection = new Vector3(frame.FacingX, 0f, frame.FacingZ);
            playerMovement = new Vector3(frame.MoveX, 0f, frame.MoveZ);
            if (graphics != null) graphics.rotation = Quaternion.LookRotation(FacingDirection);
        }
        else
        {
            Vector3 cameraRight = movementCamera != null ? movementCamera.transform.right : Vector3.right;
            Vector3 cameraForward = movementCamera != null ? movementCamera.transform.forward : Vector3.forward;
            PlanarMovement planarMovement = movementRules.Resolve(
                input.x, input.y, cameraRight.x, cameraRight.z, cameraForward.x, cameraForward.z);
            playerMovement = new Vector3(planarMovement.X, 0f, planarMovement.Z);
            if (playerMovement.sqrMagnitude > 0.001f)
            {
                FacingDirection = playerMovement.normalized;
                // Rotate only the model, never the player root or its fixed follow camera.
                if (graphics != null) graphics.rotation = Quaternion.LookRotation(FacingDirection);
            }
        }
        playerMovement = Vector3.ClampMagnitude(playerMovement, 1f) * speed * (effects != null ? effects.SpeedMultiplier : 1f);
        if (controller.isGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -2f;
        }
        bool jumpPressed = jumpAction != null && jumpAction.enabled ? jumpAction.WasPressedThisFrame()
            : Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        if (PlayerSfxRules.ShouldPlayJump(controller.isGrounded, jumpPressed))
        {
            verticalSpeed = jumpRules.InitialVelocity(jumpHeight, gravity);
            sfx.PlayJump();
        }
        verticalSpeed += gravity * Time.deltaTime;
        playerMovement.y = verticalSpeed;
        if (knockback != null) playerMovement += knockback.Velocity;
        controller.Move(playerMovement * Time.deltaTime);
    }
    public void ResetVerticalMotion() { verticalSpeed = 0f; }
}
