using UnityEngine;

public class EnemyMove : MonoBehaviour
{
    public float Speed = 5f;

    void Update()
    {
        // ‚µ‚½‚É“G‚ªˆÚ“®
        transform.position += Vector3.down * Speed * Time.deltaTime;
    }
}
