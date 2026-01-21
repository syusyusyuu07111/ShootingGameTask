using UnityEngine;
using System.Collections;

public class BGM_Manager : MonoBehaviour
{
    [Header("Audio Source (BGM）")]
    [SerializeField] private AudioSource BgmSource;

    [Header("Clips")]
    [SerializeField] private AudioClip TitleBgm;
    [SerializeField] private AudioClip GameBgm;
    [SerializeField] private AudioClip GameOverBgm;

    [Header("Fade")]
    [SerializeField] private float FadeTime = 0.4f;
    [SerializeField] private float DefaultVolume = 1.0f;

    /* フェード処理用コルーチン管理
     ・同時に複数のフェードが走らないようにする
     ・「いまフェード中かどうか」を fadeRoutine が null かどうかで判定できる
       （null = 何も走ってない / nullじゃない = 何かフェード処理が走ってる）
    */
    Coroutine FadeRoutine;

    void Awake()
    {
        // AudioSource取得（Inspector未設定でも動くようにする）
        if (BgmSource == null) BgmSource = GetComponent<AudioSource>();

        // bgmSourceが無いと以降の再生が全部できないので、早めにエラーだして気づけるようにする
        if (BgmSource == null)
        {
            Debug.LogError("[BGMManager] AudioSource が見つかりません（同じGameObjectに付けてください）");
            enabled = false;
            return;
        }

        BgmSource.loop = true;

        // volume=0 のままだと「鳴っているのに聞こえない」事故になるので保険
        if (BgmSource.volume <= 0f) BgmSource.volume = DefaultVolume;
    }

    //=========================================================================

    // 状態ごとのBGM再生
    public void PlayTitle() => Play(TitleBgm, true);
    public void PlayGame() => Play(GameBgm, true);
    public void PlayGameOver() => Play(GameOverBgm, true);

    public void StopBgm()
    {
        if (BgmSource == null) return;

        // フェード中にさらにStopが来た時：
        // 前のコルーチンが音量を触り続けると「新しい処理」と競合して挙動がぐちゃぐちゃになる
        // なので StopCoroutine で「音量を動かしてる処理」を止める
        if (FadeRoutine != null) StopCoroutine(FadeRoutine);
        FadeRoutine = StartCoroutine(FadeOutAndStop());
    }

    public void StopBgmImmediate()
    {
        if (BgmSource == null) return;

        // フェード途中なら止める（残った処理が音量を戻したりしないように）
        if (FadeRoutine != null) StopCoroutine(FadeRoutine);

        // Stop＞再生停止　clipをnullにしてなにも再生しないようにする
        BgmSource.Stop();
        BgmSource.clip = null;

        // 次回再生時の事故防止：
        // ・フェード途中は volume が 0 付近になっているので、そのままだと次の曲が無音になる
        // ・pitchも他で弄られている可能性があるので標準値へ戻す
        BgmSource.volume = DefaultVolume;
        BgmSource.pitch = 1f;

        // 状態管理フェード中じゃない状態に戻す
        FadeRoutine = null;
    }

    void Play(AudioClip Clip, bool Loop)
    {
        if (BgmSource == null) return;

        // 未設定クリップは何もしない（安全策）
        if (Clip == null)
        {
            Debug.LogError("[BGMManager] AudioClip が未設定です");
            return;
        }

        // 同じ曲が鳴ってるなら切り替え不要
        // フェードし直すと「一瞬音が落ちる」ので違和感になる
        if (BgmSource.clip == Clip && BgmSource.isPlaying) return;

        // 進行中のフェードは止めて、最新の命令を優先　あとの命令
        if (FadeRoutine != null) StopCoroutine(FadeRoutine);

        // 【ここでやってること】
        // ・フェードアウト→差し替え→フェードインを「時間経過」でやるため、コルーチンに渡す
        FadeRoutine = StartCoroutine(FadeToClip(Clip, Loop));
    }

    //=========================================================================

    IEnumerator FadeToClip(AudioClip NewClip, bool Loop)
    {
        // フェードに使う時間（0対策）
        // 【挙動】fadeTime=0 だと dur=0 になって t/dur が割り算事故になるので最小値を入れる
        float Dur = Mathf.Max(0.0001f, FadeTime);

        // =====================================================
        // ① フェードアウト（現在の音量 → 0）
        // =====================================================
        float StartVol = BgmSource.volume;
        float t = 0f;

        // 【while】条件が満たされる間繰り返す
        while (t < Dur)
        {
            // 【Time.unscaledDeltaTime】
            // ・前フレームからの経過秒
            // ・timeScale=0(ポーズ)でも進む
            t += Time.unscaledDeltaTime;

            // 0〜1 に正規化した進行度（0=開始、1=完了）
            // 【Clamp01】0未満/1超えを防ぐ（最終フレームで少しはみ出す事がある）
            float Rate = Mathf.Clamp01(t / Dur);

            // 【Mathf.Lerp(A,B,rate)】
            // ・rate=0 → A
            // ・rate=1 → B
            // ・途中は線形でなめらかに変化
            // ここでは「startVol → 0」へ向かって音量を少しずつ下げる
            BgmSource.volume = Mathf.Lerp(StartVol, 0f, Rate);

            // 【yield return null】
            // ・この行で一旦処理が止まる
            // ・次のフレームで while の続きから再開される
            // ＝「フレームごとに音量が変化する」＝フェードになる
            yield return null;
        }

        // 念のため完全に0に固定
        BgmSource.volume = 0f;

        // =====================================================
        // ② 曲の差し替えかえ
        // =====================================================
        /* 音量0で差し替える
        // ・差し替え瞬間に前の曲が一瞬聞こえる／プチッとなるのを避ける
         ・無音の状態で差し替える＞自然な切り替えにする
        */
        BgmSource.loop = Loop;
        BgmSource.clip = NewClip; // 曲が変わる
        BgmSource.Play();         // 新しい曲の再生開始

        // =====================================================
        // ③ フェードイン（0 → defaultVolume）
        // =====================================================
        // t をリセットして「もう一回0から進行度を作り直す」
        t = 0f;

        while (t < Dur)
        {
            t += Time.unscaledDeltaTime;

            float Rate = Mathf.Clamp01(t / Dur);

            // ここでは「0 → defaultVolume」へ音量を少しずつ上げる
            BgmSource.volume = Mathf.Lerp(0f, DefaultVolume, Rate);

            // 次のフレームへ（＝フェードインが進む）
            yield return null;
        }

        // 音量
        BgmSource.volume = DefaultVolume;

        // フェード完了（待機状態に戻す）
        FadeRoutine = null;
    }

    //=========================================================================

    IEnumerator FadeOutAndStop()
    {
        float Dur = Mathf.Max(0.0001f, FadeTime);

        float StartVol = BgmSource.volume;
        float t = 0f;

        // フェードアウトのみ（曲は差し替えない）
        while (t < Dur)
        {
            t += Time.unscaledDeltaTime;

            float Rate = Mathf.Clamp01(t / Dur);

            // startVol → 0 に向けて徐々に下げる（FadeToClipのフェードアウトと同じ仕組み）
            BgmSource.volume = Mathf.Lerp(StartVol, 0f, Rate);

            // 1フレーム待つ＝次のフレームでまた少し下げる＝フェードになる
            yield return null;
        }

        // 無音を保証
        BgmSource.volume = 0f;

        // 停止処理
        // 【Stop】再生を止めるだけ（volumeはそのままなので、上で0にしてから止めている）
        BgmSource.Stop();

        // clipをnullにすることで「何も再生していない状態」を明確化
        // （あとで bgmSource.clip == clip の判定にも影響してくる）
        BgmSource.clip = null;

        // 状態リセット
        FadeRoutine = null;
    }
}
