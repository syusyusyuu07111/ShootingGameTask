using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public float DeathDistance = 0.5f;
    public Animator anim;
    public bool IsDeath = false;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (IsDeath) return;

        Vector3 enemyPos = transform.position;
        float deathDistSq = DeathDistance * DeathDistance; // 距離の二乗で判定（ちょい高速）

        // 今生きてる全ての弾をチェック
        foreach (var bullet in Bullet.AllBullets)
        {
            if (bullet == null) continue;

            Vector3 diff = bullet.transform.position - enemyPos;
            float distSq = diff.sqrMagnitude;

            if (distSq < deathDistSq)
            {
                Die();
                break; // どれか1発でも当たったら終了
            }
        }
    }

    void Die()
    {
        IsDeath = true;
        anim.SetBool("IsDeath", true);
    }
}
