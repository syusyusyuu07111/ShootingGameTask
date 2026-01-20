using UnityEngine;
using System.Collections;

/// <summary>
/// BGM専用の再生管理
/// ・タイトル / ゲーム / ゲームオーバーのBGM切り替え
/// ・曲切り替えはフェードアウト→差し替え→フェードイン
/// ・Stopはフェードアウトして停止（即時停止も別途用意）
///
/// ※SEとは分離して「BGMだけ」を一つのAudioSourceで回す
/// </summary>
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

    // フェードするためのコルーチンを同時に走らせないための管理
    // ・Play/Stop連打でも、複数の処理が走らずに最後の命令を優先するようにする
    // ・StopCoroutine対象を保持することで「いまどのフェードが動いてるか」を明確にする
    Coroutine fadeRoutine;

    void Awake()
    {
        //Audio Source取得する===========================================
        // ・Inspector未設定でも動くようにGetComponentで拾う
        // ・BGM専用なので「同一GameObjectにAudioSourceがある」前提
        if (bgmSource == null)
            bgmSource = GetComponent<AudioSource>();

        // ここがnullのままだと以降は全部動かせないので、早めに気づけるようエラーを出す
        if (bgmSource == null)
            Debug.LogError("[BGMManager] AudioSource が見つかりません（同じGameObjectに付けてください）");

        if (bgmSource != null)
        {
            // BGMは基本ループ再生（曲ごとに変えたい場合はPlay側で指定）
            bgmSource.loop = true;

            // 0のままだと無音事故になるので、未設定っぽい値なら既定音量に寄せる
            // ※AudioSourceの初期値が0で保存されてると「鳴ってるのに聞こえない」が起きがち
            if (bgmSource.volume <= 0f) bgmSource.volume = defaultVolume;
        }
    }
    //=========================================================================

    // 外部から呼ぶためのメソッド
    // ・呼び出し側が「今どの状態にしたいか」を明確に書ける（Play(gameBgm)より意図が伝わる）
    public void PlayTitle() => Play(titleBgm, true);
    public void PlayGame() => Play(gameBgm, true);
    public void PlayGameOver() => Play(gameOverBgm, true);

    /// <summary>
    /// BGMをフェードアウトして停止する
    /// 「停止」演出を入れたいときに使う（遷移・リザルト等）
    /// </summary>
    public void StopBgm()
    {
        if (bgmSource == null) return;

        // フェード中なら止めて「最後に呼ばれたStop」が勝つようにする
        // ・例えば曲切り替え中にStopが来ても、確実に止めたい
        // ・逆にStop中にPlayが来たら、Play側が上書きする（最後の命令優先）
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);

        // 「止める」も演出なのでフェードアウトしてから停止する
        fadeRoutine = StartCoroutine(FadeOutAndStop());
    }

    /// <summary>
    /// フェードなしで即停止する
    /// リトライ即時切替などのアニメーションを入れないときに使う
    /// </summary>
    public void StopBgmImmediate()
    {
        if (bgmSource == null) return;

        // フェード途中で即停止した場合も、別コルーチンが音量を触り続けないよう止める
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        // 即停止：音を確実に止めたい（演出不要／ローディング入る等）
        bgmSource.Stop();
        bgmSource.clip = null;

        // 次の再生のために既定値へ戻しておく
        // ・フェード中はvolumeが0付近になっているので、そのままだと次の曲が無音で始まる
        // ・pitchも他で弄られる可能性があるので、標準に戻して事故を減らす
        bgmSource.volume = defaultVolume;
        bgmSource.pitch = 1f;

        // いまフェードは走っていない
        fadeRoutine = null;
    }

    /// <summary>
    /// 指定クリップへフェードで切り替える
    /// </summary>
    void Play(AudioClip clip, bool loop)
    {
        if (bgmSource == null) return;

        // 未設定クリップは何も動作しないようにする
        // ・未実装のBGMがある段階でも、呼び出し側は安全に書ける
        // ・ここでエラーを出したい場合は好みで Debug.LogWarning を入れてもOK
        if (clip == null)
        {
            return;
        }

        // 同じ曲を再指定した場合は何もしない
        // ・同じ曲でフェードし直すと「一瞬音が落ちる」違和感が出るため
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        // 切り替え命令が来たら、進行中フェードは中断してあとに来たのを優先する
        // ・フェード中にさらにPlayが来ても、音量計算が競合しないようにする
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeToClip(clip, loop));
    }

    IEnumerator FadeToClip(AudioClip newClip, bool loop)
    {
        // fadeTimeが0でも破綻しないよう、最小値を噛ませる
        // ・0だと除算が怖い＆whileが即抜けて「意図しない瞬間切り替え」になりやすい
        float dur = Mathf.Max(0.0001f, fadeTime);

        // -----------------
        // フェードアウト
        // -----------------
        float startVol = bgmSource.volume;
        float t = 0f;

        // Time.unscaledDeltaTime を使う：
        // ・ポーズ中(Time.timeScale=0)でもBGMだけは自然に切り替えたい
        // ・リザルト/ゲームオーバー演出などでtimeScaleを止めてもBGMは演出として動かせる
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / dur);
            bgmSource.volume = Mathf.Lerp(startVol, 0f, a);
            yield return null;
        }

        // -----------------
        // クリップ差し替え
        // -----------------
        // いったん0に固定してから差し替え＆再生：
        // ・曲の切り替えタイミングで「一瞬だけ前の音が残る」などの事故を避ける
        bgmSource.volume = 0f;
        bgmSource.loop = loop;
        bgmSource.clip = newClip;
        bgmSource.Play();

        // -----------------
        // フェードイン
        // -----------------
        t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / dur);
            bgmSource.volume = Mathf.Lerp(0f, defaultVolume, a);
            yield return null;
        }

        // 最終的に既定音量へ（途中で丸め誤差が出るので最後に固定）
        bgmSource.volume = defaultVolume;

        // フェード完了（今は何も走ってない状態に戻す）
        // ・次のPlay/Stopで「動いてるか？」判断ができるようにする
        // ・StopCoroutine対象として残り続けるのを防ぐ（読みやすさのため）
        fadeRoutine = null;
    }

    IEnumerator FadeOutAndStop()
    {
        // Stop用フェードアウト：
        // ・曲切り替えのFadeToClipと同じ考え方で「演出として自然に音を消す」
        // ・timeScaleが止まっている状況でも動くよう unscaledDeltaTime を使う
        float dur = Mathf.Max(0.0001f, fadeTime);

        float startVol = bgmSource.volume;
        float t = 0f;

        // -----------------
        // フェードアウト（音量を0へ）
        // -----------------
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / dur);
            bgmSource.volume = Mathf.Lerp(startVol, 0f, a);
            yield return null;
        }

        // ここで0に固定してから停止する：
        // ・フェード中に別処理が入っても「最後は確実に無音」になる
        bgmSource.volume = 0f;

        // -----------------
        // 停止処理
        // -----------------
        // Stop：再生そのものを止める
        bgmSource.Stop();

        // clipをnullにする：
        // ・「いま何も再生していない状態」を明確にする
        // ・次のPlayで「同じ曲指定時の早期return」判定にも影響する（意図的に“何も鳴ってない”にする）
        bgmSource.clip = null;

        // フェード完了（参照を消して状態が読みやすいように）
        // ・fadeRoutine != null かどうかで「いまフェード中？」が分かる
        fadeRoutine = null;
    }
}
