using UnityEngine;
using UnityEngine.Pool;

/*
     GameObject（主に弾）を再利用するためのプール管理クラス
     ・弾の生成 / 再利用を一元管理する
     ・弾の寿命（Destroyer）を一元管理する
     ・弾の移動速度（BulletMove）を一元管理する
*/
public class PoolManager : MonoBehaviour
{
    //================
    // Pool Target
    //================

    [SerializeField] private GameObject Prefab;    // プールする元となるPrefab（弾）

    //================
    // Move Settings
    //================

    [SerializeField] private float Speed = 5f;     // 弾の移動速度

    //================
    // Life
    //================

    [SerializeField] private float LifeTime = 2f;  // 弾の寿命（秒）

    //================
    // Pool
    //================

    ObjectPool<GameObject> Pool;

    //================
    // Unity Event
    //================

    void Awake()
    {
        /*
             必須参照チェック
        */
        if (Prefab == null) Debug.LogError("[PoolManager] Prefab が未設定です（プールするPrefabを設定してください）");

        /*
             ObjectPool を生成する
             ・生成 / 取得 / 返却 / 破棄 の処理を登録する
        */
        Pool = new ObjectPool<GameObject>(
            createFunc: OnCreatePooledObject,
            actionOnGet: OnGetFromPool,
            actionOnRelease: OnReleaseToPool,
            actionOnDestroy: OnDestroyPooledObject,
            collectionCheck: false,
            defaultCapacity: 10,
            maxSize: 100
        );
    }

    //================
    // プール内部処理
    //================

    GameObject OnCreatePooledObject()
    {
        /*
             Prefab から弾を生成して、非アクティブで保持する
        */
        if (Prefab == null) return null;

        GameObject Obj = Instantiate(Prefab);
        Obj.SetActive(false);

        /*
             Bullet：存在管理（距離判定など）用
        */
        Bullet Bt = Obj.GetComponent<Bullet>();
        if (Bt == null) Bt = Obj.AddComponent<Bullet>();

        /*
             Destroyer：寿命管理用
        */
        Destroyer Ds = Obj.GetComponent<Destroyer>();
        if (Ds == null) Ds = Obj.AddComponent<Destroyer>();
        Ds.PoolManager = this;

        /*
             BulletMove：移動処理用
             Prefabの子に付いている可能性もあるので children も探す
        */
        BulletMove Bm = GetOrAddBulletMove(Obj);
        Bm.Speed = Speed;

        return Obj;
    }

    void OnGetFromPool(GameObject Obj)
    {
        /*
             取得時に毎回リセットが必要な内容を設定する
        */
        if (Obj == null) return;

        // オブジェクトを有効化（OnEnableが呼ばれる）
        Obj.SetActive(true);

        /*
             移動速度を反映する
        */
        BulletMove Bm = GetOrAddBulletMove(Obj);
        Bm.Speed = Speed;

        /*
             Destroyer を設定して寿命タイマーを開始する
        */
        Destroyer Ds = Obj.GetComponent<Destroyer>();
        if (Ds == null) Ds = Obj.AddComponent<Destroyer>();
        Ds.PoolManager = this;

        Ds.StartDestroyTimer(LifeTime);
    }

    void OnReleaseToPool(GameObject Obj)
    {
        /*
             返却時は非アクティブ化する
        */
        if (Obj == null) return;

        Obj.SetActive(false);
    }

    void OnDestroyPooledObject(GameObject Obj)
    {
        /*
             プール上限超過などで完全破棄される時
        */
        if (Obj == null) return;

        Destroy(Obj);
    }

    //================
    // コンポーネント取得補助
    //================

    BulletMove GetOrAddBulletMove(GameObject Obj)
    {
        /*
             BulletMove を取得する
             ・同階層に無ければ子階層（非アクティブ含む）も探す
             ・それでも無ければ追加する
        */
        BulletMove Bm = Obj.GetComponent<BulletMove>();
        if (Bm != null) return Bm;

        Bm = Obj.GetComponentInChildren<BulletMove>(true);
        if (Bm != null) return Bm;

        return Obj.AddComponent<BulletMove>();
    }

    //================
    // 外部から利用するAPI
    //================

    public GameObject GetGameObject(Vector3 Position, Quaternion Rotation)
    {
        /*
             プールから取得して指定位置・回転で配置する
        */
        if (Pool == null) { Debug.LogError("[PoolManager] Pool が未生成です（Awakeが呼ばれているか確認してください）"); return null; }

        GameObject Obj = Pool.Get();
        if (Obj == null) { Debug.LogError("[PoolManager] Pool.Get() が null を返しました（Prefab未設定の可能性）"); return null; }

        Transform Tr = Obj.transform;
        Tr.SetPositionAndRotation(Position, Rotation);

        return Obj;
    }

    public void ReleaseGameObject(GameObject Obj)
    {
        /*
             Destroyer から呼ばれてプールに戻す
        */
        if (Pool == null) { Debug.LogError("[PoolManager] Pool が未生成です（Awakeが呼ばれているか確認してください）"); return; }
        if (Obj == null) return;

        Pool.Release(Obj);
    }
}
