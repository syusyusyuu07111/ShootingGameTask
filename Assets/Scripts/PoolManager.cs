using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// GameObject を再利用するためのプール管理クラス
/// 弾の管理
/// ・弾の寿命 / 移動 / 登録処理を一元管理
/// </summary>
public class PoolManager : MonoBehaviour
{
    [Header("Pool Target")]
    public GameObject Prefab;    // プールする元Prefab（弾）

    [Header("Move Settings")]
    public float Speed = 5f;     // 弾の移動速度

    [Header("Life")]
    public float LifeTime = 2f;  // 弾の寿命

    // Unity標準のObjectPool
    ObjectPool<GameObject> pool;

    void Awake()
    {
        // プール生成
        pool = new ObjectPool<GameObject>(
            createFunc: OnCreatePooledObject,     // 新規生成時
            actionOnGet: OnGetFromPool,           // 取り出した時
            actionOnRelease: OnReleaseToPool,     // プールに戻す時
            actionOnDestroy: OnDestroyPooledObject, // 完全破棄時
            collectionCheck: false,               // 重複チェック
            defaultCapacity: 10,                  // 初期生成数
            maxSize: 100                          // 最大保持数
        );
    }

    // =========================================================
    // プール内部処理
    // =========================================================

    /// <summary>
    /// プールに存在しない場合に新しく生成されるオブジェクト
    /// 初回のみ呼ばれる
    /// </summary>
    GameObject OnCreatePooledObject()
    {
        // Prefabから生成
        var obj = Instantiate(Prefab);
        obj.SetActive(false);

        // ---------- Bullet 登録 ----------
        // 弾の存在管理（距離判定用）
        var bullet = obj.GetComponent<Bullet>();
        if (bullet == null)
            bullet = obj.AddComponent<Bullet>();

        // ---------- Destroyer（寿命管理） ----------
        var destroyer = obj.GetComponent<Destroyer>();
        if (destroyer == null)
            destroyer = obj.AddComponent<Destroyer>();

        // PoolManager を渡すことで Destroy ではなくプール返却になる
        destroyer.PoolManager = this;

        // ---------- 移動処理 ----------
        var move = obj.GetComponent<BulletMove>();
        if (move == null)
            move = obj.GetComponentInChildren<BulletMove>(true);

        if (move == null)
            move = obj.AddComponent<BulletMove>();

        // 初期速度設定
        move.Speed = Speed;

        return obj;
    }

    /// <summary>
    /// プールからオブジェクトを取り出した時に呼ばれる
    /// 毎回リセットが必要な処理を書く
    /// </summary>
    void OnGetFromPool(GameObject obj)
    {
        // 有効化（OnEnableが呼ばれる）
        obj.SetActive(true);

        // ---------- 移動設定の再反映 ----------
        var move = obj.GetComponent<BulletMove>();
        if (move == null)
            move = obj.GetComponentInChildren<BulletMove>(true);

        if (move == null)
            move = obj.AddComponent<BulletMove>();

        move.Speed = Speed;

        // ---------- Destroyer 再設定 ----------
        var destroyer = obj.GetComponent<Destroyer>();
        if (destroyer == null)
            destroyer = obj.AddComponent<Destroyer>();

        destroyer.PoolManager = this;

        // ---------- 寿命スタート ----------
        destroyer.StartDestroyTimer(LifeTime);
    }

    /// <summary>
    /// プールに戻す時に呼ばれる
    /// </summary>
    void OnReleaseToPool(GameObject obj)
    {
        // 無効化（OnDisableが呼ばれる）
        obj.SetActive(false);
    }

    /// <summary>
    /// プール上限超過などで完全破棄される時
    /// </summary>
    void OnDestroyPooledObject(GameObject obj)
    {
        Destroy(obj);
    }

    // =========================================================
    // 外から読み込む用
    // =========================================================

    /// <summary>
    /// プールからオブジェクトを取得して配置する
    /// </summary>
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
    public void ReleaseGameObject(GameObject obj)
    {
        pool.Release(obj);
    }
}
