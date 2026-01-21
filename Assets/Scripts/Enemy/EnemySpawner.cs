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
    [Header("Spawn Settings")]
    public float AppearanceTime = 3f;        // 敵を生成する間隔
    public GameObject EnemyPrefab;           // 生成する敵Prefab
    public Transform Player;                 // スポーン位置計算用（プレイヤー基準）

    /*
    生成した敵インスタンス一覧

    ・List<GameObject> に「生成した敵の参照」を保持する
    ・あとで「全削除」「特定敵の削除」「敵一覧参照」に使う
    */
    readonly List<GameObject> Spawned = new List<GameObject>();

    /*
    スポーン処理用コルーチン

    ・StartCoroutine の戻り値を保存しておくと StopCoroutine できる
    ・spawnRoutine が null かどうかで「いまスポーン中？」が分かる
    */
    Coroutine SpawnRoutine;

    [Header("Spawn SE")]
    [Tooltip("未設定ならこのオブジェクトから自動取得（無ければ自動追加）")]
    public AudioSource SeSource;

    [Tooltip("敵が登場した瞬間に鳴らすSE")]
    public AudioClip LaunchSE;

    [Range(0f, 1f)]
    public float Volume = 1.0f;

    [Header("Limiter")]
    [Tooltip("この秒数以内の連続再生は無視（多重防止）")]
    public float MinInterval = 0.05f;

    float LastPlayTime = -999f;

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

        if (Camera.main == null)
        {
            Debug.LogError("[EnemySpawner] Camera.main が取れません（MainCameraタグ確認）");
            return;
        }

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

    IEnumerator SpawnLoop()
    {
        /*
        while(true) + WaitForSeconds で「一定間隔で処理」を作る

        ・1回スポーン
        ・appearanceTime秒待つ
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

            if (Camera.main == null)
            {
                Debug.LogError("[EnemySpawner] Camera.main が取れません（MainCameraタグ確認）");
                yield return new WaitForSeconds(1f);
                continue;
            }

            Vector3 Pos = GetSpawnPosition(Camera.main);

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
            var ec = Enemy.GetComponentInChildren<EnemyController>(true);
            if (ec == null)
            {
                Debug.LogError($"[EnemySpawner] Spawned '{Enemy.name}' に EnemyController がありません。Prefabに付けてください。");
            }
            if (ec != null) ec.SetOwner(this, Enemy);

            yield return new WaitForSeconds(AppearanceTime);
        }
    }

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

        if (SeSource == null)
        {
            Debug.LogError("[EnemySpawner] SeSource が未設定です（Startで確保できていない）");
            AudioSource.PlayClipAtPoint(LaunchSE, transform.position, Volume);
            return;
        }

        SeSource.PlayOneShot(LaunchSE, Volume);
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

    Vector3 GetSpawnPosition(Camera Cam)
    {
        float h = Cam.orthographicSize;
        float w = h * Cam.aspect;

        float cx = Cam.transform.position.x;

        float x = Random.Range(cx - w, cx + w);
        float y = Player.position.y + Random.Range(0f, h);

        return new Vector3(x, y, 0f);
    }
}
