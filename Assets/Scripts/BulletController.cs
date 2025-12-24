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

    [Header("Audio")]
    public AudioSource seSource;
    public AudioClip LaunchSE;

    [Range(0f, 1f)]
    public float volume = 1.0f;

    [Header("Limiter")]
    [Tooltip("この秒数以内の連続再生は無視（多重防止）")]
    public float minInterval = 0.05f;

    float lastPlayTime = -999f;

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
            if (LaunchSE == null) return;

            // 多重再生防止
            if (Time.time - lastPlayTime < minInterval)
                return;

            lastPlayTime = Time.time;

            seSource.PlayOneShot(LaunchSE, volume);

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
