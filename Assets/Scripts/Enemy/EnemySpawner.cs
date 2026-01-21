using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
     敵を一定時間ごとに生成・管理するクラス

     ・スポーン開始 / 停止が可能
     ・生成した敵をListで保持（後で全削除や参照に使う）
     ・Coroutine + IEnumerator で「一定間隔スポーン」を実現
     ・Destroyされた参照がListに残る問題を CleanupList() で掃除して防ぐ
*/
public class EnemySpawner : MonoBehaviour
{
    //================
    // Spawn Settings
    //================

    [Header("Spawn Settings")]
    [SerializeField] private float AppearanceTime = 3f;        // 敵を生成する間隔
    [SerializeField] private GameObject EnemyPrefab;           // 生成する敵Prefab
    [SerializeField] private Transform Player;                 // スポーン位置計算用（プレイヤー基準）

    /*
         生成した敵インスタンス一覧

         ・List<GameObject> に「生成した敵の参照」を保持する
         ・あとで「全削除」「特定敵の削除」「敵一覧参照」に使う
    */
    readonly List<GameObject> Spawned = new List<GameObject>();

    /*
         スポーン処理用コルーチン

         ・StartCoroutine の戻り値を保存しておくと StopCoroutine できる
         ・SpawnRoutine が null かどうかで「いまスポーン中？」が分かる
    */
    Coroutine SpawnRoutine;

    //================
    // Spawn SE
    //================

    [Header("Spawn SE")]
    [Tooltip("未設定ならこのオブジェクトから自動取得（無ければ自動追加）")]
    [SerializeField] private AudioSource SeSource;

    [Tooltip("敵が登場した瞬間に鳴らすSE")]
    [SerializeField] private AudioClip LaunchSE;

    [Range(0f, 1f)]
    [SerializeField] private float Volume = 1.0f;

    [Header("Limiter")]
    [Tooltip("この秒数以内の連続再生は無視（多重防止）")]
    [SerializeField] private float MinInterval = 0.05f;

    float LastPlayTime = -999f;

    //================
    // Cache
    //================

    /*
         MainCamera を保持
         Camera.main を毎回呼ばないため
    */
    Camera Cam;

    /*
         Cam.transform を保持
         位置参照を軽くして読みやすくするため
    */
    Transform CamTr;

    /*
         自身の Transform を保持
         transform.position を毎回取りに行かないため
    */
    Transform Tr;

    //================
    // Unity Event
    //================

    void Awake()
    {
        /*
             transform を保持する
             Unityのプロパティ呼び出しを減らして、読みやすくする
        */
        Tr = transform;
    }

    void Start()
    {
        /*
             AudioSource取得

             ・Inspector未設定でも動くように GetComponent で拾う
             ・それでも無ければ AddComponent で作る
        */
        if (SeSource == null) SeSource = GetComponent<AudioSource>();
        if (SeSource == null) SeSource = gameObject.AddComponent<AudioSource>();

        SeSource.playOnAwake = false;
        SeSource.loop = false;

        /*
             Camera取得（保持）

             ・Camera.main はタグ検索なので毎回呼ばない
             ・未設定はエラー（ルール）
        */
        Cam = Camera.main;
        if (Cam == null) Debug.LogError("[EnemySpawner] Camera.main が取れません（MainCameraタグ確認）");
        if (Cam != null) CamTr = Cam.transform;

        /*
             未設定チェック（ルール：未設定はエラーを出す）
        */
        if (EnemyPrefab == null) Debug.LogError("[EnemySpawner] EnemyPrefab が未設定です");
        if (Player == null) Debug.LogError("[EnemySpawner] Player が未設定です");
        if (LaunchSE == null) Debug.LogError("[EnemySpawner] LaunchSE が未設定です（SEを鳴らすなら設定してください）");
    }

    void Update()
    {
        /*
             Destroyされた敵がListに残る問題を掃除する

             ・DestroyしてもListから自動で消えない
             ・Spawner経由で消さないケース（自滅/画面外/別処理）でも汚れないようにする
        */
        CleanupList();
    }

    //======================
    // Public
    //======================

    //======================
    /// 現在生成されている敵一覧を取得（外から読む用）
    /// IReadOnlyListで返して「外からAdd/Removeできない」ようにする
    //======================
    public IReadOnlyList<GameObject> GetSpawnedEnemies()
    {
        return Spawned;
    }

    //======================
    /// スポーン開始
    /// 既にスポーン中なら何もしない
    //======================
    public void StartSpawn()
    {
        /*
             二重起動防止

             ・SpawnLoopが2本走ると、敵が2倍ペースで出てしまう
        */
        if (SpawnRoutine != null) return;

        /*
             参照チェック（未設定はエラー）
        */
        if (EnemyPrefab == null)
        {
            Debug.LogError("[EnemySpawner] EnemyPrefab が未設定です");
            return;
        }

        if (Player == null)
        {
            Debug.LogError("[EnemySpawner] Player が未設定です");
            return;
        }

        /*
             Camera参照チェック（保持した Cam を使う）

             ・Start時に取れていない場合もあるので、ここでも拾い直す
        */
        if (Cam == null) Cam = Camera.main;
        if (Cam == null)
        {
            Debug.LogError("[EnemySpawner] Camera.main が取れません（MainCameraタグ確認）");
            return;
        }
        if (CamTr == null) CamTr = Cam.transform;

        SpawnRoutine = StartCoroutine(SpawnLoop());
    }

    //======================
    /// スポーン停止
    /// コルーチンを止めて、以降生成されなくする
    //======================
    public void StopSpawn()
    {
        if (SpawnRoutine == null) return;

        StopCoroutine(SpawnRoutine);
        SpawnRoutine = null;
    }

    //======================
    /// 指定した敵を消す（Spawner管理下の削除口）
    /// ・Listから外す
    /// ・Destroy（遅延も可）
    //======================
    public void KillSpawned(GameObject EnemyInstance, float Delay = 0f)
    {
        if (EnemyInstance == null) return;

        Spawned.Remove(EnemyInstance);

        if (Delay <= 0f) Destroy(EnemyInstance);
        if (Delay > 0f) Destroy(EnemyInstance, Delay);
    }

    //======================
    /// 生成済みの全ての敵を削除し、Listもクリアする
    /// タイトル戻りやリトライ時に呼び出す
    //======================
    public void ClearAllSpawned()
    {
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

    IEnumerator SpawnLoop()
    {
        /*
             while(true) + WaitForSeconds で「一定間隔で処理」を作る

             ・1回スポーン
             ・AppearanceTime秒待つ
             ・またスポーン
             を繰り返す
        */
        while (true)
        {
            /*
                 途中で参照が切れた時の保険（未設定はエラー）
            */
            if (EnemyPrefab == null)
            {
                Debug.LogError("[EnemySpawner] EnemyPrefab が未設定です（スポーンできません）");
                yield return new WaitForSeconds(1f);
                continue;
            }

            if (Player == null)
            {
                Debug.LogError("[EnemySpawner] Player が未設定です（スポーンできません）");
                yield return new WaitForSeconds(1f);
                continue;
            }

            /*
                 Camera参照の保険

                 ・シーン切替などで Cam が消える可能性がある
                 ・無ければ拾い直す
            */
            if (Cam == null) Cam = Camera.main;
            if (Cam == null)
            {
                Debug.LogError("[EnemySpawner] Camera.main が取れません（MainCameraタグ確認）");
                yield return new WaitForSeconds(1f);
                continue;
            }
            if (CamTr == null) CamTr = Cam.transform;

            Vector3 Pos = GetSpawnPosition(Cam, CamTr);

            GameObject Enemy = Instantiate(EnemyPrefab, Pos, Quaternion.identity);
            if (Enemy == null)
            {
                Debug.LogError("[EnemySpawner] Instantiateに失敗しました");
                yield return new WaitForSeconds(AppearanceTime);
                continue;
            }

            Spawned.Add(Enemy);

            PlayLaunchSE();

            /*
                 Spawnerを所有者として渡す（EnemyController側がSpawner経由で消せるようになる）

                 ・GetComponentInChildren(true)
                   子階層も探す / 非アクティブも対象
            */
            EnemyController Ec = Enemy.GetComponentInChildren<EnemyController>(true);
            if (Ec == null) Debug.LogError($"[EnemySpawner] Spawned '{Enemy.name}' に EnemyController がありません。Prefabに付けてください。");
            if (Ec != null) Ec.SetOwner(this, Enemy);

            yield return new WaitForSeconds(AppearanceTime);
        }
    }

    //================
    // SE
    //================

    void PlayLaunchSE()
    {
        /*
             SE未設定はエラー（ルール）
        */
        if (LaunchSE == null)
        {
            Debug.LogError("[EnemySpawner] LaunchSE が未設定です");
            return;
        }

        /*
             多重再生防止

             ・Time.time は timeScale=0 で止まる
             ・ポーズ中にSEを鳴らしたいなら unscaledTime を使う設計もあり
        */
        if (Time.time - LastPlayTime < MinInterval) return;

        LastPlayTime = Time.time;

        /*
             自身の位置参照を軽くするために Tr.position を使う
             transform.position 直参照を避ける
        */
        Vector3 SePos = Vector3.zero;
        if (Tr != null) SePos = Tr.position;

        if (SeSource == null)
        {
            Debug.LogError("[EnemySpawner] SeSource が未設定です（Startで確保できていない）");
            AudioSource.PlayClipAtPoint(LaunchSE, SePos, Volume);
            return;
        }

        SeSource.PlayOneShot(LaunchSE, Volume);
    }

    //================
    // List Cleanup
    //================

    void CleanupList()
    {
        /*
             後ろから消す
             ・RemoveAtで詰まるので、前から消すと飛ばしが起きる
        */
        for (int i = Spawned.Count - 1; i >= 0; i--)
        {
            if (Spawned[i] == null) Spawned.RemoveAt(i);
        }
    }

    //================
    // Spawn Position
    //================

    Vector3 GetSpawnPosition(Camera Cam, Transform CamTransform)
    {
        /*
             画面サイズ
             ・orthographicSize = 高さの半分
             ・aspect を掛けると横幅の半分になる
        */
        float h = Cam.orthographicSize;
        float w = h * Cam.aspect;

        /*
             カメラ中心X
             ・ここを基準に左右にランダム
        */
        float cx = CamTransform.position.x;

        /*
             X：画面内ランダム
             Y：プレイヤーより上にランダム
        */
        float x = Random.Range(cx - w, cx + w);
        float y = Player.position.y + Random.Range(0f, h);

        return new Vector3(x, y, 0f);
    }
}
