using UnityEngine;

/// <summary>
/// 敵キャラクターの移動挙動を制御するクラス。
/// 移動タイプ（左方向 or 斜め）を Inspector から切り替え可能。
///
/// </summary>
public class EnemyMove : MonoBehaviour
{
    /// <summary>
    /// 移動速度（1秒あたりに進む距離）
    /// </summary>
    public float Speed = 5f;


    /// </summary>
    public enum MoveType
    {
        Left,       // 左方向（画面左へ水平移動）
        Diagonal    // 斜め方向に移動
    }

    /// <summary>
    /// 現在の移動タイプ
    /// Inspectorから選択できるので、Prefabごとに挙動を変えられる
    /// </summary>
    public MoveType moveType = MoveType.Left;


    public Vector2 diagonalDirection = new Vector2(-1f, -1f);

    /// <summary>
    /// 現在の moveType に応じて、実際の移動処理を切り替える
    /// </summary>
    void Update()
    {

        switch (moveType)
        {
            case MoveType.Left:
                // 左移動用の処理
                MoveLeft();
                break;

            case MoveType.Diagonal:
                // 斜め移動用の処理
                MoveDiagonal();
                break;
        }
    }

    /// <summary>
    /// 左方向に一定速度で移動する
    /// </summary>
    void MoveLeft()
    {
        // 【Vector3.left】
        // ・(-1, 0, 0) を意味する定数
        // ・X方向のマイナス = 左方向
        //
        // 【Time.deltaTime】
        // ・前フレームからの経過秒数
        // ・FPSが違っても、1秒あたりの移動量が一定になる
        //
        // この1行は：
        // 「毎フレーム、左へ少しずつ移動する」
        transform.position += Vector3.left * Speed * Time.deltaTime;
    }

    /// <summary>
    /// diagonalDirectionで指定した方向に、一定速度で斜め移動する
    /// </summary>
    void MoveDiagonal()
    {

        Vector3 dir = new Vector3(
            diagonalDirection.x,
            diagonalDirection.y,
            0f
        )


        // 「斜め移動のほうが速い」状態にしないように正規化
        //

        .normalized;

        // 正規化された方向 × 速度 × deltaTime
        // → 斜めでも水平でも、体感速度が揃う
        transform.position += dir * Speed * Time.deltaTime;
    }
}
