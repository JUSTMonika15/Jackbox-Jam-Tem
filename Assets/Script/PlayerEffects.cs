using UnityEngine;

[RequireComponent(typeof(PlayerState), typeof(CharacterController))]
public class PlayerEffects : MonoBehaviour
{
    readonly PlayerEffectRules rules = new PlayerEffectRules();
    readonly WorkRules work = new WorkRules();
    PlayerState player;
    CharacterController controller;
    FollowCamPlayer movement;
    PlayerKnockback knockback;
    LapTracker laps;
    GameManager game;
    bool waitingForRelease;
    bool workQueued;
    public bool IsWorking => workQueued || work.IsWorking(Time.time);
    public float WorkRemaining => workQueued ? 3f : work.Remaining(Time.time);
    public bool IsJailed => rules.IsJailed(Time.time);
    public float JailRemaining => rules.JailRemaining(Time.time);
    public float SpeedMultiplier => rules.Speed(Time.time);
    public bool HasJailPass => rules.HasJailPass;
    public bool HasTollPass => rules.HasTollPass;
    void Awake()
    {
        player = GetComponent<PlayerState>();
        controller = GetComponent<CharacterController>();
        movement = GetComponent<FollowCamPlayer>();
        knockback = GetComponent<PlayerKnockback>();
        laps = GetComponent<LapTracker>();
        game = FindAnyObjectByType<GameManager>();
    }
    void Update()
    {
        if (!player.CanPlay) return;
        if (waitingForRelease && !IsJailed)
        {
            waitingForRelease = false;
            if (game != null) Teleport(game.JailExitPosition);
            player.ShowFeedback("RELEASED\n2s jail immunity", PlayerFeedbackKind.Positive);
        }
        // Birthday bills can reach jailed players: work starts after their release.
        if (workQueued && !IsJailed)
        {
            workQueued = false;
            BeginWork();
        }
        if (!IsJailed && work.Complete(Time.time))
        {
            player.AddMoney(10);
            player.ShowFeedback("WORK COMPLETE\n+$10 wages", PlayerFeedbackKind.Positive);
        }
    }
    public void RequestWork()
    {
        if (!player.CanPlay || IsWorking) return;
        if (IsJailed || waitingForRelease) { workQueued = true; return; }
        BeginWork();
    }
    void BeginWork()
    {
        if (!work.Start(Time.time)) return;
        if (movement != null) movement.ResetVerticalMotion();
        if (knockback != null) knockback.ResetKnockback();
        player.ShowFeedback("BANKRUPT\nWork for 3s, then earn $10", PlayerFeedbackKind.Negative);
    }
    public bool TryJail(float seconds = 5f)
    {
        if (!player.CanAct || game == null || !game.HasJail) return false;
        bool hadPass = HasJailPass;
        if (!rules.TryJail(Time.time, seconds))
        {
            if (hadPass && !HasJailPass)
                player.ShowFeedback("JAIL PASS USED\nYou stay free!", PlayerFeedbackKind.Pass);
            return false;
        }
        waitingForRelease = true;
        Teleport(game.JailPosition);
        player.ShowFeedback("JAILED!\nLocked up for " + seconds.ToString("0") + " seconds",
            PlayerFeedbackKind.Jail);
        return true;
    }
    public void GrantJailPass() { rules.GrantJailPass(); player.ShowFeedback("JAIL PASS\nOne free escape", PlayerFeedbackKind.Pass); }
    public void GrantTollPass() { rules.GrantTollPass(); player.ShowFeedback("TOLL PASS\nOne free toll", PlayerFeedbackKind.Pass); }
    public bool ConsumeTollPass() => rules.ConsumeTollPass();
    public void SetSpeed(float multiplier, float seconds)
    {
        rules.SetSpeed(multiplier, seconds, Time.time);
        player.ShowFeedback(multiplier > 1f ? "SPEED UP!\nFast for " + seconds + " seconds"
                : "SLOWED!\nSlow for " + seconds + " seconds",
            multiplier > 1f ? PlayerFeedbackKind.SpeedUp : PlayerFeedbackKind.SlowDown);
    }
    public void ReturnToStart()
    {
        player.ReturnToSpawnPoint();
        player.ShowFeedback("BACK TO START!\nNo free lap", PlayerFeedbackKind.Teleport);
    }
    public void Teleport(Vector3 position)
    {
        // Disable the capsule briefly so CharacterController accepts a transform teleport.
        bool enabledBefore = controller.enabled;
        controller.enabled = false;
        transform.position = position;
        if (movement != null) movement.ResetVerticalMotion();
        if (knockback != null) knockback.ResetKnockback();
        if (laps != null) laps.ResetCheckpointProgress();
        if (game != null) game.ForgetZoneContacts(player);
        controller.enabled = enabledBefore;
    }
    public void ResetEffects()
    {
        bool wasInJail = waitingForRelease;
        rules.Reset();
        work.Reset(); workQueued = false;
        waitingForRelease = false;
        if (wasInJail && game != null && game.HasJail) Teleport(game.JailExitPosition);
        if (movement != null) movement.ResetVerticalMotion();
    }
}
