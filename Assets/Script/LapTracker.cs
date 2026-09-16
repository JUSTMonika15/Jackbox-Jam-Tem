using UnityEngine;

public class LapTracker : MonoBehaviour
{
    private int lapCount;
    private int nextCheckpoint = 1;
    private int totalCheckpoints = 4;
    private PlayerState player;
    public int LapCount => lapCount;
    public int NextCheckpoint => nextCheckpoint;
    private void Awake() { player = GetComponent<PlayerState>(); }
    public void ResetLaps() { lapCount = 0; nextCheckpoint = 1; }
    public void ResetCheckpointProgress() { nextCheckpoint = 1; }
    public void ApplyNetworkState(int laps, int checkpoint)
    {
        lapCount = Mathf.Max(0, laps);
        nextCheckpoint = Mathf.Clamp(checkpoint, 0, totalCheckpoints - 1);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void passCheckpoint(int checkpointIndex)
    {
        if (player == null) player = GetComponent<PlayerState>();
        if (player != null && (!player.CanAct || !player.IsStateAuthority)) return;
        if (checkpointIndex != nextCheckpoint)
        {
            Debug.Log("You need to pass the previous checkpoint first!");
        }
        else if(checkpointIndex == 0)
        {
            lapCount += 1;
            nextCheckpoint = 1; 
            if (player != null) player.OnLapCompleted();
            Debug.Log("The current lap: "+ lapCount);
        }
        else if (checkpointIndex == totalCheckpoints - 1)
        {
            nextCheckpoint = 0;
        }
        else
        {
            nextCheckpoint += 1;
        }

        if (player != null) player.NotifyStateChanged();

    }
}
