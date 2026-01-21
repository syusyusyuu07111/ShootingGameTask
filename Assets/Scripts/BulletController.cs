using UnityEngine;

/*
     プレイヤーの弾発射を制御するクラス
     ・発射間隔管理
     ・入力受付
     ・弾の生成（PoolManager利用）
     ・発射SEの再生
*/
public class BulletController : MonoBehaviour
{
    //================
    // 参照
    //================

    public PoolManager Pool;
    public Transform Player;

    //================
    // 発射設定
    //================

    public float FireInterval = 0.2f;
    public float FireTimer = 0f;

    //================
    // 入力
    //================

    InputSystem_Actions Input;

    [Header("Control")]
    [Tooltip("false の間は攻撃入力を受け付けない")]
    public bool ControlEnabled = true;

    //================
    // Audio
    //================

    [Header("Audio")]
    public AudioSource SeSource;
    public AudioClip LaunchSE;

    [Range(0f, 1f)]
    public float Volume = 1.0f;

    [Header("Limiter")]
    [Tooltip("この秒数以内の連続再生は無視（多重防止）")]
    public float MinInterval = 0.05f;

    float LastPlayTime = -999f;

    //================
    // Unity Event
    //================

    void Awake()
    {
        /*
             InputSystem を生成する
        */
        Input = new InputSystem_Actions();
    }

    void OnEnable()
    {
        /*
             入力受付を開始する
        */
        Input.Enable();
    }

    void OnDisable()
    {
        /*
             入力受付を停止する
        */
        Input.Disable();
    }

    void Update()
    {
        /*
             操作不能中は発射しない
             発射間隔タイマーも進めない
        */
        if (!ControlEnabled) return;

        /*
             参照が未設定ならエラーを出す
        */
        if (Pool == null) { Debug.LogError("[BulletController] Pool が未設定です（PoolManager を設定してください）"); return; }
        if (Player == null) { Debug.LogError("[BulletController] Player が未設定です（Transform を設定してください）"); return; }
        if (SeSource == null) { Debug.LogError("[BulletController] SeSource が未設定です（AudioSource を設定してください）"); return; }

        /*
             発射間隔タイマーを進める
        */
        FireTimer += Time.deltaTime;

        /*
             攻撃入力が押されていて、発射間隔を超えていれば発射する
        */
        bool IsAttack = Input.Player.Attack.IsPressed();
        bool CanFire = FireTimer >= FireInterval;

        if (!IsAttack || !CanFire) return;

        /*
             発射SEが未設定ならエラーを出す
        */
        if (LaunchSE == null)
        {
            Debug.LogError("[BulletController] LaunchSE が未設定です（AudioClip を設定してください）");
            return;
        }

        /*
             効果音の多重再生防止（MinInterval未満なら再生しない）
        */
        if (Time.time - LastPlayTime < MinInterval) return;

        LastPlayTime = Time.time;

        /*
             発射SE再生
        */
        SeSource.PlayOneShot(LaunchSE, Volume);

        /*
             プールから弾を取得して、プレイヤー位置に出す
        */
        Vector3 SpawnPos = Player.position;
        Quaternion SpawnRot = Quaternion.identity;

        GameObject Bullet = Pool.GetGameObject(SpawnPos, SpawnRot);
        if (Bullet == null)
        {
            Debug.LogError("[BulletController] Pool から弾が取得できませんでした（GetGameObject が null を返しました）");
            return;
        }

        /*
             Destroyer が付いているなら、プール返却と自動破棄タイマーを設定する
        */
        Destroyer Destroyer = Bullet.GetComponent<Destroyer>();
        if (Destroyer != null)
        {
            Destroyer.PoolManager = Pool;
            Destroyer.StartDestroyTimer(2f);
        }

        /*
             発射間隔タイマーをリセットする
        */
        FireTimer = 0f;
    }
}
