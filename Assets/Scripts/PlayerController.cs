using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Move Settings")]
    public float Speed = 5f;

    // 実際に動かすプレイヤー
    public Transform Player;

    [Header("Move Limit (Y)")]
    public float LimitDown = -4f;
    public float LimitUp = 6f;

    InputSystem_Actions input;
    Animator anim;

    public bool IsMoving = false;

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
        // 入力取得（上下：W/S or Stick）
        Vector2 move = input.Player.Move.ReadValue<Vector2>();

        if (move.sqrMagnitude > 0.01f)
        {
            IsMoving = true;
            if (anim != null)
                anim.SetBool("iswalk", true);

            // 現在位置を取得
            Vector3 pos = Player.position;

            // 入力に応じて上下移動
            pos.y += move.y * Speed * Time.deltaTime;

            // ｙ座標を制限（-4 ～ 6）
            pos.y = Mathf.Clamp(pos.y, LimitDown, LimitUp);

            // 位置を反映
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
