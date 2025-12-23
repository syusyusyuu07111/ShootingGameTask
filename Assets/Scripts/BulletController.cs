using UnityEngine;

public class BulletController : MonoBehaviour
{
    public PoolManager pool;
    public Transform player;
    public float FireInterval = 0.2f;
    public float fireTimer = 0f;

    InputSystem_Actions input;

    [Header("Control")]
    [Tooltip("false の間は攻撃入力を受け付けない")]
    public bool ControlEnabled = true;

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
        // 操作不能中は発射しない（タイマーも回さない）
        if (!ControlEnabled) return;

        fireTimer += Time.deltaTime;

        if (input.Player.Attack.IsPressed() && fireTimer >= FireInterval)
        {
            GameObject bullet = pool.GetGameObject(
                player.position,
                Quaternion.identity
            );

            var destroyer = bullet.GetComponent<Destroyer>();
            if (destroyer != null)
            {
                destroyer.PoolManager = pool;
                destroyer.StartDestroyTimer(2f);
            }

            fireTimer = 0f;
        }
    }
}
