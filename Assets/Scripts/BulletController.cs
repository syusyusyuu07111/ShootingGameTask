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
            GameObject bullet = pool.GetGameObject(
                player.position,
                Quaternion.identity
            );
            var destroyer = bullet.GetComponent<Destroyer>();
            if (destroyer != null)
            {
                destroyer.PoolManager = pool;
                destroyer.StartDestroyTimer(2f); // 2ïbå„Ç…ÉvÅ[ÉãÇ…ñﬂÇ∑ó·
            }
            fireTimer = 0f;
        }
    }
}
