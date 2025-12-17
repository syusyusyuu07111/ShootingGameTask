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
    public float appearanceTime = 3f;   // 敵を生成する間隔（秒）
    public GameObject enemyPrefab;       // 生成する敵Prefab
    public Transform player;             // スポーン位置計算用（プレイヤー基準）

    // 現在スポーン中の敵インスタンス一覧
    readonly List<GameObject> spawned = new List<GameObject>();

    // スポーン処理用コルーチン
    Coroutine spawnRoutine;

    void Update()
    {
        // Destroyされた敵がリストに残らないよう毎フレーム掃除
        CleanupList();
    }

    // =========================================================
    // 外から受け取る用
    // =========================================================

    /// <summary>
    /// 現在生成されている敵一覧を取得（読み取り）
    /// PlayerDie などから距離判定に使用
    /// </summary>
    public IReadOnlyList<GameObject> GetSpawnedEnemies()
    {
        return spawned;
    }

    /// <summary>
    /// 敵のスポーンを開始する
    /// タイトル → ゲーム開始時 / リトライ時に呼ばれる
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
    /// ポーズ / ゲームオーバー / タイトル遷移時に使用
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
    /// 敵を一定間隔で生成し続けるループ
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

            // EnemyController があれば Spawner を所有者として渡す
            // → Destroy時に Spawner 巻き込みを防ぐ
            var ec = e.GetComponentInChildren<EnemyController>(true);
            if (ec != null)
                ec.SetOwner(this, e);
            else
                Debug.LogError(
                    $"[Spawner] Spawned '{e.name}' has NO EnemyController. Prefabに付けてください。"
                );

            // 次の生成まで待つ
            yield return new WaitForSeconds(appearanceTime);
        }
    }

    // =========================================================
    // 敵の削除・クリーンアップ
    // =========================================================

    /// <summary>
    /// 指定した敵インスタンスをSpawner管理下から削除してDestroy
    /// EnemyController から呼ばれる
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
    /// 生成済みの敵をすべて削除
    /// タイトル戻り・リトライ時に使用
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
    /// Destroy済みオブジェクトをリストから除去
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
    /// カメラ範囲内のランダム位置に敵を出す
    /// ・X：画面幅内ランダム
    /// ・Y：プレイヤーより少し上+ランダム
    /// </summary>
    Vector3 GetSpawnPosition(Camera cam)
    {
        // カメラの表示範囲サイズ
        float h = cam.orthographicSize;
        float w = h * cam.aspect;

        // カメラ中心X
        float cx = cam.transform.position.x;

        // Xは画面内ランダム
        float x = Random.Range(cx - w, cx + w);

        // Yはプレイヤーより上
        float y = player.position.y + Random.Range(0f, h);

        return new Vector3(x, y, 0f);
    }
}
