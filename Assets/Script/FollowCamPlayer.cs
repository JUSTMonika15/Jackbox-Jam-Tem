using UnityEngine;
using UnityEngine.InputSystem;

public class FollowCamPlayer : MonoBehaviour
{
    public CharacterController controller;
    public PlayerInput playerInput; 
    public float speed = 4f;
    public float gravity = -20f;


    private Vector3 playerMovement;
    private InputAction moveAction;
    private float verticalSpeed;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }
        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }
        moveAction = playerInput.actions.FindAction("Move", true);

    }

    // Update is called once per frame
    void Update()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        playerMovement = new Vector3(input.x, 0f, input.y);
        playerMovement *= speed;
        if (controller.isGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -2f;
        }
        verticalSpeed += gravity * Time.deltaTime;
        playerMovement.y = verticalSpeed;
        controller.Move(playerMovement * Time.deltaTime);
    }
}
