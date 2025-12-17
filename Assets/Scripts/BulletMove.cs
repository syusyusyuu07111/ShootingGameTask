using UnityEngine;

public class BulletMove : MonoBehaviour
{
    public float Speed = 5f;

    void Update()
    {
        // ’e‚ð‰E‚É”ò‚Î‚·
        transform.position += Vector3.right * Speed * Time.deltaTime;
    }
}
