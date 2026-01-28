using UnityEngine;
using UnityEngine.Pool;

/*
     GameObject（主に弾）を再利用するためのプール管理クラス

     【主な役割】
     ・弾Prefabをプールして、生成/再利用を一元管理する
     ・弾の寿命（Destroyer）を一元管理する
     ・弾の移動速度（BulletMove）を一元管理する

     【設計方針】
     ・GetComponent/AddComponent を毎回しない（生成時に1回だけ確保してキャッシュ）
     ・Prefab未設定は致命なのでAwakeでエラーを出し、プールを生成しない
     ・OnGet は「有効化と寿命開始」に集中させる
*/
public sealed class PoolManager : MonoBehaviour
{
    //================
    // Pool Target
    //================

    [SerializeField] private GameObject Prefab;


    //================
    // Move Settings
    //================

    [SerializeField] private float Speed = 5f;


    //================
    // Life
    //================

    [SerializeField] private float LifeTime = 2f;


    //================
    // Pool Settings
    //================

    [Header("Pool Size")]
    [SerializeField] private int DefaultCapacity = 10;

    [SerializeField] private int MaxSize = 100;


    //================
    // Pool
    //================

    private ObjectPool<GameObject> Pool;


    //================
    // Unity Event
    //================

    private void Awake()
    {
        //================
        // Validate
        //================

        /*
             Prefab未設定で動かないように

             ・プールはPrefabが無いと動かない
             ・nullを返すプールは事故の元なので、プール自体を作らない
        */
        if (Prefab == null)
        {
            Debug.LogError("[PoolManager] Prefab が未設定です（プールするPrefabを設定してください）");
            return;
        }

        if (DefaultCapacity <= 0)
        {
            Debug.LogError($"[PoolManager] DefaultCapacity が不正です value={DefaultCapacity}");
            DefaultCapacity = 1;
        }

        if (MaxSize < DefaultCapacity)
        {
            Debug.LogError($"[PoolManager] MaxSize が不正です max={MaxSize} default={DefaultCapacity}");
            MaxSize = DefaultCapacity;
        }

        //================
        // Pool Create
        //================

        /*
             ObjectPool を生成する

             createFunc        : 新規生成時の処理
             actionOnGet       : 取得時の処理
             actionOnRelease   : 返却時の処理
             actionOnDestroy   : 完全破棄時の処理
        */
        Pool = new ObjectPool<GameObject>(
            createFunc: CreatePooledObject,
            actionOnGet: OnGetFromPool,
            actionOnRelease: OnReleaseToPool,
            actionOnDestroy: OnDestroyPooledObject,
            collectionCheck: false,
            defaultCapacity: DefaultCapacity,
            maxSize: MaxSize
        );
    }


    //================
    // Pool Internal
    //================

    private GameObject CreatePooledObject()
    {
        /*
             Prefabから生成してプール用に初期化する

             ・必要コンポーネントを必ず揃える
             ・以後は GetComponent を繰り返さず、キャッシュを使う
        */
        GameObject Obj = Instantiate(Prefab);
        Obj.SetActive(false);

        /*
             このオブジェクトが「プール弾」であることを保証する

             ・PooledBulletが各コンポーネント参照をキャッシュする
             ・PoolManagerは以後、PooledBulletだけ触れば良い
        */
        PooledBullet Pb = Obj.GetComponent<PooledBullet>();
        if (Pb == null) Pb = Obj.AddComponent<PooledBullet>();

        Pb.Setup(this, Speed);

        return Obj;
    }

    private void OnGetFromPool(GameObject Obj)
    {
        /*
             取得時の処理

             ・速度を最新値で反映する（Inspector変更に追従）
             ・有効化する（OnEnableが呼ばれる）
             ・寿命タイマーを開始する
        */
        if (Obj == null) return;

        PooledBullet Pb = Obj.GetComponent<PooledBullet>();
        if (Pb == null)
        {
            Debug.LogError("[PoolManager] PooledBullet がありません（Prefab構成が想定と違います）");
            return;
        }

        Pb.ApplySpeed(Speed);

        Obj.SetActive(true);

        Pb.StartLifeTimer(LifeTime);
    }

    private void OnReleaseToPool(GameObject Obj)
    {
        /*
             返却時の処理

             ・非アクティブ化してシーンから消す
             ・OnDisableが呼ばれ、Bulletなどの一覧から外れる
        */
        if (Obj == null) return;

        Obj.SetActive(false);
    }

    private void OnDestroyPooledObject(GameObject Obj)
    {
        /*
             プール上限超過などで完全破棄される時
        */
        if (Obj == null) return;

        Destroy(Obj);
    }


    //================
    // Public API
    //================

    public GameObject GetGameObject(Vector3 Position, Quaternion Rotation)
    {
        /*
             プールから取得して指定位置・回転で配置する
        */
        if (Pool == null)
        {
            Debug.LogError("[PoolManager] Pool が未生成です（Prefab未設定 / Awake未実行の可能性）");
            return null;
        }

        GameObject Obj = Pool.Get();
        if (Obj == null)
        {
            Debug.LogError("[PoolManager] Pool.Get() が null を返しました");
            return null;
        }

        Transform Tr = Obj.transform;
        Tr.SetPositionAndRotation(Position, Rotation);

        return Obj;
    }

    public void ReleaseGameObject(GameObject Obj)
    {
        /*
             Destroyer などから呼ばれてプールに戻す
        */
        if (Pool == null)
        {
            Debug.LogError("[PoolManager] Pool が未生成です（Prefab未設定 / Awake未実行の可能性）");
            return;
        }

        if (Obj == null) return;

        Pool.Release(Obj);
    }


    //================
    // Nested: PooledBullet
    //================

    /*
         プール対象（弾）に必要な参照をまとめて持つコンポーネント

         【目的】
         ・PoolManager側でGetComponentを繰り返さない
         ・弾の構成（Bullet/Destroyer/BulletMove）を1箇所で保証する
    */
    private sealed class PooledBullet : MonoBehaviour
    {
        // 必須参照
        private PoolManager OwnerPool;
        private Destroyer Destroyer;
        private BulletMove BulletMove;

        /*
             初期化（生成時に1回だけ呼ぶ）
        */
        public void Setup(PoolManager Pool, float MoveSpeed)
        {
            OwnerPool = Pool;

            // Bullet：存在管理（EnemyControllerが参照する）用
            Bullet Bt = GetComponent<Bullet>();
            if (Bt == null) Bt = gameObject.AddComponent<Bullet>();

            /*
                 Destroyer：寿命管理用

                 ・Pool返却できるようにPoolManagerを渡す
                 ・Destroyerが「何に返せばいいか」を覚える
            */
            Destroyer = GetComponent<Destroyer>();
            if (Destroyer == null) Destroyer = gameObject.AddComponent<Destroyer>();

            Destroyer.SetPoolManager(OwnerPool);

            /*
                 BulletMove：移動処理用

                 ・Prefabの子に付いている可能性もあるので children も探す
                 ・それでも無ければ自分に追加する
            */
            BulletMove = GetComponent<BulletMove>();
            if (BulletMove == null) BulletMove = GetComponentInChildren<BulletMove>(true);
            if (BulletMove == null) BulletMove = gameObject.AddComponent<BulletMove>();

            ApplySpeed(MoveSpeed);
        }

        /*
             速度を反映する（取得時にも呼ぶ）
        */
        public void ApplySpeed(float MoveSpeed)
        {
            if (BulletMove == null) return;

            /*
                 BulletMove.Speed を直接触らない設計にする

                 ・BulletMove側のSpeedがprivateでもOKになる
                 ・「速度の反映方法」をBulletMove内部に閉じ込められる
            */
            BulletMove.SetSpeed(MoveSpeed);
        }

        /*
             寿命タイマーを開始する
        */
        public void StartLifeTimer(float LifeTimeSeconds)
        {
            if (Destroyer == null) return;

            Destroyer.StartDestroyTimer(LifeTimeSeconds);
        }
    }
}
