using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Move Settings")]
    public float Speed = 5f;

    // 実際に動かすプレイヤー
    public Transform Player;

    [Header("Move Limit (X)")]
    public float LimitLeft = -8f;
    public float LimitRight = 8f;

    [Header("Move Limit (Y)")]
    public float LimitDown = -4f;
    public float LimitUp = 6f;

    InputSystem_Actions input;
    Animator anim;

    public bool IsMoving = false;

    [Header("Control")]
    [Tooltip("false の間は移動入力を受け付けない")]
    public bool ControlEnabled = true;

    private void Awake()
    {
        input = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        input.Player.Enable();
    }

    private void OnDisable()
    {
        input.Player.Disable();
    }

    private void Start()
    {
        anim = GetComponent<Animator>();
    }

    private void Update()
    {
        // 操作不能中は完全停止（アニメも止める）
        if (!ControlEnabled)
        {
            IsMoving = false;
            if (anim != null) anim.SetBool("iswalk", false);
            return;
        }

        // 入力取得（WASD / スティック）
        Vector2 move = input.Player.Move.ReadValue<Vector2>();

        if (move.sqrMagnitude > 0.01f)
        {
            IsMoving = true;
            if (anim != null)
                anim.SetBool("iswalk", true);

            Vector3 pos = Player.position;

            // X,Y 両方動かす
            pos.x += move.x * Speed * Time.deltaTime;
            pos.y += move.y * Speed * Time.deltaTime;

            // 移動制限
            pos.x = Mathf.Clamp(pos.x, LimitLeft, LimitRight);
            pos.y = Mathf.Clamp(pos.y, LimitDown, LimitUp);

            Player.position = pos;
        }
        else
        {
            IsMoving = false;
            if (anim != null)
                anim.SetBool("iswalk", false);
        }
    }
}
