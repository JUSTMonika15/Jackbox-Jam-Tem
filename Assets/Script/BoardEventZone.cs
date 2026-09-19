using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class BoardEventZone : MonoBehaviour
{
    [SerializeField] bool fortune = true;
    readonly HashSet<PlayerState> visitors = new HashSet<PlayerState>();
    readonly BoardEventRules rules = new BoardEventRules();
    GameManager game;
    BoxCollider trigger;
    void Awake()
    {
        trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
        game = FindAnyObjectByType<GameManager>();
    }
    void Update()
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
            if (visitors.Add(player) && player.CanAct) Draw(player);
        }
        visitors.RemoveWhere(player => player == null || !player.isActiveAndEnabled);
    }
    public BoardEventKind Draw(PlayerState player)
    {
        BoardEventKind kind = rules.ResolveWeighted(fortune,Random.Range(0,20));
        if (player == null || !player.CanAct) return kind;
        ApplyEvent(player, kind);
        return kind;
    }
    public void ApplyEvent(PlayerState player, BoardEventKind kind)
    {
        if (player == null || !player.CanAct || game == null || !game.IsAuthoritative) return;
        PlayerEffects effects = player.GetComponent<PlayerEffects>();
        // Event payments use available cash only; only rent can liquidate land or trigger work.
        if (kind <= BoardEventKind.Dividend)
        {
            List<EconomyAccount> accounts = new List<EconomyAccount>();
            Dictionary<PlayerState, int> moneyBefore = new Dictionary<PlayerState, int>();
            int owned = 0;
            if (game != null)
                foreach (PlayerState peer in game.Players)
                    if (peer != null)
                    {
                        accounts.Add(peer.Account);
                        moneyBefore[peer] = peer.Money;
                    }
            if (kind == BoardEventKind.Dividend)
                foreach (PropertyZone land in FindObjectsByType<PropertyZone>())
                    if (land.Owner == player) owned++;
            int changed = rules.ApplyMoney(kind, player.Account, accounts, owned);
            if (game != null)
                foreach (PlayerState peer in game.Players)
                    if (peer != null)
                    {
                        peer.NotifyStateChanged();
                        if (kind == BoardEventKind.Birthday && peer != player
                            && moneyBefore.TryGetValue(peer, out int before) && peer.Money < before)
                            peer.ShowFeedback("BIRTHDAY GIFT\n-$" + (before-peer.Money) + " to "
                                + player.DisplayName, PlayerFeedbackKind.Negative);
                    }
            player.ShowFeedback((fortune ? "FORTUNE" : "CHANCE") + "\n" + kind + ": "
                    + (changed >= 0 ? "+$" : "-$") + System.Math.Abs((long)changed),
                changed >= 0 ? PlayerFeedbackKind.Positive : PlayerFeedbackKind.Negative);
            return;
        }
        player.ApplyBoardEffect(kind);
        player.ShowFeedback((fortune ? "FORTUNE" : "CHANCE") + "\n" + EventText(kind), FeedbackKind(kind));
    }
    private static PlayerFeedbackKind FeedbackKind(BoardEventKind kind)
    {
        switch (kind)
        {
            case BoardEventKind.JailPass:
            case BoardEventKind.TollPass: return PlayerFeedbackKind.Pass;
            case BoardEventKind.ReturnStart: return PlayerFeedbackKind.Teleport;
            case BoardEventKind.Jail: return PlayerFeedbackKind.Jail;
            case BoardEventKind.SpeedUp: return PlayerFeedbackKind.SpeedUp;
            case BoardEventKind.SlowDown: return PlayerFeedbackKind.SlowDown;
            default: return PlayerFeedbackKind.Neutral;
        }
    }
    private static string EventText(BoardEventKind kind)
    {
        switch (kind)
        {
            case BoardEventKind.JailPass: return "JAIL PASS ACQUIRED";
            case BoardEventKind.ReturnStart: return "RETURNED TO START";
            case BoardEventKind.Jail: return "JAILED FOR 5 SECONDS";
            case BoardEventKind.SpeedUp: return "SPEED UP FOR 5 SECONDS";
            case BoardEventKind.SlowDown: return "SLOWED FOR 3 SECONDS";
            case BoardEventKind.TollPass: return "FREE TOLL PASS ACQUIRED";
            default: return kind.ToString().ToUpperInvariant();
        }
    }
    public void ForgetVisitor(PlayerState player) { visitors.Remove(player); }
    public void ResetZone() { visitors.Clear(); }
    public void Configure(bool isFortune) { fortune = isFortune; }
    void OnDisable() { visitors.Clear(); }
}
