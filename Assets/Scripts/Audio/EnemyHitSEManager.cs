using UnityEngine;

/// <summary>
/// 敵が被弾した際の効果音（SE）を一元管理するクラス
/// ・EnemyController から呼び出される
/// ・多重再生防止、音量調整、再生間隔制御、距離減衰（未実装）を担当
/// </summary>
public class EnemyHitSEManager : MonoBehaviour
{
    /// <summary>
    /// EnemyHitSEManager の唯一のインスタンス（シングルトン）
    /// </summary>
    public static EnemyHitSEManager Instance { get; private set; }

    [Header("Audio")]
    /// <summary>
    /// 効果音を再生するための AudioSource
    /// </summary>
    public AudioSource seSource;

    /// <summary>
    /// 敵が被弾した時に再生する AudioClip（効果音データ）
    /// </summary>
    public AudioClip enemyHitSE;

    [Range(0f, 1f)]
    /// <summary>
    /// 効果音の音量（0.0～1.0）
    /// </summary>
    public float volume = 1.0f;

    [Header("Limiter")]
    [Tooltip("この秒数以内の連続再生は無視（多重防止）")]
    /// <summary>
    /// 効果音の連続再生を防ぐための最小間隔（秒）
    /// </summary>
    public float minInterval = 0.05f;

    /// <summary>
    /// 最後に効果音を再生した時刻（Time.time）
    /// </summary>
    float lastPlayTime = -999f;

    void Awake()
    {
        // シングルトンパターン　既にインスタンスが存在する場合は自身を破棄
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // AudioSource が未設定の場合は取得または追加
        if (seSource == null)
            seSource = GetComponent<AudioSource>();

        if (seSource == null)
            seSource = gameObject.AddComponent<AudioSource>();

        // AudioSource の初期設定
        seSource.playOnAwake = false;   // 自動再生しない
        seSource.loop = false;          // ループ再生しない
        seSource.spatialBlend = 0f;     // 2D音（空間効果なし）
    }

    /// <summary>
    /// 敵が被弾した瞬間に呼び出すメソッド
    /// 効果音の多重再生を防ぎつつ、指定音量で再生する
    /// </summary>
    public void PlayEnemyHit()
    {
        // 効果音が設定されていない場合は何もしない
        if (enemyHitSE == null) return;

        // 最小間隔未満の場合は再生しない（多重再生防止）
        if (Time.time - lastPlayTime < minInterval)
            return;

        // 最後に再生した時刻を更新
        lastPlayTime = Time.time;

        // 効果音を一度だけ再生
        seSource.PlayOneShot(enemyHitSE, volume);
    }
}
