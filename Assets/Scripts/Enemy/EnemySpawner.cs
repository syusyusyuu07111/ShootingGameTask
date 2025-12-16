using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public float appearanceTime = 3.0f;

    [Tooltip("ProjectビューのPrefab（青アイコン）を入れてください。HierarchyのオブジェクトはNG。")]
    public GameObject enemyPrefab;

    public Transform player;

    readonly List<GameObject> spawned = new List<GameObject>();
    Coroutine routine;

    void OnEnable()
    {
        Debug.Log($"[Spawner] OnEnable active={gameObject.activeInHierarchy} enabled={enabled}");
    }

    void OnDisable()
    {
        Debug.Log($"[Spawner] OnDisable active={gameObject.activeInHierarchy} enabled={enabled}");
    }

    void OnDestroy()
    {
        Debug.Log("[Spawner] OnDestroy");
    }

    void Start()
    {
        Debug.Log($"[Spawner] Start time={Time.time:F2} timeScale={Time.timeScale} " +
                  $"enemyPrefab={(enemyPrefab ? enemyPrefab.name : "null")} " +
                  $"prefabSceneValid={(enemyPrefab != null && enemyPrefab.scene.IsValid())}");

        if (enemyPrefab != null && enemyPrefab.scene.IsValid())
        {
            Debug.LogError("[Spawner] enemyPrefab が Hierarchy上のオブジェクトです。ProjectビューのPrefab（青アイコン）に差し替えてください。");
        }

        routine ??= StartCoroutine(SpawnLoop());
    }

    void Update()
    {
        CleanupList();
    }

    IEnumerator SpawnLoop()
    {
        int loop = 0;

        while (true)
        {
            loop++;

            Debug.Log($"[Spawner] Loop={loop} time={Time.time:F2} timeScale={Time.timeScale} " +
                      $"active={gameObject.activeInHierarchy} enabled={enabled}");

            if (enemyPrefab == null)
            {
                Debug.LogError($"[Spawner] Loop={loop} enemyPrefab is NULL !!! " +
                               $"(Hierarchy参照をDestroyしてnull化してる可能性大)");
                yield return new WaitForSeconds(1f);
                continue;
            }

            if (enemyPrefab.scene.IsValid())
            {
                Debug.LogError($"[Spawner] Loop={loop} enemyPrefab is SCENE OBJECT (Hierarchy参照) name={enemyPrefab.name}. " +
                               $"→ ProjectビューのPrefab（青）に差し替えてください。");
            }

            if (player == null)
            {
                Debug.LogError($"[Spawner] Loop={loop} player is NULL (Inspectorで設定してください)");
                yield return new WaitForSeconds(1f);
                continue;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError($"[Spawner] Loop={loop} Camera.main is NULL (MainCameraタグを確認)");
                yield return new WaitForSeconds(1f);
                continue;
            }

            Vector3 pos = GetSpawnPosition(cam);
            Debug.Log($"[Spawner] Loop={loop} Instantiate at {pos}");

            GameObject e = Instantiate(enemyPrefab, pos, Quaternion.identity);
            spawned.Add(e);

            // 生成した個体を EnemyController に渡す
            var ec = e.GetComponentInChildren<EnemyController>(true);
            if (ec != null)
                ec.SetOwner(this, e);
            else
                Debug.LogWarning($"[Spawner] EnemyController が見つかりません: {e.name}");

            Debug.Log($"[Spawner] Loop={loop} Spawned name={e.name} id={e.GetInstanceID()} total={spawned.Count}");

            Debug.Log($"[Spawner] Loop={loop} WaitForSeconds({appearanceTime})");
            yield return new WaitForSeconds(appearanceTime);

            Debug.Log($"[Spawner] Loop={loop} AfterWait time={Time.time:F2}");
        }
    }

    // ★EnemyControllerが呼ぶ：リストから外して、その個体だけ消す
    public void KillSpawned(GameObject enemyInstance, float delay = 0f)
    {
        if (enemyInstance == null) return;

        spawned.Remove(enemyInstance);

        if (delay <= 0f) Destroy(enemyInstance);
        else Destroy(enemyInstance, delay);
    }

    void CleanupList()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] == null) spawned.RemoveAt(i);
        }
    }

    Vector3 GetSpawnPosition(Camera cam)
    {
        float height = cam.orthographicSize;
        float width = height * cam.aspect;

        float cx = cam.transform.position.x;

        float x = Random.Range(cx - width, cx + width);
        float y = player.position.y + Random.Range(0f, height);

        return new Vector3(x, y, 0f);
    }
}
