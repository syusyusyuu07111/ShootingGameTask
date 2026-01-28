using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
     敵を一定時間ごとに生成・管理するクラス

     【主な役割】
     ・スポーン開始 / 停止ができる
     ・生成した敵をListで保持し、後で参照・全削除に使う
     ・Coroutineで「一定間隔スポーン」を実現する
     ・Destroyされた参照がListに残る問題を適切なタイミングで掃除する

     【設計方針】
     ・Updateは持たない（毎フレーム処理を作らない）
     ・transform / camera / player参照はキャッシュして読みやすくする
     ・Listの掃除は「必要な時だけ」行い、無駄な処理を減らす
*/
public sealed class EnemySpawner : MonoBehaviour
{
    //================
    // Spawn Settings
    //================

    [Header("Spawn Settings")]
    [SerializeField] private float AppearanceTime = 3f;

    [SerializeField] private GameObject EnemyPrefab;

    [Tooltip("スポーン位置計算に使うプレイヤー（基準Y）")]
    [SerializeField] private Transform Player;


    //================
    // Spawned List
    //================

    /*
         生成した敵インスタンス一覧

         ・List<GameObject> に「生成した敵の参照」を保持する
         ・あとで「全削除」「特定敵の削除」「敵一覧参照」に使う
    */
    private readonly List<GameObject> Spawned = new List<GameObject>();


    //================
    // Routine
    //================

    /*
         スポーン処理用コルーチン

         ・StartCoroutineの戻り値を保持してStopできるようにする
         ・nullかどうかで「いまスポーン中か」を判断できる
    */
    private Coroutine SpawnRoutine;


    //================
    // Spawn SE
    //================

    [Header("Spawn SE")]
    [Tooltip("未設定ならこのオブジェクトから自動取得（無ければ自動追加）")]
    [SerializeField] private AudioSource SeSource;

    [Tooltip("敵が登場した瞬間に鳴らすSE（未使用ならnullでもOK）")]
    [SerializeField] private AudioClip LaunchSE;

    [Range(0f, 1f)]
    [SerializeField] private float Volume = 1.0f;

    [Header("Limiter")]
    [Tooltip("この秒数以内の連続再生は無視（多重防止）")]
    [SerializeField] private float MinInterval = 0.05f;

    private float LastPlayTime = -999f;


    //================
    // Cache
    //================

    /*
         自身のTransform

         ・position参照を統一して読みやすくする
         ・transformプロパティを何度も呼ばない
    */
    private Transform SpawnerTransform;

    /*
         PlayerのTransform（参照用）
         ・PlayerはSerializeFieldなので、基本はStartで未設定チェックする
    */
    private Transform PlayerTransform;

    /*
         MainCameraを保持

         ・Camera.main はタグ検索で重いので保持する
         ・シーン切替などで失われる可能性があるため、必要なら拾い直す
    */
    private Camera Cam;

    /*
         Cam.transform を保持
         ・カメラ位置を読むだけならTransformを持つ方が読みやすい
    */
    private Transform CamTransform;


    //================
    // Unity Event
    //================

    private void Awake()
    {
        /*
             Transformを保持しておく
        */
        SpawnerTransform = transform;
    }

    private void Start()
    {
        //================
        // Validate
        //================

        /*
             必須参照チェック（ルール：未設定はエラー）

             ・EnemyPrefab が無いとスポーンできない
             ・Player が無いとスポーン位置計算ができない
        */
        if (EnemyPrefab == null) Debug.LogError("[EnemySpawner] EnemyPrefab が未設定です");
        if (Player == null) Debug.LogError("[EnemySpawner] Player が未設定です");

        PlayerTransform = Player;

        //================
        // AudioSource Setup
        //================

        /*
             AudioSource取得

             ・Inspector未設定でも動くように GetComponent で拾う
             ・それでも無ければ AddComponent で作る
        */
        if (SeSource == null) SeSource = GetComponent<AudioSource>();
        if (SeSource == null) SeSource = gameObject.AddComponent<AudioSource>();

        SeSource.playOnAwake = false;
        SeSource.loop = false;

        //================
        // Camera Cache
        //================

        /*
             Cameraを保持する
             ・Camera.main は毎回呼ばない
             ・取れない場合はエラー（MainCameraタグ確認）
        */
        CacheMainCamera();
        if (Cam == null) Debug.LogError("[EnemySpawner] Camera.main が取れません（MainCameraタグ確認）");
    }


    //================
    // Public
    //================

    /*
         現在生成されている敵一覧を取得（外から読む用）

         ・掃除してから返す（nullが混ざった状態を返さない）
         ・IReadOnlyListで返して、外側からAdd/Removeできないようにする
    */
    public IReadOnlyList<GameObject> GetSpawnedEnemies()
    {
        CleanupSpawnedList();
        return Spawned;
    }

    /*
         スポーン開始
         既にスポーン中なら何もしない
    */
    public void StartSpawn()
    {
        // 二重起動防止
        if (SpawnRoutine != null) return;

        // 必須参照チェック
        if (EnemyPrefab == null)
        {
            Debug.LogError("[EnemySpawner] EnemyPrefab が未設定です");
            return;
        }

        if (PlayerTransform == null)
        {
            Debug.LogError("[EnemySpawner] Player が未設定です");
            return;
        }

        // Camera参照チェック（必要なら拾い直す）
        if (Cam == null || CamTransform == null) CacheMainCamera();
        if (Cam == null || CamTransform == null)
        {
            Debug.LogError("[EnemySpawner] Camera.main が取れません（MainCameraタグ確認）");
            return;
        }

        SpawnRoutine = StartCoroutine(SpawnLoop());
    }

    /*
         スポーン停止
         コルーチンを止めて、以降生成されなくする
    */
    public void StopSpawn()
    {
        if (SpawnRoutine == null) return;

        StopCoroutine(SpawnRoutine);
        SpawnRoutine = null;
    }

    /*
         指定した敵を消す（Spawner管理下の削除口）

         ・Listから外す
         ・Destroy（遅延も可）
    */
    public void KillSpawned(GameObject EnemyInstance, float Delay = 0f)
    {
        if (EnemyInstance == null) return;

        Spawned.Remove(EnemyInstance);

        if (Delay <= 0f) Destroy(EnemyInstance);
        if (Delay > 0f) Destroy(EnemyInstance, Delay);
    }

    /*
         生成済みの全ての敵を削除し、Listもクリアする
         タイトル戻りやリトライ時に呼び出す
    */
    public void ClearAllSpawned()
    {
        CleanupSpawnedList();

        for (int i = Spawned.Count - 1; i >= 0; i--)
        {
            if (Spawned[i] == null) continue;
            Destroy(Spawned[i]);
        }

        Spawned.Clear();
    }


    //================
    // Spawn Loop
    //================

    private IEnumerator SpawnLoop()
    {
        /*
             while(true) + WaitForSeconds で「一定間隔でスポーン」を作る

             ・1回スポーンする
             ・AppearanceTime秒待つ
             ・またスポーンする
             を繰り返す
        */
        while (true)
        {
            //================
            // Validate (Runtime)
            //================

            /*
                 途中で参照が切れた場合の保険

                 ・シーン切替や破棄で参照がnullになる可能性がある
                 ・致命なら待って再試行する
            */
            if (EnemyPrefab == null)
            {
                Debug.LogError("[EnemySpawner] EnemyPrefab が未設定です（スポーンできません）");
                yield return new WaitForSeconds(1f);
                continue;
            }

            if (PlayerTransform == null)
            {
                Debug.LogError("[EnemySpawner] Player が未設定です（スポーンできません）");
                yield return new WaitForSeconds(1f);
                continue;
            }

            if (Cam == null || CamTransform == null) CacheMainCamera();
            if (Cam == null || CamTransform == null)
            {
                Debug.LogError("[EnemySpawner] Camera.main が取れません（MainCameraタグ確認）");
                yield return new WaitForSeconds(1f);
                continue;
            }

            //================
            // Cleanup List
            //================

            /*
                 null参照掃除

                 ・Destroyされた敵がListに残る問題をここで解消する
                 ・毎フレーム掃除しない（必要時だけ）
            */
            CleanupSpawnedList();

            //================
            // Spawn
            //================

            /*
                 スポーン位置を計算する

                 ・カメラ範囲内のXにランダム
                 ・プレイヤーの上側にランダム
            */
            Vector3 Pos = GetSpawnPosition(Cam, CamTransform, PlayerTransform);

            /*
                 敵を生成する
            */
            GameObject EnemyInstance = Instantiate(EnemyPrefab, Pos, Quaternion.identity);
            if (EnemyInstance == null)
            {
                Debug.LogError("[EnemySpawner] Instantiateに失敗しました");
                yield return new WaitForSeconds(AppearanceTime);
                continue;
            }

            Spawned.Add(EnemyInstance);

            //================
            // SE
            //================

            PlayLaunchSE();

            //================
            // Owner Set
            //================

            /*
                 Spawnerを所有者として渡す

                 ・EnemyController側がSpawner経由で消せるようになる
                 ・子階層も探す / 非アクティブも対象
            */
            EnemyController Ec = EnemyInstance.GetComponentInChildren<EnemyController>(true);
            if (Ec == null) Debug.LogError($"[EnemySpawner] Spawned '{EnemyInstance.name}' に EnemyController がありません。Prefabに付けてください。");
            if (Ec != null) Ec.SetOwner(this, EnemyInstance);

            yield return new WaitForSeconds(AppearanceTime);
        }
    }


    //================
    // SE
    //================

    private void PlayLaunchSE()
    {
        /*
             SEが未設定なら何もしない

             ・SEは演出だが、無くてもゲームは進行できる
             ・ただし「鳴らしたいのに未設定」はミスなのでエラーを出す
        */
        if (LaunchSE == null)
        {
            Debug.LogError("[EnemySpawner] LaunchSE が未設定です");
            return;
        }

        /*
             多重再生防止

             ・MinInterval以内の連続再生は無視する
             ・Time.time は timeScale=0 で止まる（ポーズ中に鳴らす設計なら unscaledTime を使う）
        */
        if (Time.time - LastPlayTime < MinInterval) return;
        LastPlayTime = Time.time;

        /*
             再生位置

             ・AudioSourceがあるならその場で鳴らす
             ・無い場合の保険として PlayClipAtPoint を使う
        */
        Vector3 SePos = Vector3.zero;
        if (SpawnerTransform != null) SePos = SpawnerTransform.position;

        if (SeSource == null)
        {
            Debug.LogError("[EnemySpawner] SeSource が未設定です（Startで確保できていない）");
            AudioSource.PlayClipAtPoint(LaunchSE, SePos, Volume);
            return;
        }

        SeSource.PlayOneShot(LaunchSE, Volume);
    }


    //================
    // Cache Helper
    //================

    private void CacheMainCamera()
    {
        /*
             MainCameraを取得して保持する

             ・Camera.main はタグ検索なので必要時だけ呼ぶ
             ・取得できたらTransformも保持する
        */
        Cam = Camera.main;
        CamTransform = null;

        if (Cam != null)
            CamTransform = Cam.transform;
    }


    //================
    // List Cleanup
    //================

    private void CleanupSpawnedList()
    {
        /*
             Destroyされた参照をListから取り除く

             ・DestroyしてもListから自動で消えない
             ・後ろからRemoveAtする（前から消すと詰まりで飛ばしが起きる）
        */
        for (int i = Spawned.Count - 1; i >= 0; i--)
        {
            if (Spawned[i] == null) Spawned.RemoveAt(i);
        }
    }


    //================
    // Spawn Position
    //================

    private Vector3 GetSpawnPosition(Camera Cam, Transform CamTransform, Transform PlayerTransform)
    {
        /*
             画面サイズ

             ・orthographicSize は「画面の縦半分」
             ・aspect を掛けると「画面の横半分」
        */
        float h = Cam.orthographicSize;
        float w = h * Cam.aspect;

        /*
             カメラ中心X
             ・ここを基準に画面内でランダムに出す
        */
        float cx = CamTransform.position.x;

        /*
             X：画面内ランダム
             Y：プレイヤーより上にランダム
        */
        float x = Random.Range(cx - w, cx + w);
        float y = PlayerTransform.position.y + Random.Range(0f, h);

        return new Vector3(x, y, 0f);
    }
}
