using UnityEngine;
using Unity.Cinemachine;

// Scene-owned presentation only. Player prefab/ownership/network settings remain unchanged.
[RequireComponent(typeof(GameManager))]
public class BoardCamera : MonoBehaviour
{
    readonly BoardPresentationRules presentation = new BoardPresentationRules();
    Camera view;
    CinemachineBrain brain;
    bool brainWasEnabled;
    Vector3 oldPosition;
    Quaternion oldRotation;
    Rect oldRect;
    bool oldOrthographic;
    float oldSize;
    bool oldFog;
    public Camera View => view;
    void Start()
    {
        view = Camera.main;
        if (view == null) { Debug.LogWarning("BoardCamera needs the scene Main Camera.",this); return; }
        oldPosition = view.transform.position; oldRotation = view.transform.rotation;
        oldRect = view.rect; oldOrthographic = view.orthographic; oldSize = view.orthographicSize;
        oldFog = RenderSettings.fog;
        RenderSettings.fog = false; // High board camera must not wash out the small tiles/player.
        brain = view.GetComponent<CinemachineBrain>();
        if (brain != null) { brainWasEnabled = brain.enabled; brain.enabled = false; }
        view.transform.position = new Vector3(0f,38f,0f);
        view.transform.rotation = Quaternion.Euler(90f,0f,0f);
        view.orthographic = true;
        // Render behind the HUD instead of shrinking the board into the remaining strip.
        view.rect = new Rect(0f,presentation.CameraViewportBottom,1f,presentation.CameraViewportHeight);
        Fit();
    }
    void LateUpdate() { if (view != null) Fit(); }
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
        RenderSettings.fog = oldFog;
        if (brain != null) brain.enabled = brainWasEnabled;
    }
}
