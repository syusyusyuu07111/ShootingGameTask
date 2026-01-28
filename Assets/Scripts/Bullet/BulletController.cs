using UnityEngine;

/*
     プレイヤーの弾発射を制御するクラス

     【主な役割】
     ・発射間隔（クールタイム）を管理する
     ・入力を監視して発射条件を満たしたら弾を出す
     ・弾はPoolManagerから取得して再利用する
     ・発射SEを再生する
     ・弾の寿命（返却タイマー）を設定する

     【設計方針】
     ・Updateは「撃つかどうか」だけに集中させる
     ・参照の未設定はStartでエラーを出し、実行中にログが増え続けないようにする
     ・transform参照はキャッシュして読みやすくする
*/
public sealed class BulletController : MonoBehaviour
{
    //================
    // Reference
    //================

    [SerializeField] private PoolManager Pool;

    [Tooltip("弾の出現位置の基準（プレイヤーTransform）")]
    [SerializeField] private Transform Player;


    //================
    // Fire Settings
    //================

    [SerializeField] private float FireInterval = 0.2f;

    /*
         発射間隔タイマー

         ・deltaTimeで加算していき、FireInterval以上になったら発射可能
         ・発射した瞬間に0へ戻す
    */
    private float FireTimer = 0f;


    //================
    // Input
    //================

    private InputSystem_Actions Input;

    [Header("Control")]
    [Tooltip("false の間は攻撃入力を受け付けない")]
    public bool ControlEnabled = true;


    //================
    // Audio
    //================

    [Header("Audio")]
    [SerializeField] private AudioSource SeSource;

    [SerializeField] private AudioClip LaunchSE;

    [Range(0f, 1f)]
    [SerializeField] private float Volume = 1.0f;

    [Header("Limiter")]
    [Tooltip("この秒数以内の連続再生は無視（多重防止）")]
    [SerializeField] private float MinInterval = 0.05f;

    private float LastPlayTime = -999f;


    //================
    // Bullet Life
    //================

    [Header("Bullet Life (Destroyer)")]
    [Tooltip("Destroyerが付いている場合に設定する自動返却時間")]
    [SerializeField] private float BulletLifeTime = 2f;


    //================
    // Cache
    //================

    private Transform PlayerTransform;


    //================
    // Unity Event
    //================

    private void Awake()
    {
        /*
             InputSystem_Actions を生成する
             入力のEnable/DisableはOnEnable/OnDisableで行う
        */
        Input = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        /*
             Player操作マップを有効化する
        */
        Input.Player.Enable();
    }

    private void OnDisable()
    {
        /*
             入力受付を停止する
        */
        Input.Player.Disable();
    }

    private void Start()
    {
        //================
        // Validate
        //================

        if (Pool == null) Debug.LogError("[BulletController] Pool が未設定です（PoolManager を設定してください）");
        if (Player == null) Debug.LogError("[BulletController] Player が未設定です（Transform を設定してください）");
        if (SeSource == null) Debug.LogError("[BulletController] SeSource が未設定です（AudioSource を設定してください）");

        PlayerTransform = Player;

        if (SeSource != null) SeSource.playOnAwake = false;
    }

    private void Update()
    {
        //================
        // Control
        //================

        if (!ControlEnabled) return;

        if (Pool == null) return;
        if (PlayerTransform == null) return;

        //================
        // Timer
        //================

        FireTimer += Time.deltaTime;

        //================
        // Input Check
        //================

        bool IsAttack = Input.Player.Attack.IsPressed();
        if (!IsAttack) return;

        if (FireTimer < FireInterval) return;

        //================
        // Fire
        //================

        Fire();
    }


    //================
    // Fire Core
    //================

    private void Fire()
    {
        //================
        // SE
        //================

        PlayLaunchSe();

        //================
        // Spawn
        //================

        Vector3 SpawnPos = PlayerTransform.position;
        Quaternion SpawnRot = Quaternion.identity;

        GameObject BulletInstance = Pool.GetGameObject(SpawnPos, SpawnRot);
        if (BulletInstance == null)
        {
            Debug.LogError("[BulletController] Pool から弾が取得できませんでした（GetGameObject が null を返しました）");
            return;
        }

        //================
        // Destroyer Setup
        //================

        /*
             Destroyerが付いているなら寿命（返却タイマー）を設定する

             ・PoolManagerへの返却口は Destroyer が持つ
             ・PoolManagerを渡しておけば、寿命後にReleaseGameObjectを呼べる
        */
        Destroyer Destroyer = BulletInstance.GetComponent<Destroyer>();
        if (Destroyer != null)
        {
            Destroyer.SetPoolManager(Pool);
            Destroyer.StartDestroyTimer(BulletLifeTime);
        }

        //================
        // Timer Reset
        //================

        FireTimer = 0f;
    }


    //================
    // SE Helper
    //================

    private void PlayLaunchSe()
    {
        if (LaunchSE == null)
        {
            Debug.LogError("[BulletController] LaunchSE が未設定です（AudioClip を設定してください）");
            return;
        }

        if (SeSource == null)
        {
            Debug.LogError("[BulletController] SeSource が未設定です（AudioSource を設定してください）");
            return;
        }

        if (Time.time - LastPlayTime < MinInterval) return;
        LastPlayTime = Time.time;

        SeSource.PlayOneShot(LaunchSE, Volume);
    }
}
