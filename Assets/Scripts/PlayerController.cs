using UnityEngine;

/*
     プレイヤーの移動を制御するクラス
     ・入力から移動量を作る
     ・移動範囲を制限する
     ・移動中/停止中でアニメを切り替える
*/
public class PlayerController : MonoBehaviour
{
    //================
    // Move Settings
    //================

    // プレイヤーの移動速度
    public float Speed = 5f;

    // 実際に動かすプレイヤーのTransform
    public Transform Player;

    //================
    // Move Limit (X)
    //================

    // プレイヤーが移動できるX座標の左端制限
    public float LimitLeft = -8f;

    // プレイヤーが移動できるX座標の右端制限
    public float LimitRight = 8f;

    //================
    // Move Limit (Y)
    //================

    // プレイヤーが移動できるY座標の下端制限
    public float LimitDown = -4f;

    // プレイヤーが移動できるY座標の上端制限
    public float LimitUp = 6f;

    //================
    // Input / Anim
    //================

    InputSystem_Actions Input;
    Animator Animator;

    //================
    // State / Control
    //================

    // プレイヤーが現在移動中かどうか
    public bool IsMoving = false;

    [Header("Control")]
    [Tooltip("false の間は移動入力を受け付けない")]
    public bool ControlEnabled = true;

    //================
    // Unity Event
    //================

    void Awake()
    {
        // 入力システムを生成する
        Input = new InputSystem_Actions();
    }

    void OnEnable()
    {
        // Player入力を有効化する
        Input.Player.Enable();
    }

    void OnDisable()
    {
        // Player入力を無効化する
        Input.Player.Disable();
    }

    void Start()
    {
        // Animator を取得する
        Animator = GetComponent<Animator>();
    }

    void Update()
    {
        /*
             操作不能中は完全停止する
             ・移動フラグを落とす
             ・歩きアニメを止める
        */
        if (!ControlEnabled)
        {
            IsMoving = false;
            SetWalkAnimation(false);
            return;
        }

        /*
             入力から移動量を取得する
             ・WASD / スティック
        */
        Vector2 Move = Input.Player.Move.ReadValue<Vector2>();

        /*
             入力が無いなら停止扱い
        */
        float MoveSq = Move.sqrMagnitude;
        bool HasMove = MoveSq > 0.01f;

        if (!HasMove)
        {
            IsMoving = false;
            SetWalkAnimation(false);
            return;
        }

        /*
             入力があるなら移動する
        */
        if (Player == null)
        {
            Debug.LogError("[PlayerController] Player が未設定です（Transform を設定してください）");
            return;
        }

        IsMoving = true;
        SetWalkAnimation(true);

        /*
             position をいじるので Transform を変数に保持する
        */
        Transform Tr = Player;

        Vector3 Pos = Tr.position;

        // X,Y 両方動かす
        float dt = Time.deltaTime;
        float MoveX = Move.x * Speed * dt;
        float MoveY = Move.y * Speed * dt;

        Pos.x += MoveX;
        Pos.y += MoveY;

        // 移動制限
        Pos.x = Mathf.Clamp(Pos.x, LimitLeft, LimitRight);
        Pos.y = Mathf.Clamp(Pos.y, LimitDown, LimitUp);

        Tr.position = Pos;
    }

    //================
    // Animation
    //================

    void SetWalkAnimation(bool IsWalk)
    {
        // Animator が無い場合は何もしない
        if (Animator == null) return;

        // iswalk を更新する
        Animator.SetBool("iswalk", IsWalk);
    }
}
