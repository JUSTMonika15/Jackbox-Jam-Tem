using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class PlayerSfx : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float jumpVolume = .42f;
    [SerializeField, Range(0f, 1f)] private float pushVolume = .5f;
    [SerializeField, Range(0f, 1f)] private float feedbackVolume = .48f;

    private AudioSource source;
    private AudioClip jumpClip;
    private AudioClip pushClip;
    private AudioClip rewardClip;
    private AudioClip penaltyClip;
    private AudioClip speedUpClip;
    private AudioClip slowDownClip;
    private AudioClip teleportClip;
    private AudioClip jailClip;
    private PlayerState player;

    private void Awake()
    {
        player = GetComponent<PlayerState>();
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        jumpClip = Resources.Load<AudioClip>(PlayerSfxRules.JumpResourcePath);
        pushClip = Resources.Load<AudioClip>(PlayerSfxRules.PushResourcePath);
    }

    public void PlayJump() => PlayLocal(jumpClip, jumpVolume);
    public void PlayPush() => PlayLocal(pushClip, pushVolume);

    public void PlayFeedback(PlayerFeedbackKind kind)
    {
        if (player != null && !player.IsLocalPlayer) return;
        EnsureFeedbackClips();
        switch (new PlayerFeedbackRules().SoundFor(kind))
        {
            case PlayerFeedbackSound.Reward: PlayLocal(rewardClip, feedbackVolume); break;
            case PlayerFeedbackSound.Penalty: PlayLocal(penaltyClip, feedbackVolume); break;
            case PlayerFeedbackSound.SpeedUp: PlayLocal(speedUpClip, feedbackVolume); break;
            case PlayerFeedbackSound.SlowDown: PlayLocal(slowDownClip, feedbackVolume); break;
            case PlayerFeedbackSound.Teleport: PlayLocal(teleportClip, feedbackVolume); break;
            case PlayerFeedbackSound.Jail: PlayLocal(jailClip, feedbackVolume); break;
        }
    }

    private void EnsureFeedbackClips()
    {
        if (rewardClip != null) return;
        rewardClip = CreateArcadeClip("Reward", new[] { 660f, 880f }, .1f, .25f);
        penaltyClip = CreateArcadeClip("Penalty", new[] { 330f, 220f }, .13f, .28f);
        speedUpClip = CreateArcadeClip("SpeedUp", new[] { 440f, 660f, 880f }, .075f, .22f);
        slowDownClip = CreateArcadeClip("SlowDown", new[] { 440f, 320f, 210f }, .11f, .24f);
        teleportClip = CreateArcadeClip("Teleport", new[] { 280f, 420f, 620f, 920f }, .07f, .2f);
        jailClip = CreateArcadeClip("Jail", new[] { 190f, 145f, 190f }, .15f, .36f);
    }

    private static AudioClip CreateArcadeClip(string clipName, float[] frequencies,
        float secondsPerTone, float harmonic)
    {
        const int sampleRate = 22050;
        int samplesPerTone = Mathf.Max(1, Mathf.RoundToInt(sampleRate * secondsPerTone));
        float[] samples = new float[samplesPerTone * frequencies.Length];
        for (int index = 0; index < samples.Length; index++)
        {
            int tone = Mathf.Min(frequencies.Length - 1, index / samplesPerTone);
            int withinTone = index % samplesPerTone;
            float progress = withinTone / (float)samplesPerTone;
            float envelope = Mathf.Sin(progress * Mathf.PI);
            float time = withinTone / (float)sampleRate;
            float frequency = frequencies[tone];
            samples[index] = Mathf.Clamp(envelope * (.72f * Mathf.Sin(2f * Mathf.PI * frequency * time)
                + harmonic * Mathf.Sin(2f * Mathf.PI * frequency * 2f * time)), -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("MonoRush_" + clipName, samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void PlayLocal(AudioClip clip, float volume)
    {
        if (clip == null || source == null) return;
        if (player != null && !player.IsLocalPlayer) return;
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
