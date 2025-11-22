using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float Speed = 5f;
    public Transform Player;

    public float LimitLeft = -9.9f;
    public float LimitRight = 10f;

    InputSystem_Actions input;
    Animator anim;
    public bool IsMoving = false;

    private void Awake()
    {
        input = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        input.Player.Move.Enable();
    }

    private void Start()
    {
        anim = GetComponent<Animator>();
    }

    private void Update()
    {
        Vector2 move = input.Player.Move.ReadValue<Vector2>();

        if (move.sqrMagnitude > 0.01f)
        {
            IsMoving = true;
            anim.SetBool("iswalk", true);

            // 右（D）が押されているとき
            if (move.x > 0 && Player.transform.position.x < LimitRight)
            {
                // プレイヤーを右に移動させる
                Player.transform.position += Vector3.right * Speed * Time.deltaTime;
            }

            // 左（A）が押されているとき
            if (move.x < 0 && Player.transform.position.x > LimitLeft)
            {
                // プレイヤーを左に移動させる
                Player.transform.position += Vector3.left * Speed * Time.deltaTime;
            }
        }
        else
        {
            IsMoving = false;
            anim.SetBool("iswalk", false);
        }
    }
}
