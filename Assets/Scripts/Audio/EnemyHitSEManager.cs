using UnityEngine;

/*
    敵が被弾した際の効果音（SE）を一元管理するクラス
    ・EnemyController から呼び出される
    ・多重再生防止、音量調整、再生間隔制御を担当
*/
public class EnemyHitSEManager : MonoBehaviour
{
    /*
        EnemyHitSEManager のインスタンス
        シングルトンとして管理する
    */
    public static EnemyHitSEManager Instance { get; private set; }

    [Header("Audio")]
    /*
        効果音を再生するための AudioSource
    */
    [SerializeField] private AudioSource SeSource;

    /*
        敵が被弾した時に再生する AudioClip
    */
    [SerializeField] private AudioClip EnemyHitSE;

    [Range(0f, 1f)]
    /*
        効果音の音量（0.0～1.0）
    */
    [SerializeField] private float Volume = 1.0f;

    [Header("Limiter")]
    [Tooltip("この秒数以内の連続再生は無視（多重防止）")]
    /*
        効果音の連続再生を防ぐための最小間隔（秒）
    */
    [SerializeField] private float MinInterval = 0.05f;

    /*
        最後に効果音を再生した時刻（Time.time）
    */
    float LastPlayTime = -999f;

    void Awake()
    {
        // シングルトン 既に存在する場合は自身を破棄
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // AudioSource の取得 or 追加
        if (SeSource == null) SeSource = GetComponent<AudioSource>();
        if (SeSource == null) SeSource = gameObject.AddComponent<AudioSource>();

        if (SeSource == null)
        {
            Debug.LogError("[EnemyHitSEManager] AudioSource が取得できません");
            enabled = false;
            return;
        }

        // AudioSource 初期設定
        SeSource.playOnAwake = false;
        SeSource.loop = false;
        SeSource.spatialBlend = 0f;
    }

    /*
        敵が被弾した瞬間に呼び出す
        ・最小間隔を守りつつ効果音を再生する
    */
    public void PlayEnemyHit()
    {
        // 再生できない条件はまとめて弾く
        if (EnemyHitSE == null || Time.time - LastPlayTime < MinInterval) return;

        LastPlayTime = Time.time;

        SeSource.PlayOneShot(EnemyHitSE, Volume);
    }
}
