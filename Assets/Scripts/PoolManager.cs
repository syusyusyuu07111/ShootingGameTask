using UnityEngine;
using UnityEngine.Pool;

public class PoolManager : MonoBehaviour
{
    public GameObject Prefab;   // Pool ÇÃëŒè€Prefab

    ObjectPool<GameObject> pool;

    void Awake()
    {
        pool = new ObjectPool<GameObject>(
            OnCreatePooledObject,
            OnGetFromPool,
            OnReleaseToPool,
            OnDestroyPooledObject
        );
    }

    GameObject OnCreatePooledObject()
    {
        return Instantiate(Prefab);
    }

    void OnGetFromPool(GameObject obj)
    {
        obj.SetActive(true);
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
        GameObject obj = pool.Get();
        Transform tf = obj.transform;
        tf.position = position;
        tf.rotation = rotation;
        return obj;
    }

    public void ReleaseGameObject(GameObject obj)
    {
        pool.Release(obj);
    }
}
