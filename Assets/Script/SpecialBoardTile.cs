using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SpecialBoardTile : MonoBehaviour
{
    [SerializeField] private BoardSpaceKind kind;
    private readonly HashSet<PlayerState> visitors = new HashSet<PlayerState>();
    private readonly Dictionary<PlayerState, int> speedStripLap = new Dictionary<PlayerState, int>();
    private readonly MovementBonusRules movementBonus = new MovementBonusRules();
    private BoxCollider trigger;
    private GameManager game;

    private void Awake()
    {
        trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
        game = FindAnyObjectByType<GameManager>();
    }
    public void Configure(BoardSpaceKind spaceKind) => kind = spaceKind;

    private void Update()
    {
        if (game == null) game = FindAnyObjectByType<GameManager>();
        if (game == null || !game.IsAuthoritative || trigger == null) return;
        foreach (PlayerState player in game.Players)
        {
            if (player == null) continue;
            bool standing = player.IsStandingOn(trigger);
            if (!standing)
            {
                visitors.Remove(player);
                continue;
            }
            if (visitors.Add(player) && player.CanAct) Activate(player);
        }
        visitors.RemoveWhere(player => player == null || !player.isActiveAndEnabled);
    }

    private void Activate(PlayerState player)
    {
        switch (kind)
        {
            case BoardSpaceKind.IncomeTax:
                int tax = Mathf.Min(20, Mathf.CeilToInt(player.Money * .2f));
                player.AddMoney(-tax);
                player.ShowFeedback("INCOME TAX\n-$" + tax, PlayerFeedbackKind.Negative);
                break;
            case BoardSpaceKind.LuxuryTax:
                int luxury = Mathf.Min(25, player.Money);
                player.AddMoney(-luxury);
                player.ShowFeedback("LUXURY TAX\n-$" + luxury, PlayerFeedbackKind.Negative);
                break;
            case BoardSpaceKind.MovementBonus:
                int reward = movementBonus.TryClaim(player, player.LapCount);
                if (reward <= 0) break;
                player.AddMoney(reward);
                player.ShowFeedback("MOTION BONUS\n+$" + reward, PlayerFeedbackKind.Positive);
                break;
            case BoardSpaceKind.SlowStrip:
                player.ApplyBoardEffect(BoardEventKind.SlowDown);
                break;
            case BoardSpaceKind.SpeedStrip:
                if (speedStripLap.TryGetValue(player, out int lap) && lap == player.LapCount) return;
                speedStripLap[player] = player.LapCount;
                player.ApplyBoardEffect(BoardEventKind.SpeedUp);
                player.ResetPushCooldownFromBoard();
                player.ShowFeedback("SPEED BOOST!\nFast for 5s — push refreshed",
                    PlayerFeedbackKind.SpeedUp);
                break;
            case BoardSpaceKind.GoToJail:
                player.ApplyBoardEffect(BoardEventKind.Jail);
                break;
        }
    }

    public void ForgetVisitor(PlayerState player) => visitors.Remove(player);
    public void ResetZone()
    {
        visitors.Clear();
        speedStripLap.Clear();
        movementBonus.Reset();
    }
    private void OnDisable() => visitors.Clear();
}
