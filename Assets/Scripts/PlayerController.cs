using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Move Settings")]
    /// <summary>
    /// プレイヤーの移動速度
    /// </summary>
    public float Speed = 5f;

    /// <summary>
    /// 実際に動かすプレイヤーオブジェクトのTransform
    /// </summary>
    public Transform Player;

    [Header("Move Limit (X)")]
    /// <summary>
    /// プレイヤーが移動できるX座標の左端制限
    /// </summary>
    public float LimitLeft = -8f;
    /// <summary>
    /// プレイヤーが移動できるX座標の右端制限
    /// </summary>
    public float LimitRight = 8f;

    [Header("Move Limit (Y)")]
    /// <summary>
    /// プレイヤーが移動できるY座標の下端制限
    /// </summary>
    public float LimitDown = -4f;
    /// <summary>
    /// プレイヤーが移動できるY座標の上端制限
    /// </summary>
    public float LimitUp = 6f;

    /// <summary>
    /// 入力システムのアクション（移動入力を取得するためのもの）
    /// </summary>
    InputSystem_Actions input;

    /// <summary>
    /// プレイヤーのアニメーション制御用
    /// </summary>
    Animator anim;

    /// <summary>
    /// プレイヤーが現在移動中かどうか
    /// </summary>
    public bool IsMoving = false;

    [Header("Control")]
    [Tooltip("false の間は移動入力を受け付けない")]
    /// <summary>
    /// プレイヤーの操作を有効にするかどうか（falseで操作不能）
    /// </summary>
    public bool ControlEnabled = true;

    /// <summary>
    /// インスタンス生成時に入力システムを初期化
    /// </summary>
    private void Awake()
    {
        input = new InputSystem_Actions();
    }

    /// <summary>
    /// オブジェクト有効化時に入力アクションを有効化
    /// </summary>
    private void OnEnable()
    {
        input.Player.Enable();
    }

    /// <summary>
    /// オブジェクト無効化時に入力アクションを無効化
    /// </summary>
    private void OnDisable()
    {
        input.Player.Disable();
    }

    /// <summary>
    /// 開始時にAnimatorコンポーネントを取得
    /// </summary>
    private void Start()
    {
        anim = GetComponent<Animator>();
    }

    /// <summary>
    /// 毎フレーム、プレイヤーの移動処理とアニメーション制御を行う
    /// </summary>
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
