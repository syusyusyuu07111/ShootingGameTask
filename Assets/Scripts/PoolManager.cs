using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// GameObject（主に弾）を再利用するためのプール管理クラス
/// 弾の寿命・移動・登録処理を一元管理
/// </summary>
public class PoolManager : MonoBehaviour
{
    [Header("Pool Target")]
    public GameObject Prefab;    // プールする元となるPrefab（弾）

    [Header("Move Settings")]
    public float Speed = 5f;     // 弾の移動速度

    [Header("Life")]
    public float LifeTime = 2f;  // 弾の寿命（秒）

    //ObjectPoolを利用して弾を管理
    private ObjectPool<GameObject> pool;

    /// <summary>
    /// プールの初期化。ObjectPoolのインスタンスを生成する。
    /// </summary>
    void Awake()
    {
        // プール生成
        pool = new ObjectPool<GameObject>(
            createFunc: OnCreatePooledObject,         // 新規生成時の処理
            actionOnGet: OnGetFromPool,               // プールから取得時の処理
            actionOnRelease: OnReleaseToPool,         // プールに戻す時の処理
            actionOnDestroy: OnDestroyPooledObject,   // プールから完全に削除する時の処理
            collectionCheck: false,                   // 重複チェック（falseで高速化）
            defaultCapacity: 10,                      // 初期生成数
            maxSize: 100                              // プールの最大保持数
        );
    }

    // =========================================================
    // プール内部処理
    // =========================================================

    /// <summary>
    /// プールに存在しない場合に新しく生成されるオブジェクト
    /// 初回のみ呼ばれる
    /// </summary>
    /// <returns>新規生成されたGameObject</returns>
    GameObject OnCreatePooledObject()
    {
        // Prefabから弾を生成し、非アクティブ化
        var obj = Instantiate(Prefab);
        obj.SetActive(false);

        // 弾の存在管理（距離判定用）Bulletコンポーネントを付与
        var bullet = obj.GetComponent<Bullet>();
        if (bullet == null)
        {
            bullet = obj.AddComponent<Bullet>();
        }

        // 寿命管理用Destroyerコンポーネントを付与
        var destroyer = obj.GetComponent<Destroyer>();
        if (destroyer == null)
        {
            destroyer = obj.AddComponent<Destroyer>();
        }
        // プール返却用にPoolManager参照をセット
        destroyer.PoolManager = this;

        // 弾の移動処理用BulletMoveコンポーネントを付与
        var move = obj.GetComponent<BulletMove>();
        if (move == null)
        {
            move = obj.GetComponentInChildren<BulletMove>(true);
        }
        if (move == null)
        {
            move = obj.AddComponent<BulletMove>();
        }
        // 初期速度を設定
        move.Speed = Speed;

        return obj;
    }

    /// <summary>
    /// プールからオブジェクトを取り出した時に呼ばれる
    /// 毎回リセットが必要な処理を書く
    /// </summary>
    /// <param name="obj">プールから取得したGameObject</param>
    void OnGetFromPool(GameObject obj)
    {
        // オブジェクトを有効化（OnEnableが呼ばれる）
        obj.SetActive(true);

        // 移動設定の再反映
        var move = obj.GetComponent<BulletMove>();
        if (move == null)
        {
            move = obj.GetComponentInChildren<BulletMove>(true);
        }
        if (move == null)
        {
            move = obj.AddComponent<BulletMove>();
        }
        move.Speed = Speed;

        // Destroyer（寿命管理）再設定
        var destroyer = obj.GetComponent<Destroyer>();
        if (destroyer == null)
        {
            destroyer = obj.AddComponent<Destroyer>();
        }
        destroyer.PoolManager = this;

        // 寿命タイマー開始
        destroyer.StartDestroyTimer(LifeTime);
    }

    /// <summary>
    /// プールに戻す時に呼ばれる
    /// </summary>
    /// <param name="obj">プールに戻すGameObject</param>
    void OnReleaseToPool(GameObject obj)
    {
        // オブジェクトを無効化（OnDisableが呼ばれる）
        obj.SetActive(false);
    }

    /// <summary>
    /// プール上限超過などで完全破棄される時
    /// </summary>
    /// <param name="obj">破棄するGameObject</param>
    void OnDestroyPooledObject(GameObject obj)
    {
        // 完全に削除
        Destroy(obj);
    }

    // =========================================================
    // 外部から利用するためのAPI
    // =========================================================

    /// <summary>
    /// プールからオブジェクトを取得して指定位置・回転で配置する
    /// </summary>
    /// <param name="position">配置位置</param>
    /// <param name="rotation">配置回転</param>
    /// <returns>取得したGameObject</returns>
    public GameObject GetGameObject(Vector3 position, Quaternion rotation)
    {
        var obj = pool.Get();
        obj.transform.SetPositionAndRotation(position, rotation);
        return obj;
    }

    /// <summary>
    /// オブジェクトをプールに戻す
    /// Destroyer から呼びだす
    /// </summary>
    /// <param name="obj">プールに戻すGameObject</param>
    public void ReleaseGameObject(GameObject obj)
    {
        pool.Release(obj);
    }
}
