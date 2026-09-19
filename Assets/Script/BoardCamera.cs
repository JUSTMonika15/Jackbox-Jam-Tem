using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

// Scene-owned presentation only. Player prefab/ownership/network settings remain unchanged.
[RequireComponent(typeof(GameManager))]
public class BoardCamera : MonoBehaviour
{
    readonly BoardPresentationRules presentation = new BoardPresentationRules();
    readonly CameraViewRules modeRules = new CameraViewRules();
    [SerializeField, Min(1f)] float shoulderDistance = 5f;
    [SerializeField, Min(1f)] float shoulderHeight = 5f;
    [SerializeField, Range(35f, 80f)] float shoulderFieldOfView = 58f;
    [SerializeField, Min(.1f)] float shoulderSmoothing = 9f;
    Camera view;
    CinemachineBrain brain;
    GameManager game;
    PlayerState localPlayer;
    FollowCamPlayer localMovement;
    CameraViewMode preferredMode;
    CameraViewMode appliedMode = (CameraViewMode)(-1);
    bool brainWasEnabled;
    Vector3 oldPosition;
    Quaternion oldRotation;
    Rect oldRect;
    bool oldOrthographic;
    float oldSize;
    float oldFieldOfView;
    bool oldFog;
    public Camera View => view;
    public CameraViewMode PreferredMode => preferredMode;
    public string ModeButtonText => modeRules.ButtonText(preferredMode);
    void Start()
    {
        game = GetComponent<GameManager>();
        preferredMode = modeRules.Initial();
        view = Camera.main;
        if (view == null) { Debug.LogWarning("BoardCamera needs the scene Main Camera.",this); return; }
        oldPosition = view.transform.position; oldRotation = view.transform.rotation;
        oldRect = view.rect; oldOrthographic = view.orthographic; oldSize = view.orthographicSize;
        oldFieldOfView = view.fieldOfView;
        oldFog = RenderSettings.fog;
        RenderSettings.fog = false; // High board camera must not wash out the small tiles/player.
        brain = view.GetComponent<CinemachineBrain>();
        if (brain != null) { brainWasEnabled = brain.enabled; brain.enabled = false; }
        // Render behind the HUD instead of shrinking the board into the remaining strip.
        view.rect = new Rect(0f,presentation.CameraViewportBottom,1f,presentation.CameraViewportHeight);
        ApplyOverview(true);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) ToggleMode();
    }

    void LateUpdate()
    {
        if (view == null) return;
        RefreshLocalPlayer();
        CameraViewMode effective = modeRules.Effective(preferredMode, localPlayer != null);
        bool modeChanged = effective != appliedMode;
        if (effective == CameraViewMode.Shoulder) ApplyShoulder(modeChanged);
        else ApplyOverview(modeChanged);
        appliedMode = effective;
    }

    public void ToggleMode()
    {
        preferredMode = modeRules.Toggle(preferredMode);
    }

    void RefreshLocalPlayer()
    {
        if (localPlayer != null && localPlayer.IsLocalPlayer) return;
        localPlayer = game == null ? null : game.GetLocalPlayer();
        localMovement = localPlayer == null ? null : localPlayer.GetComponent<FollowCamPlayer>();
    }

    void ApplyOverview(bool snap)
    {
        view.orthographic = true;
        view.transform.position = new Vector3(0f,38f,0f);
        view.transform.rotation = Quaternion.Euler(90f,0f,0f);
        Fit();
    }

    void ApplyShoulder(bool snap)
    {
        if (localPlayer == null) { ApplyOverview(true); return; }
        Vector3 forward = localMovement == null ? Vector3.forward : localMovement.FacingDirection;
        forward.y = 0f;
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 lookTarget = localPlayer.transform.position + Vector3.up * .9f;
        Vector3 desiredPosition = lookTarget - forward * shoulderDistance + Vector3.up * shoulderHeight;
        Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - desiredPosition, Vector3.up);
        view.orthographic = false;
        view.fieldOfView = shoulderFieldOfView;
        if (snap)
        {
            view.transform.SetPositionAndRotation(desiredPosition, desiredRotation);
            return;
        }
        float blend = 1f - Mathf.Exp(-shoulderSmoothing * Time.unscaledDeltaTime);
        view.transform.position = Vector3.Lerp(view.transform.position, desiredPosition, blend);
        view.transform.rotation = Quaternion.Slerp(view.transform.rotation, desiredRotation, blend);
    }
    void Fit()
    {
        float widthLimitedSize = 12.5f / Mathf.Max(.1f,view.aspect);
        view.orthographicSize = Mathf.Max(presentation.CameraHalfHeight,widthLimitedSize);
    }
    void OnDisable()
    {
        if (view == null) return;
        view.transform.SetPositionAndRotation(oldPosition,oldRotation);
        view.rect = oldRect; view.orthographic = oldOrthographic; view.orthographicSize = oldSize;
        view.fieldOfView = oldFieldOfView;
        RenderSettings.fog = oldFog;
        if (brain != null) brain.enabled = brainWasEnabled;
    }
}
