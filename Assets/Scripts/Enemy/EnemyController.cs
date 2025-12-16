using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public float DeathDistance = 0.5f;
    public float destroyDelay = 0f;

    Animator anim;
    bool isDead = false;

    EnemySpawner ownerSpawner;
    GameObject myInstance;

    public void SetOwner(EnemySpawner spawner, GameObject instance)
    {
        ownerSpawner = spawner;
        myInstance = instance;
    }

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        Debug.Log($"[Enemy] Start name={name} anim={(anim != null)}");
    }

    void Update()
    {
        if (isDead) return;

        int count = Bullet.AllBullets.Count;
        if (count == 0) return;

        Vector3 enemyPos = transform.position;
        float deathDistSq = DeathDistance * DeathDistance;

        for (int i = count - 1; i >= 0; i--)
        {
            var b = Bullet.AllBullets[i];
            if (b == null) continue;

            float sq = (b.transform.position - enemyPos).sqrMagnitude;
            if (sq <= deathDistSq)
            {
                Debug.Log($"[Enemy] HIT! enemy={name} bullet={b.name} dist={Mathf.Sqrt(sq)}");
                Die();
                break;
            }
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        if (anim != null)
            anim.SetBool("IsDeath", true);

        // 1) Spawnerが渡した「生成個体」があるなら、それだけ消す（最優先）
        if (ownerSpawner != null && myInstance != null)
        {
            ownerSpawner.KillSpawned(myInstance, destroyDelay);
            return;
        }

        // 2) 保険：自分の親階層に EnemySpawner がいたら、そいつは絶対消さない
        var spawnerInParents = GetComponentInParent<EnemySpawner>();
        if (spawnerInParents != null)
        {
            Debug.LogError("[Enemy] Die: 親にEnemySpawnerが居ます。Spawnerが敵Prefabに混ざってる可能性大。破壊をスキップします。");
            return;
        }

        // 3) 最後の保険：自分のルート（敵のまとまり）を消す
        Destroy(transform.root.gameObject, destroyDelay);
    }

    void OnDestroy()
    {
        Debug.Log($"[Enemy] OnDestroy name={name} id={GetInstanceID()} root={transform.root.name}");
    }
}
