using UnityEngine;

/// <summary>
/// 敵キャラクターの移動挙動を制御するクラス。
/// 移動タイプ（左方向または斜め）をInspectorで選択可能。
/// </summary>
public class EnemyMove : MonoBehaviour
{
    /// <summary>
    /// 移動速度
    /// </summary>
    public float Speed = 5f;

    /// <summary>
    /// 移動タイプの列挙体。
    /// Left: 左方向（真下）に移動
    /// Diagonal: 斜め方向に移動
    /// </summary>
    public enum MoveType
    {
        Left,       // 左方向（真下）
        Diagonal    // 斜め方向
    }

    /// <summary>
    /// 現在の移動タイプ（Inspectorで設定可能）
    /// </summary>
    public MoveType moveType = MoveType.Left;

    /// <summary>
    /// 斜め移動時の移動方向ベクトル（x, y）。
    /// 例: (-1, -1) で左下方向。
    /// </summary>
    public Vector2 diagonalDirection = new Vector2(-1f, -1f);

    /// <summary>
    /// 毎フレーム呼ばれ、移動タイプに応じて移動処理を実行する。
    /// </summary>
    void Update()
    {
        switch (moveType)
        {
            case MoveType.Left:
                MoveLeft();
                break;

            case MoveType.Diagonal:
                MoveDiagonal();
                break;
        }
    }

    /// <summary>
    /// 左方向（真下）に一定速度で移動する。
    /// </summary>
    void MoveLeft()
    {
        transform.position += Vector3.left * Speed * Time.deltaTime;
    }

    /// <summary>
    /// diagonalDirectionで指定した方向に、速度を一定にして斜め移動する。
    /// </summary>
    void MoveDiagonal()
    {
        // 移動方向を正規化して速度を一定にする
        Vector3 dir = new Vector3(diagonalDirection.x, diagonalDirection.y, 0f).normalized;
        transform.position += dir * Speed * Time.deltaTime;
    }
}
