using UnityEngine;

public class BulletController : MonoBehaviour
{
    public PoolManager pool;
    public Transform player;
    public float FireInterval = 0.2f;
    public float fireTimer = 0f;

    InputSystem_Actions input;

    void Awake()
    {
        input = new InputSystem_Actions();
    }

    void OnEnable()
    {
        input.Enable();
    }

    void OnDisable()
    {
        input.Disable();
    }

    void Update()
    {
        fireTimer += Time.deltaTime;

        if (input.Player.Attack.IsPressed() && fireTimer >= FireInterval)
        {
            // Pool から弾オブジェクトを取得
            GameObject bullet = pool.GetGameObject(
                player.position,
                Quaternion.identity
            );

            fireTimer = 0f;
        }
    }
}
