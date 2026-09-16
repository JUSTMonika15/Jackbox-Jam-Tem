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
            player.ShowMessage("Released! 2s jail immunity");
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
            player.ShowMessage("Work complete! +$10 wages");
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
        player.ShowMessage("Bankrupt! Work here for 3s, then earn $10");
    }
    public bool TryJail(float seconds = 5f)
    {
        if (!player.CanAct || game == null || !game.HasJail) return false;
        bool hadPass = HasJailPass;
        if (!rules.TryJail(Time.time, seconds))
        {
            if (hadPass && !HasJailPass) player.ShowMessage("Jail pass used — free!");
            return false;
        }
        waitingForRelease = true;
        Teleport(game.JailPosition);
        player.ShowMessage("Jailed for " + seconds.ToString("0") + "s!");
        return true;
    }
    public void GrantJailPass() { rules.GrantJailPass(); player.ShowMessage("Jail pass acquired (one use)"); }
    public void GrantTollPass() { rules.GrantTollPass(); player.ShowMessage("Free toll pass acquired (one use)"); }
    public bool ConsumeTollPass() => rules.ConsumeTollPass();
    public void SetSpeed(float multiplier, float seconds)
    {
        rules.SetSpeed(multiplier, seconds, Time.time);
        player.ShowMessage((multiplier > 1f ? "Tailwind! " : "Traffic! ") + seconds + "s");
    }
    public void ReturnToStart()
    {
        if (game == null || !game.HasRaceStart) { player.ShowMessage("Race start is not configured"); return; }
        Teleport(game.RaceStartPosition);
        player.ShowMessage("Back to start — no free lap");
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
