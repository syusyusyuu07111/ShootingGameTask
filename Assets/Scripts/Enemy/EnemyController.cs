using UnityEngine;

/// <summary>
/// 敵1体を管理するクラス
/// ・弾との距離で当たり判定を行う
/// ・当たったら死亡する
/// ・Spawner から生成された敵は Spawner 経由で消す
/// </summary>
public class EnemyController : MonoBehaviour
{
    // ================================
    // 当たり判定設定
    // ================================

    [Header("Hit Circle Settings")]
    [Tooltip("弾との当たり判定の半径")]
    public float hitRadius = 0.5f;

    // 当たり判定の中心
    // 未設定ならこの EnemyController が付いている位置を使う
    public Transform hitCenter;

    // ================================
    // 死亡設定
    // ================================

    [Header("Death")]
    [Tooltip("消えるまでの遅延時間（死亡アニメ用）")]
    public float destroyDelay = 0f;

    Animator anim;
    bool isDead = false;

    // ================================
    // Spawner 管理用（ここが重要）
    // ================================

    // この敵を「生成した」Spawner
    // → Spawner が敵リストを管理している
    EnemySpawner ownerSpawner;

    // Spawner が管理している「この敵の実体」
    // → Destroy するときに Spawner に渡すための参照
    GameObject myInstance;

    /// <summary>
    /// Spawner から呼ばれる
    /// 「この敵は私（Spawner）が管理しているよ」
    /// という情報を受け取るための関数
    /// </summary>
    public void SetOwner(EnemySpawner spawner, GameObject instance)
    {
        ownerSpawner = spawner;
        myInstance = instance;
    }

    void Start()
    {
        // 子オブジェクトから Animator を取得
        anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // すでに死んでいたら何もしない
        if (isDead) return;

        // 現在存在している弾リストを取得
        var bullets = Bullet.AllBullets;
        if (bullets.Count == 0) return;

        // 当たり判定の中心を決める
        Vector3 center;
        if (hitCenter != null)
            center = hitCenter.position;
        else
            center = transform.position;

        // 半径の2乗（sqrtを使わないため）
        float rSq = hitRadius * hitRadius;

        // 弾との距離チェック
        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            Bullet b = bullets[i];
            if (b == null) continue;

            Vector3 bp = b.transform.position;
            float sq = (bp - center).sqrMagnitude;

            // 半径以内ならヒット
            if (sq <= rSq)
            {
                Die();
                break;
            }
        }
    }

    /// <summary>
    /// 敵を死亡させる
    /// </summary>
    void Die()
    {
        // 二重実行防止
        if (isDead) return;
        isDead = true;

        // 死亡アニメ再生
        if (anim != null)
            anim.SetBool("IsDeath", true);

        // --------------------------------
        // Spawner 管理の敵の場合
        // --------------------------------
        // Spawner は「生成した敵のリスト」を持っているので
        // 勝手に Destroy するとリストが壊れる
        // → 必ず Spawner に「消して」とお願いする
        if (ownerSpawner != null && myInstance != null)
        {
            ownerSpawner.KillSpawned(myInstance, destroyDelay);
            return;
        }

        // --------------------------------
        // Spawner 管理でない敵の場合
        // --------------------------------
        // シーンに直置きされた敵
        // 自分のルートごと消す
        Destroy(transform.root.gameObject, destroyDelay);
    }
}
