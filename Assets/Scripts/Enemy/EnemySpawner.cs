using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public float appearanceTime = 3f;
    public GameObject enemyPrefab;  // ProjectビューのPrefab（青）
    public Transform player;

    readonly List<GameObject> spawned = new List<GameObject>();
    Coroutine spawnRoutine;

    void Update()
    {
        CleanupList();
    }

    // ★外部から参照する用（PlayerDieなど）
    public IReadOnlyList<GameObject> GetSpawnedEnemies()
    {
        return spawned;
    }

    // ★外部から開始/停止できるようにする（重要）
    public void StartSpawn()
    {
        if (spawnRoutine != null) return;

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

        spawnRoutine = StartCoroutine(SpawnLoop());
        Debug.Log("[Spawner] StartSpawn");
    }

    public void StopSpawn()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
            Debug.Log("[Spawner] StopSpawn");
        }
    }

    IEnumerator SpawnLoop()
    {
        int loop = 0;

        while (true)
        {
            loop++;

            // 途中で参照が切れた時の保険
            if (enemyPrefab == null || player == null || Camera.main == null)
            {
                Debug.LogWarning($"[Spawner] Loop={loop} missing refs. wait...");
                yield return new WaitForSeconds(1f);
                continue;
            }

            Vector3 pos = GetSpawnPosition(Camera.main);

            GameObject e = Instantiate(enemyPrefab, pos, Quaternion.identity);
            spawned.Add(e);

            // EnemyControllerがあるならOwnerを渡す（Destroy巻き込み防止）
            var ec = e.GetComponentInChildren<EnemyController>(true);
            if (ec != null)
                ec.SetOwner(this, e);
            else
                Debug.LogError($"[Spawner] Spawned '{e.name}' has NO EnemyController. Prefabに付けてください。");

            // Debug.Log($"[Spawner] Spawned loop={loop} enemy='{e.name}' total={spawned.Count}");

            yield return new WaitForSeconds(appearanceTime);
        }
    }

    public void KillSpawned(GameObject enemyInstance, float delay = 0f)
    {
        if (enemyInstance == null) return;

        spawned.Remove(enemyInstance);

        if (delay <= 0f) Destroy(enemyInstance);
        else Destroy(enemyInstance, delay);
    }

    public void ClearAllSpawned()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] != null) Destroy(spawned[i]);
        }
        spawned.Clear();
    }

    void CleanupList()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
            if (spawned[i] == null) spawned.RemoveAt(i);
    }

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
