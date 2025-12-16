using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Hit Circle Settings")]
    public float hitRadius = 0.5f;

    [Tooltip("未設定ならこのEnemyControllerが付いているTransformを中心にします。")]
    public Transform hitCenter;   // 入れなくてOK

    [Header("Death")]
    public float destroyDelay = 0f;

    Animator anim;
    bool isDead = false;

    // Spawner管理（生成された個体）
    EnemySpawner ownerSpawner;
    GameObject myInstance;

    float logTimer;

    public void SetOwner(EnemySpawner spawner, GameObject instance)
    {
        ownerSpawner = spawner;
        myInstance = instance;
    }

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        Debug.Log($"[Enemy] Start name={name} selfObj='{gameObject.name}' pos={transform.position} root='{transform.root.name}' rootPos={transform.root.position}");
    }

    void Update()
    {
        if (isDead) return;

        // 1秒に1回だけログ
        logTimer += Time.deltaTime;
        bool doLog = false;
        if (logTimer >= 1f) { logTimer = 0f; doLog = true; }

        var bullets = Bullet.AllBullets;
        int count = bullets.Count;

        Vector3 center = (hitCenter != null) ? hitCenter.position : transform.position;
        float rSq = hitRadius * hitRadius;

        if (doLog)
        {
            Debug.Log($"[EnemyHit] enemyObj='{gameObject.name}' root='{transform.root.name}' bullets={count} center={center} r={hitRadius}");
        }

        if (count == 0) return;

        float nearestSq = float.PositiveInfinity;
        Transform nearestBullet = null;

        for (int i = count - 1; i >= 0; i--)
        {
            var b = bullets[i];
            if (b == null) continue;

            Vector3 bp = b.transform.position;
            float sq = (bp - center).sqrMagnitude;

            if (sq < nearestSq)
            {
                nearestSq = sq;
                nearestBullet = b.transform;
            }

            if (sq <= rSq)
            {
                Debug.Log($"[EnemyHit] HIT enemyObj='{gameObject.name}' root='{transform.root.name}' bullet='{b.name}' dist={Mathf.Sqrt(sq)}");
                Die();
                break;
            }
        }

        if (doLog && nearestBullet != null)
        {
            Debug.Log($"[EnemyHit] nearest enemyObj='{gameObject.name}' bullet='{nearestBullet.name}' dist={Mathf.Sqrt(nearestSq)} need<={hitRadius} bulletPos={nearestBullet.position}");
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        if (anim != null)
            anim.SetBool("IsDeath", true);

        // ★最優先：Spawnerが渡した生成個体（ルート）を消す
        if (ownerSpawner != null && myInstance != null)
        {
            Debug.Log($"[Enemy] Die -> KillSpawned instance='{myInstance.name}'");
            ownerSpawner.KillSpawned(myInstance, destroyDelay);
            return;
        }

        // ★保険：Owner未設定なら、自分のroot（敵のまとまり）を消す
        Debug.Log($"[Enemy] Die -> Destroy(root) root='{transform.root.name}'");
        Destroy(transform.root.gameObject, destroyDelay);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 center = (hitCenter != null) ? hitCenter.position : transform.position;
        Gizmos.DrawWireSphere(center, hitRadius);
    }
#endif
}
