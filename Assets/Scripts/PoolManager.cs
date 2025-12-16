using UnityEngine;
using UnityEngine.Pool;

public class PoolManager : MonoBehaviour
{
    [Header("Pool Target")]
    public GameObject Prefab;

    [Header("Move Settings")]
    public float Speed = 5f;

    [Header("Life")]
    public float LifeTime = 2f;

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

        // 弾リスト
        var bullet = obj.GetComponent<Bullet>();
        if (bullet == null) bullet = obj.AddComponent<Bullet>();

        // 寿命
        var destroyer = obj.GetComponent<Destroyer>();
        if (destroyer == null) destroyer = obj.AddComponent<Destroyer>();
        destroyer.PoolManager = this;

        // 移動
        var move = obj.GetComponent<BulletMove>();
        if (move == null) move = obj.GetComponentInChildren<BulletMove>(true);
        if (move == null) move = obj.AddComponent<BulletMove>();
        move.Speed = Speed;

        return obj;
    }

    void OnGetFromPool(GameObject obj)
    {
        obj.SetActive(true);

        // Speed 再反映
        var move = obj.GetComponent<BulletMove>();
        if (move == null) move = obj.GetComponentInChildren<BulletMove>(true);
        if (move == null) move = obj.AddComponent<BulletMove>();
        move.Speed = Speed;

        // Destroyer 再設定
        var destroyer = obj.GetComponent<Destroyer>();
        if (destroyer == null) destroyer = obj.AddComponent<Destroyer>();
        destroyer.PoolManager = this;

        // 寿命スタート
        destroyer.StartDestroyTimer(LifeTime);
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
