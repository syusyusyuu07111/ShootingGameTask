using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public float appearanceTime = 3f;
    public GameObject enemyPrefab;      // ProjectビューのPrefab（青）
    public Transform player;

    readonly List<GameObject> spawned = new List<GameObject>();

    void Awake()
    {
        Debug.Log($"[Spawner] Awake obj='{gameObject.name}' scene='{gameObject.scene.name}'");
    }

    void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        int loop = 0;

        while (true)
        {
            loop++;

            if (enemyPrefab == null)
            {
                Debug.LogError($"[Spawner] Loop={loop} enemyPrefab is NULL (ProjectのPrefabを入れてください)");
                yield return new WaitForSeconds(1f);
                continue;
            }

            if (player == null || Camera.main == null)
            {
                Debug.LogError($"[Spawner] Loop={loop} player or Camera.main is NULL");
                yield return new WaitForSeconds(1f);
                continue;
            }

            Vector3 pos = GetSpawnPosition(Camera.main);

            GameObject e = Instantiate(enemyPrefab, pos, Quaternion.identity);
            spawned.Add(e);

            // スポーン個体に EnemyController が居るか確認して紐付ける
            var ec = e.GetComponentInChildren<EnemyController>(true);
            if (ec == null)
            {
                Debug.LogError($"[Spawner] ERROR: Spawned enemy has NO EnemyController! prefab={enemyPrefab.name} instance={e.name}");
            }
            else
            {
                ec.SetOwner(this, e);
                Debug.Log($"[Spawner] OK: Spawned '{e.name}' rootPos={e.transform.position} ctrlObj='{ec.gameObject.name}' ctrlPos={ec.transform.position}");
            }

            Debug.Log($"[Spawner] Spawned loop={loop} enemy='{e.name}' total={spawned.Count}");

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

    void CleanupList()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
            if (spawned[i] == null) spawned.RemoveAt(i);
    }

    void Update()
    {
        CleanupList();
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
