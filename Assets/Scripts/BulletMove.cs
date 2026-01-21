using UnityEngine;

public class BulletMove : MonoBehaviour
{
    public float Speed = 5f;

    Transform tr;

    void Awake()
    {
        // Transform をキャッシュしておく
        tr = transform;
    }

    void Update()
    {
        // 弾を右に飛ばす
        tr.position += Vector3.right * Speed * Time.deltaTime;
    }
}
