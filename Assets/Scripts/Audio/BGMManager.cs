using UnityEngine;
using System.Collections;

public class BGMManager : MonoBehaviour
{
    [Header("Audio Source (BGM only)")]
    public AudioSource bgmSource;

    [Header("Clips")]
    public AudioClip titleBgm;
    public AudioClip gameBgm;
    public AudioClip gameOverBgm;

    [Header("Fade")]
    public float fadeTime = 0.4f;
    public float defaultVolume = 1.0f;

    Coroutine fadeRoutine;

    void Awake()
    {
        if (bgmSource == null)
            bgmSource = GetComponent<AudioSource>();

        if (bgmSource == null)
            Debug.LogError("[BGMManager] AudioSource が見つかりません（同じGameObjectに付けてください）");

        if (bgmSource != null)
        {
            bgmSource.loop = true;
            if (bgmSource.volume <= 0f) bgmSource.volume = defaultVolume;
        }
    }

    public void PlayTitle() => Play(titleBgm, true);
    public void PlayGame() => Play(gameBgm, true);
    public void PlayGameOver() => Play(gameOverBgm, true);

    public void StopBgm()
    {
        if (bgmSource == null) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOutAndStop());
    }

    void Play(AudioClip clip, bool loop)
    {
        if (bgmSource == null) return;
        if (clip == null)
        {
            return;
        }

        // 同じ曲を再指定した場合は何もしない
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeToClip(clip, loop));
    }

    IEnumerator FadeToClip(AudioClip newClip, bool loop)
    {
        // フェードアウト
        float startVol = bgmSource.volume;
        float t = 0f;
        float dur = Mathf.Max(0.0001f, fadeTime);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / dur);
            bgmSource.volume = Mathf.Lerp(startVol, 0f, a);
            yield return null;
        }

        bgmSource.volume = 0f;
        bgmSource.loop = loop;
        bgmSource.clip = newClip;
        bgmSource.Play();

        // フェードイン
        t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / dur);
            bgmSource.volume = Mathf.Lerp(0f, defaultVolume, a);
            yield return null;
        }

        bgmSource.volume = defaultVolume;
    }

    IEnumerator FadeOutAndStop()
    {
        float startVol = bgmSource.volume;
        float t = 0f;
        float dur = Mathf.Max(0.0001f, fadeTime);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / dur);
            bgmSource.volume = Mathf.Lerp(startVol, 0f, a);
            yield return null;
        }

        bgmSource.volume = 0f;
        bgmSource.Stop();
        bgmSource.clip = null;
    }
}
