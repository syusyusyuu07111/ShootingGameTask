using UnityEngine;
using System.Collections;

/// <summary>
/// BGM専用の再生管理
/// ・タイトル / ゲーム / ゲームオーバーのBGM切り替え
/// ・曲切り替えはフェードアウト → 曲差し替え → フェードイン
/// ・Stopはフェードアウトして停止
///
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

    // フェード処理用コルーチン管理
    // ・同時に複数のフェードが走らないようにする
    // ・「いまフェード中かどうか」を fadeRoutine が null かどうかで判定できる
    //   （null = 何も走ってない / nullじゃない = 何かフェード処理が走ってる）
    Coroutine fadeRoutine;

    void Awake()
    {
        // AudioSource取得（Inspector未設定でも動くようにする）
        if (bgmSource == null)
            bgmSource = GetComponent<AudioSource>(); //

        // bgmSourceが無いと以降の再生が全部できないので、早めにエラーだして気づけるようにする
        if (bgmSource == null)
            Debug.LogError("[BGMManager] AudioSource が見つかりません（同じGameObjectに付けてください）");

        if (bgmSource != null)
        {
            bgmSource.loop = true; // 【挙動】BGMは基本ループ（曲ごとに変えたければPlay側で上書き）

            // volume=0 のままだと「鳴っているのに聞こえない」事故になるので保険
            if (bgmSource.volume <= 0f)
                bgmSource.volume = defaultVolume;
        }
    }

    //=========================================================================

    // 状態ごとのBGM再生
    public void PlayTitle() => Play(titleBgm, true);
    public void PlayGame() => Play(gameBgm, true);
    public void PlayGameOver() => Play(gameOverBgm, true);

    /// <summary>
    /// BGMをフェードアウトして停止する
    /// </summary>
    public void StopBgm()
    {
        if (bgmSource == null) return;

        // フェード中にさらにStopが来た時：
        // 前のコルーチンが音量を触り続けると「新しい処理」と競合して挙動がぐちゃぐちゃになる
        // なので StopCoroutine で「音量を動かしてる処理」を止める
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOutAndStop());
    }

    /// <summary>
    /// フェードなしで即停止する
    /// </summary>
    public void StopBgmImmediate()
    {
        if (bgmSource == null) return;

        // フェード途中なら止める（残った処理が音量を戻したりしないように）
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        // Stop＞再生停止　clipをnullにしてなにも再生しないようにする
        bgmSource.Stop();
        bgmSource.clip = null;

        // 次回再生時の事故防止：
        // ・フェード途中は volume が 0 付近になっているので、そのままだと次の曲が無音になる
        // ・pitchも他で弄られている可能性があるので標準値へ戻す
        bgmSource.volume = defaultVolume;
        bgmSource.pitch = 1f;

        // 状態管理フェード中じゃない状態に戻す
        fadeRoutine = null;
    }

    /// <summary>
    /// 指定クリップへフェード付きで切り替える
    /// </summary>
    void Play(AudioClip clip, bool loop)
    {
        if (bgmSource == null) return;

        // 未設定クリップは何もしない（安全策）
        if (clip == null) return;

        // 同じ曲が鳴ってるなら切り替え不要
        // フェードし直すと「一瞬音が落ちる」ので違和感になる
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        // 進行中のフェードは止めて、最新の命令を優先　あとの命令
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        // 【ここでやってること】
        // ・フェードアウト→差し替え→フェードインを「時間経過」でやるため、コルーチンに渡す
        fadeRoutine = StartCoroutine(FadeToClip(clip, loop));
    }

    //=========================================================================

    /// <summary>
    /// フェードしながら曲を切り替える本体
    ///
    /// 【コルーチンのポイント】
    /// ・IEnumerator の中で yield return すると処理が一旦止まり、次のフレーム以降に再開される
    /// ・つまり while の中で yield return null を入れると「毎フレームちょっとずつ」処理できる
    /// </summary>
    IEnumerator FadeToClip(AudioClip newClip, bool loop)
    {
        // フェードに使う時間（0対策）
        // 【挙動】fadeTime=0 だと dur=0 になって t/dur が割り算事故になるので最小値を入れる
        float dur = Mathf.Max(0.0001f, fadeTime);

        // =====================================================
        // ① フェードアウト（現在の音量 → 0）
        // =====================================================
        float startVol = bgmSource.volume;
        float t = 0f;

        // 【while】条件が満たされる間繰り返す
        while (t < dur)
        {
            // 【Time.unscaledDeltaTime】
            // ・前フレームからの経過秒
            // ・timeScale=0(ポーズ)でも進む
            t += Time.unscaledDeltaTime;

            // 0〜1 に正規化した進行度（0=開始、1=完了）
            // 【Clamp01】0未満/1超えを防ぐ（最終フレームで少しはみ出す事がある）
            float rate = Mathf.Clamp01(t / dur);

            // 【Mathf.Lerp(A,B,rate)】
            // ・rate=0 → A
            // ・rate=1 → B
            // ・途中は線形でなめらかに変化
            // ここでは「startVol → 0」へ向かって音量を少しずつ下げる
            bgmSource.volume = Mathf.Lerp(startVol, 0f, rate);

            // 【yield return null】
            // ・この行で一旦処理が止まる
            // ・次のフレームで while の続きから再開される
            // ＝「フレームごとに音量が変化する」＝フェードになる
            yield return null;
        }

        // 念のため完全に0に固定
        bgmSource.volume = 0f;

        // =====================================================
        // ② 曲の差し替えかえ
        // =====================================================
        // 音量0で差し替える
        // ・差し替え瞬間に前の曲が一瞬聞こえる／プチッとなるのを避ける
        // ・無音の状態で差し替える＞自然な切り替えにする
        bgmSource.loop = loop;
        bgmSource.clip = newClip; // 曲が変わる
        bgmSource.Play();         // 新しい曲の再生開始

        // =====================================================
        // ③ フェードイン（0 → defaultVolume）
        // =====================================================
        // t をリセットして「もう一回0から進行度を作り直す」
        t = 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;

            float rate = Mathf.Clamp01(t / dur);

            // ここでは「0 → defaultVolume」へ音量を少しずつ上げる
            bgmSource.volume = Mathf.Lerp(0f, defaultVolume, rate);

            // 次のフレームへ（＝フェードインが進む）
            yield return null;
        }

        // 音量
        bgmSource.volume = defaultVolume;

        // フェード完了（待機状態に戻す）
        fadeRoutine = null;
    }

    //=========================================================================

    /// <summary>
    /// フェードアウトして停止する処理
    /// ・曲の差し替えはしない
    /// ・音量だけ0にしてから Stop() する
    /// </summary>
    IEnumerator FadeOutAndStop()
    {
        float dur = Mathf.Max(0.0001f, fadeTime);

        float startVol = bgmSource.volume;
        float t = 0f;

        // フェードアウトのみ（曲は差し替えない）
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;

            float rate = Mathf.Clamp01(t / dur);

            // startVol → 0 に向けて徐々に下げる（FadeToClipのフェードアウトと同じ仕組み）
            bgmSource.volume = Mathf.Lerp(startVol, 0f, rate);

            // 1フレーム待つ＝次のフレームでまた少し下げる＝フェードになる
            yield return null;
        }

        // 無音を保証
        bgmSource.volume = 0f;

        // 停止処理
        // 【Stop】再生を止めるだけ（volumeはそのままなので、上で0にしてから止めている）
        bgmSource.Stop();

        // clipをnullにすることで「何も再生していない状態」を明確化
        // （あとで bgmSource.clip == clip の判定にも影響してくる）
        bgmSource.clip = null;

        // 状態リセット
        fadeRoutine = null;
    }
}
