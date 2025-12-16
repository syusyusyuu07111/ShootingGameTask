using UnityEngine;
using UnityEngine.Pool;

public class PoolManager : MonoBehaviour
{
    [Header("Pool Target")]
    public GameObject Prefab;

    [Header("Move Settings")]
    public float Speed = 5f;

    ObjectPool<GameObject> pool;

    void Awake()
    {
        pool = new ObjectPool<GameObject>(
            createFunc: OnCreatePooledObject,
            actionOnGet: OnGetFromPool,
            actionOnRelease: OnReleaseToPool,
            actionOnDestroy: OnDestroyPooledObject,
            collectionCheck: false,
            defaultCapacity: 10,
            maxSize: 100
        );
    }

    GameObject OnCreatePooledObject()
    {
        var obj = Instantiate(Prefab);
        obj.SetActive(false);

        // 生成物に BulletMove が無ければ必ず付ける（ルート→無ければ子も探す）
        var move = obj.GetComponent<BulletMove>();
        if (move == null) move = obj.GetComponentInChildren<BulletMove>(true);
        if (move == null) move = obj.AddComponent<BulletMove>();

        move.Speed = Speed;

        return obj;
    }

    void OnGetFromPool(GameObject obj)
    {
        obj.SetActive(true);

        // 取り出し時にも Speed を反映
        var move = obj.GetComponent<BulletMove>();
        if (move == null) move = obj.GetComponentInChildren<BulletMove>(true);

        if (move == null)
        {
            // 念のため：もし無いならここでも付ける
            move = obj.AddComponent<BulletMove>();
        }

        move.Speed = Speed;

        // デバッグ：本当に付いてるか確認したい時だけON
        // Debug.Log($"[Pool] Get: {obj.name}, BulletMove={move != null}, Speed={move.Speed}");
    }

    void OnReleaseToPool(GameObject obj)
    {
        obj.SetActive(false);
    }

    void OnDestroyPooledObject(GameObject obj)
    {
        Destroy(obj);
    }

    public GameObject GetGameObject(Vector3 position, Quaternion rotation)
    {
        var obj = pool.Get();
        obj.transform.SetPositionAndRotation(position, rotation);
        return obj;
    }

    public void ReleaseGameObject(GameObject obj)
    {
        pool.Release(obj);
    }
}
