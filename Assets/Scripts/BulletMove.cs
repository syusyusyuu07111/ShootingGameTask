using UnityEngine;

public class BulletMove : MonoBehaviour
{
    public float Speed = 5f;

    void Update()
    {
        // è„Ç…îÚÇŒÇ∑
        transform.position += Vector3.up * Speed * Time.deltaTime;
    }
}
