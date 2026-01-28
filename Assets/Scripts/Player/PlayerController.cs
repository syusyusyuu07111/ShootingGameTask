using UnityEngine;

/*
     プレイヤーの移動を制御するクラス

     【主な役割】
     ・入力から移動量を作り、プレイヤーを移動させる
     ・移動範囲（X/Y）を制限して画面外へ出ないようにする
     ・移動中/停止中でアニメ（歩き）を切り替える

     【設計方針】
     ・Updateは移動処理に集中させる
     ・transform参照はキャッシュして読みやすくする
     ・アニメ更新は「値が変わった時だけ」行い、無駄なSetBoolを減らす
*/
public sealed class PlayerController : MonoBehaviour
{
    //================
    // Move Settings
    //================

    /*
         移動速度（1秒あたりの移動量）
         値を大きくすると速くなる
    */
    [SerializeField] private float Speed = 5f;

    /*
         実際に動かすプレイヤーのTransform

         ・親のTransformを動かしたい/子だけ動かしたい、など設計で変わる
         ・未設定は不具合なのでStartでエラーを出す
    */
    [SerializeField] private Transform Player;


    //================
    // Move Limit (X)
    //================

    [SerializeField] private float LimitLeft = -8f;
    [SerializeField] private float LimitRight = 8f;


    //================
    // Move Limit (Y)
    //================

    [SerializeField] private float LimitDown = -4f;
    [SerializeField] private float LimitUp = 6f;


    //================
    // Input / Anim
    //================

    private InputSystem_Actions Input;
    private Animator Anim;


    //================
    // Control
    //================

    [Header("Control")]
    [Tooltip("false の間は移動入力を受け付けない")]
    public bool ControlEnabled = true;


    //================
    // Debug State
    //================

    /*
         現在移動中かどうか（内部状態）

         ・外部から書き換えさせないため private
         ・挙動確認のために保持している
    */
    private bool IsMoving = false;

    /*
         直前にAnimへ送った歩き状態

         ・毎フレームSetBoolしないためのキャッシュ
         ・値が変わった時だけ更新する
    */
    private bool LastWalkAnim = false;


    //================
    // Cache
    //================

    /*
         PlayerTransformを保持する

         ・Update内で Player を直接触らない
         ・読みやすさと統一のため「役割名」で保持する
    */
    private Transform PlayerTransform;


    //================
    // Unity Event
    //================

    private void Awake()
    {
        /*
             入力システムを生成する
             Enable/DisableはOnEnable/OnDisableで切り替える
        */
        Input = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        /*
             Player入力を有効化する
        */
        Input.Player.Enable();
    }

    private void OnDisable()
    {
        /*
             Player入力を無効化する
        */
        Input.Player.Disable();
    }

    private void Start()
    {
        //================
        // Cache
        //================

        /*
             Animatorを取得する
             （無い場合はアニメ切替ができないだけで移動は可能）
        */
        Anim = GetComponent<Animator>();

        /*
             PlayerTransformを保持する
             未設定は不具合なのでエラーを出す
        */
        if (Player == null) Debug.LogError("[PlayerController] Player が未設定です（Transform を設定してください）");
        PlayerTransform = Player;

        /*
             初期状態は停止
             歩きアニメも停止に寄せる
        */
        IsMoving = false;
        SetWalkAnimation(false);
    }

    private void Update()
    {
        //================
        // Control Disable
        //================

        /*
             操作不能中は完全停止する

             ・移動フラグを落とす
             ・歩きアニメを止める
             ・位置更新はしない
        */
        if (!ControlEnabled)
        {
            IsMoving = false;
            SetWalkAnimation(false);
            return;
        }

        /*
             PlayerTransform が無い場合は移動できない
             （Startでエラーは出ている）
        */
        if (PlayerTransform == null) return;

        //================
        // Input
        //================

        /*
             入力から移動量を取得する

             ・WASD / スティック
             ・入力値は -1～1 が基本（デバイスにより強さが変わる）
        */
        Vector2 Move = Input.Player.Move.ReadValue<Vector2>();

        /*
             入力がほぼ無いなら停止扱い

             ・微小入力（スティックの遊び）で勝手に動かないよう閾値を設ける
        */
        float MoveSq = Move.sqrMagnitude;
        bool HasMove = MoveSq > 0.01f;

        if (!HasMove)
        {
            IsMoving = false;
            SetWalkAnimation(false);
            return;
        }

        //================
        // Move
        //================

        /*
             入力があるなら移動する
        */
        IsMoving = true;
        SetWalkAnimation(true);

        /*
             positionを編集するため、現在座標を取り出して編集してから戻す
        */
        Vector3 Pos = PlayerTransform.position;

        float dt = Time.deltaTime;

        float MoveX = Move.x * Speed * dt;
        float MoveY = Move.y * Speed * dt;

        Pos.x += MoveX;
        Pos.y += MoveY;

        //================
        // Clamp
        //================

        /*
             移動制限

             ・画面外へ出ないように座標をClampする
        */
        Pos.x = Mathf.Clamp(Pos.x, LimitLeft, LimitRight);
        Pos.y = Mathf.Clamp(Pos.y, LimitDown, LimitUp);

        PlayerTransform.position = Pos;
    }


    //================
    // Animation
    //================

    private void SetWalkAnimation(bool IsWalk)
    {
        /*
             Animatorが無いなら何もしない
             （移動自体は可能）
        */
        if (Anim == null) return;

        /*
             同じ値を毎フレームSetBoolしない

             ・Animatorへの更新は地味にコストになる
             ・値が変わった時だけ送る
        */
        if (LastWalkAnim == IsWalk) return;

        LastWalkAnim = IsWalk;

        /*
             "iswalk" を更新する

             ・Animator側にBoolパラメータが必要
             ・名前を変える場合はAnimatorと合わせる
        */
        Anim.SetBool("iswalk", IsWalk);
    }
}
