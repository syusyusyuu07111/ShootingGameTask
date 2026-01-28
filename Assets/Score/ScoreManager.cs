using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// スコア表示管理
/// ・数字部分だけを拡縮アニメーションさせる
/// ・ラベル部分（SCORE:）は一切動かない
/// </summary>
public class ScoreManager : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text scoreText;

    [Header("Score")]
    public int scorePerKill = 100;
    public int Score { get; private set; }

    // ======================
    // Number Punch
    // ======================
    [Header("Number Punch")]
    [Tooltip("数字を最大何%まで拡大するか")]
    public float punchSizePercent = 130f;

    [Tooltip("拡大にかける時間")]
    public float punchUpTime = 0.06f;

    [Tooltip("戻る時間")]
    public float punchDownTime = 0.12f;

    Coroutine punchRoutine;

    const string LABEL = "SCORE : ";

    void Start()
    {
        RefreshUI(100f);
    }

    // ======================
    // 外部から呼ぶ
    // ======================
    public void AddKillScore()
    {
        AddScore(scorePerKill);
    }

    public void AddScore(int amount)
    {
        if (amount <= 0) return;

        Score += amount;

        if (punchRoutine != null)
            StopCoroutine(punchRoutine);

        punchRoutine = StartCoroutine(NumberPunchRoutine());
    }

    public void ResetScore()
    {
        Score = 0;
        RefreshUI(100f);
    }

    public bool CommitScoreToBestTop3()
    {
        return BestScoreTop3.TryRegister(Score);
    }
    public void ShowScore()
    {
        if (scoreText != null)
            scoreText.gameObject.SetActive(true);
    }

    public void HideScore()
    {
        if (scoreText != null)
            scoreText.gameObject.SetActive(false);
    }


    // ======================
    // 表示更新
    // ======================
    void RefreshUI(float sizePercent)
    {
        if (scoreText == null) return;

        // 数字部分だけ size タグで囲う
        scoreText.text =
            $"{LABEL}<size={sizePercent}%>{Score}</size>";
    }

    // ======================
    // アニメーション
    // ======================
    IEnumerator NumberPunchRoutine()
    {
        float upDur = Mathf.Max(0.0001f, punchUpTime);
        float downDur = Mathf.Max(0.0001f, punchDownTime);

        // ---------- 拡大 ----------
        float t = 0f;
        while (t < upDur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / upDur);

            float size = Mathf.Lerp(100f, punchSizePercent, EaseOutCubic(a));
            RefreshUI(size);

            yield return null;
        }

        // ---------- 戻す ----------
        t = 0f;
        while (t < downDur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / downDur);

            float size = Mathf.Lerp(punchSizePercent, 100f, EaseOutCubic(a));
            RefreshUI(size);

            yield return null;
        }

        RefreshUI(100f);
        punchRoutine = null;
    }

    // ======================
    // easing
    // ======================
    static float EaseOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f - Mathf.Pow(1f - x, 3f);
    }
}
