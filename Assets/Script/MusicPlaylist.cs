using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MusicPlaylist : MonoBehaviour
{
    [SerializeField] AudioClip[] tracks;
    [SerializeField, Range(0f, 1f)] float volume = .35f;
    [SerializeField, Min(0f)] float fadeSeconds = 1f;

    AudioSource source;
    MusicPlaylistRules order;
    Coroutine playback;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
    }

    void Start()
    {
        order = new MusicPlaylistRules(tracks == null ? 0 : tracks.Length);
        if (TrySelectPlayableTrack()) playback = StartCoroutine(PlayContinuously());
    }

    IEnumerator PlayContinuously()
    {
        while (enabled && source.clip != null)
        {
            source.volume = 0f;
            source.Play();
            yield return FadeTo(volume);

            float fadeAt = Mathf.Max(0f,source.clip.length-fadeSeconds);
            while (source.isPlaying && source.time < fadeAt) yield return null;
            yield return FadeTo(0f);
            source.Stop();

            order.Advance();
            if (!TrySelectPlayableTrack()) yield break;
        }
    }

    IEnumerator FadeTo(float target)
    {
        if (fadeSeconds <= 0f)
        {
            source.volume = target;
            yield break;
        }

        float start = source.volume;
        float elapsed = 0f;
        while (elapsed < fadeSeconds && source.isPlaying)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(start,target,Mathf.Clamp01(elapsed/fadeSeconds));
            yield return null;
        }
        source.volume = target;
    }

    bool TrySelectPlayableTrack()
    {
        if (tracks == null || tracks.Length == 0 || order == null) return false;
        for (int checkedTracks = 0; checkedTracks < tracks.Length; checkedTracks++)
        {
            AudioClip candidate = tracks[order.CurrentIndex];
            if (candidate != null)
            {
                source.clip = candidate;
                return true;
            }
            order.Advance();
        }
        source.clip = null;
        return false;
    }

    void OnDisable()
    {
        if (playback != null) StopCoroutine(playback);
        playback = null;
        if (source != null) source.Stop();
    }

    void OnValidate()
    {
        volume = Mathf.Clamp01(volume);
        fadeSeconds = Mathf.Max(0f,fadeSeconds);
    }
}
