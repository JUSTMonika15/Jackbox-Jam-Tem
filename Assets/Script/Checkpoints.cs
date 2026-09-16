using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class Checkpoints : MonoBehaviour
{
    public int index;
    private readonly HashSet<PlayerState> visitors = new HashSet<PlayerState>();
    private BoxCollider trigger;
    private GameManager game;

    private void Awake()
    {
        trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
        game = FindAnyObjectByType<GameManager>();
    }

    private void Update()
    {
        if (game == null) game = FindAnyObjectByType<GameManager>();
        if (game == null || !game.IsAuthoritative || trigger == null) return;

        foreach (PlayerState player in game.Players)
        {
            if (player == null) continue;
            bool inside = player.CanAct && player.IsInside(trigger.bounds);
            if (!inside)
            {
                visitors.Remove(player);
                continue;
            }
            if (!visitors.Add(player)) continue;
            LapTracker lapTracker = player.GetComponent<LapTracker>();
            if (lapTracker != null) lapTracker.passCheckpoint(index);
        }
        visitors.RemoveWhere(player => player == null || !player.isActiveAndEnabled);
    }

    private void OnDisable() => visitors.Clear();
}
