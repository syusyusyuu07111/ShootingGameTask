using UnityEngine;

/// <summary>
/// 敵1体を管理するクラス
/// ・弾との距離で当たり判定
/// ・当たったら死亡
/// ・Spawner生成ならSpawner経由で消す
/// ・死亡エフェクトはEffectManagerに通知して生成
/// </summary>
public class EnemyController : MonoBehaviour
{
    [Header("Hit Circle Settings")]
    public float hitRadius = 0.5f;

    [Tooltip("当たり判定の中心（未設定ならEnemy位置）")]
    public Transform hitCenter;

    [Header("Death")]
    [Tooltip("消えるまでの遅延時間（死亡アニメ用）")]
    public float destroyDelay = 0f;

    [Header("Effect")]
    [Tooltip("未設定でもOK（自動でシーンから探す）")]
    public EffectManager effectManager;

    [Header("Animator")]
    [Tooltip("死亡アニメBoolパラメータ名（Animator側に無いなら空でもOK）")]
    public string deathBoolParam = "IsDeath";

    private Animator anim;
    private bool isDead = false;

    // Spawner管理
    private EnemySpawner ownerSpawner;
    private GameObject myInstance;

    public void SetOwner(EnemySpawner spawner, GameObject instance)
    {
        ownerSpawner = spawner;
        myInstance = instance;
    }

    void Start()
    {
        anim = GetComponentInChildren<Animator>();

        // Spawner生成で参照が入らない対策：シーン上のEffectManagerを拾う
        if (effectManager == null)
            effectManager = FindFirstObjectByType<EffectManager>();

        if (effectManager == null)
            Debug.LogError($"[Enemy] EffectManager がシーンに見つかりません name={name}");
    }

    void Update()
    {
        if (isDead) return;

        var bullets = Bullet.AllBullets;
        if (bullets == null || bullets.Count == 0) return;

        Vector3 center = (hitCenter != null) ? hitCenter.position : transform.position;
        float rSq = hitRadius * hitRadius;

        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            Bullet b = bullets[i];
            if (b == null) continue;

            Vector3 bp = b.transform.position;
            float sq = (bp - center).sqrMagnitude;

            if (sq <= rSq)
            {
                Die(transform.position); // “死んだ位置”は敵の位置にする
                break;
            }
        }
    }

    void Die(Vector3 diePos)
    {
        if (isDead) return;
        isDead = true;

        // 1) まずエフェクト（ここが最優先）
        if (effectManager != null)
        {
            Debug.Log($"[Enemy] call PlayEffect pos={diePos} name={name}");
            effectManager.PlayEffect(diePos);
        }
        else
        {
            Debug.LogError($"[Enemy] effectManager が未設定で PlayEffect を呼べない name={name}");
        }

        // 2) 死亡アニメ（パラメータが無いならエラーを避けてスキップ）
        if (anim != null && !string.IsNullOrEmpty(deathBoolParam))
        {
            if (HasBoolParameter(anim, deathBoolParam))
                anim.SetBool(deathBoolParam, true);
            else
                Debug.LogWarning($"[Enemy] AnimatorにBool '{deathBoolParam}' がありません name={name}");
        }

        // 3) 消す（Spawner管理ならSpawnerへ依頼）
        if (ownerSpawner != null && myInstance != null)
        {
            ownerSpawner.KillSpawned(myInstance, destroyDelay);
            return;
        }

        Destroy(transform.root.gameObject, destroyDelay);
    }

    // Animatorに指定のBoolパラメータが存在するかチェック（存在しない時のログ地獄回避）
    bool HasBoolParameter(Animator animator, string paramName)
    {
        foreach (var p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Bool && p.name == paramName)
                return true;
        }
        return false;
    }
}
