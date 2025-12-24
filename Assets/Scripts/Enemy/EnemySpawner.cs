using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵を一定時間ごとに生成・管理するクラス
/// ・スポーン開始 / 停止が可能
/// ・生成した敵をリストで保持
/// ・ゲームオーバーやリトライ時の制御に対応
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public float appearanceTime = 3f;     // 敵を生成する間隔（秒）
    public GameObject enemyPrefab;        // 生成する敵Prefab
    public Transform player;              // スポーン位置計算用（プレイヤー基準）

    // 現在スポーン中の敵インスタンス一覧
    readonly List<GameObject> spawned = new List<GameObject>();

    // スポーン処理用コルーチン
    Coroutine spawnRoutine;

    // ======================
    // Spawn SE
    // ======================
    [Header("Spawn SE")]
    [Tooltip("未設定ならこのオブジェクトから自動取得（無ければ自動追加）")]
    public AudioSource seSource;

    [Tooltip("敵が登場した瞬間に鳴らすSE")]
    public AudioClip LaunchSE;

    [Range(0f, 1f)]
    public float volume = 1.0f;

    [Header("Limiter")]
    [Tooltip("この秒数以内の連続再生は無視（多重防止）")]
    public float minInterval = 0.05f;

    float lastPlayTime = -999f;

    /// <summary>
    /// 初期化処理
    /// AudioSourceが未設定の場合は自動で取得または追加し、SE再生用の設定を行う
    /// </summary>
    void Start()
    {
        // AudioSource 自動取得（無ければ追加）
        if (seSource == null)
            seSource = GetComponent<AudioSource>();

        if (seSource == null)
            seSource = gameObject.AddComponent<AudioSource>();

        seSource.playOnAwake = false;
        seSource.loop = false;
        seSource.spatialBlend = 0f; // 2D音（UI/ゲーム共通で聞こえる）
    }

    /// <summary>
    /// 毎フレーム呼ばれる
    /// Destroyされた敵がリストに残らないようにリストをクリーンアップする
    /// </summary>
    void Update()
    {
        // Destroyされた敵がリストに残らないよう毎フレーム掃除
        CleanupList();
    }

    // =========================================================
    // 外から受け取る用
    // =========================================================

    /// <summary>
    /// 現在生成されている敵一覧を取得（読み取り専用）
    /// 他クラスから敵の参照や距離判定などに利用できる
    /// </summary>
    public IReadOnlyList<GameObject> GetSpawnedEnemies()
    {
        return spawned;
    }

    /// <summary>
    /// 敵のスポーンを開始する
    /// 既にスポーン中の場合は何もしない
    /// 必要な参照が揃っているかチェックし、問題なければコルーチンでスポーンループを開始する
    /// </summary>
    public void StartSpawn()
    {
        // すでに動いていたら二重起動しない
        if (spawnRoutine != null) return;

        // 参照チェック（落ちる原因を事前に潰す）
        if (enemyPrefab == null)
        {
            Debug.LogError("[Spawner] enemyPrefab が未設定です");
            return;
        }
        if (player == null)
        {
            Debug.LogError("[Spawner] player が未設定です");
            return;
        }
        if (Camera.main == null)
        {
            Debug.LogError("[Spawner] Camera.main が取れません（MainCameraタグ確認）");
            return;
        }

        // スポーンループ開始
        spawnRoutine = StartCoroutine(SpawnLoop());
        Debug.Log("[Spawner] StartSpawn");
    }

    /// <summary>
    /// 敵のスポーンを停止する
    /// スポーン中のコルーチンを停止し、以降敵が生成されなくなる
    /// </summary>
    public void StopSpawn()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
            Debug.Log("[Spawner] StopSpawn");
        }
    }

    // =========================================================
    // スポーン処理本体
    // =========================================================

    /// <summary>
    /// 敵を一定間隔で生成し続けるコルーチン
    /// 参照切れやPrefab未設定時は一時停止し、復帰を待つ
    /// 生成した敵はリストに追加し、SE再生や所有者設定も行う
    /// </summary>
    IEnumerator SpawnLoop()
    {
        int loop = 0;

        while (true)
        {
            loop++;

            // 途中で参照が切れた場合の保険
            if (enemyPrefab == null || player == null || Camera.main == null)
            {
                Debug.LogWarning($"[Spawner] Loop={loop} missing refs. wait...");
                yield return new WaitForSeconds(1f);
                continue;
            }

            // スポーン位置を計算
            Vector3 pos = GetSpawnPosition(Camera.main);

            // 敵生成
            GameObject e = Instantiate(enemyPrefab, pos, Quaternion.identity);

            // 管理リストに追加
            spawned.Add(e);

            // 敵登場SE（生成した瞬間に鳴らす）
            PlayLaunchSE();

            // EnemyController があれば Spawner を所有者として渡す
            var ec = e.GetComponentInChildren<EnemyController>(true);
            if (ec != null)
                ec.SetOwner(this, e);
            else
                Debug.LogError($"[Spawner] Spawned '{e.name}' has NO EnemyController. Prefabに付けてください。");

            // 次の生成まで待つ
            yield return new WaitForSeconds(appearanceTime);
        }
    }

    /// <summary>
    /// 敵登場時のSEを再生する
    /// 多重再生防止のため、一定間隔未満では再生しない
    /// AudioSourceが未設定の場合は一時的にPlayClipAtPointで再生
    /// </summary>
    void PlayLaunchSE()
    {
        if (LaunchSE == null) return;

        // 多重再生防止
        if (Time.time - lastPlayTime < minInterval)
            return;

        lastPlayTime = Time.time;

        if (seSource == null)
        {
            // 念のため（基本Startで確保される）
            AudioSource.PlayClipAtPoint(LaunchSE, transform.position, volume);
            return;
        }

        seSource.PlayOneShot(LaunchSE, volume);
    }

    // =========================================================
    // 敵の削除・クリーンアップ
    // =========================================================

    /// <summary>
    /// 指定した敵インスタンスをリストから削除し、Destroyする
    /// 遅延時間を指定可能
    /// </summary>
    public void KillSpawned(GameObject enemyInstance, float delay = 0f)
    {
        if (enemyInstance == null) return;

        // 管理リストから外す
        spawned.Remove(enemyInstance);

        // 即時 or 遅延Destroy
        if (delay <= 0f) Destroy(enemyInstance);
        else Destroy(enemyInstance, delay);
    }

    /// <summary>
    /// 生成済みの全ての敵を削除し、リストもクリアする
    /// タイトル戻りやリトライ時に呼び出す
    /// </summary>
    public void ClearAllSpawned()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] != null)
                Destroy(spawned[i]);
        }
        spawned.Clear();
    }

    /// <summary>
    /// Destroy済みの敵オブジェクトをリストから除去する
    /// Updateで毎フレーム呼ばれる
    /// </summary>
    void CleanupList()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] == null)
                spawned.RemoveAt(i);
        }
    }

    // =========================================================
    // スポーン位置計算
    // =========================================================

    /// <summary>
    /// カメラ範囲内のランダムな位置を計算して返す
    /// X座標は画面幅内ランダム、Y座標はプレイヤーより上側でランダム
    /// </summary>
    Vector3 GetSpawnPosition(Camera cam)
    {
        float h = cam.orthographicSize;
        float w = h * cam.aspect;

        float cx = cam.transform.position.x;

        float x = Random.Range(cx - w, cx + w);
        float y = player.position.y + Random.Range(0f, h);

        return new Vector3(x, y, 0f);
    }
}
