using UnityEngine;

/// <summary>
/// プレイヤーの弾発射を制御するクラス
/// ・発射間隔管理
/// ・入力受付
/// ・弾の生成（PoolManager利用）
/// ・発射SEの再生
/// </summary>
public class BulletController : MonoBehaviour
{
    /// <summary>弾のプール管理クラス（弾の生成・再利用を担当）</summary>
    public PoolManager pool;

    /// <summary>プレイヤーのTransform（弾の発射位置を参照）</summary>
    public Transform player;

    /// <summary>弾の発射間隔（秒単位、連射速度の調整用）</summary>
    public float FireInterval = 0.2f;

    /// <summary>発射間隔を計測するタイマー（経過時間を加算）</summary>
    public float fireTimer = 0f;

    /// <summary>入力管理クラス（攻撃入力の検出に使用）</summary>
    InputSystem_Actions input;

    [Header("Control")]
    [Tooltip("false の間は攻撃入力を受け付けない")]
    /// <summary>操作受付可否（trueで発射可能、falseで無効化）</summary>
    public bool ControlEnabled = true;

    [Header("Audio")]
    /// <summary>効果音再生用AudioSource（発射SEの再生に使用）</summary>
    public AudioSource seSource;

    /// <summary>発射時の効果音（AudioClipを指定）</summary>
    public AudioClip LaunchSE;

    [Range(0f, 1f)]
    /// <summary>効果音の音量（0.0～1.0）</summary>
    public float volume = 1.0f;

    [Header("Limiter")]
    [Tooltip("この秒数以内の連続再生は無視（多重防止）")]
    /// <summary>効果音の多重再生防止間隔（秒単位、SEの連続再生抑制）</summary>
    public float minInterval = 0.05f;

    /// <summary>最後に効果音を再生した時刻（Time.timeで管理）</summary>
    float lastPlayTime = -999f;

    /// <summary>
    /// 初期化処理。入力管理クラスのインスタンス生成
    /// </summary>
    void Awake()
    {
        // 入力管理クラスを初期化
        input = new InputSystem_Actions();
    }

    /// <summary>
    /// オブジェクト有効化時に入力受付開始
    /// </summary>
    void OnEnable()
    {
        // 入力受付を有効化
        input.Enable();
    }

    /// <summary>
    /// オブジェクト無効化時に入力受付停止
    /// </summary>
    void OnDisable()
    {
        // 入力受付を無効化
        input.Disable();
    }

    /// <summary>
    /// 毎フレーム呼ばれる。弾発射処理・タイマー管理・SE再生など
    /// </summary>
    void Update()
    {
        // 操作不能中は発射しない（タイマーも回さない）
        if (!ControlEnabled) return;

        // 発射間隔タイマーを進める
        fireTimer += Time.deltaTime;

        // 攻撃入力が押されていて、発射間隔を超えていれば発射
        if (input.Player.Attack.IsPressed() && fireTimer >= FireInterval)
        {
            // 発射SEが設定されていなければ何もしない
            if (LaunchSE == null) return;

            // 効果音の多重再生防止（minInterval未満なら再生しない）
            if (Time.time - lastPlayTime < minInterval)
                return;

            // 最終再生時刻を更新
            lastPlayTime = Time.time;

            // 発射SE再生
            seSource.PlayOneShot(LaunchSE, volume);

            // プールから弾を取得し、プレイヤー位置に生成
            GameObject bullet = pool.GetGameObject(
                player.position,
                Quaternion.identity
            );

            // 弾にDestroyerコンポーネントがあれば、プール管理と自動破棄タイマーを設定
            var destroyer = bullet.GetComponent<Destroyer>();
            if (destroyer != null)
            {
                // プール管理クラスを設定
                destroyer.PoolManager = pool;
                // 2秒後に自動で弾を破棄（プールに戻す）
                destroyer.StartDestroyTimer(2f);
            }

            // 発射間隔タイマーをリセット
            fireTimer = 0f;
        }
    }
}
