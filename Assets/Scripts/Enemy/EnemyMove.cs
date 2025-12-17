using UnityEngine;

public class EnemyMove : MonoBehaviour
{
    public float Speed = 5f;

    // 移動タイプをInspectorから選択できるようにする
    public enum MoveType
    {
        Left,       // 真下
        Diagonal    // 斜め
    }

    public MoveType moveType = MoveType.Left;

    // 斜め移動用
    public Vector2 diagonalDirection = new Vector2(-1f, -1f);

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

    void MoveLeft()
    {
        transform.position += Vector3.left * Speed * Time.deltaTime;
    }

    void MoveDiagonal()
    {
        // 正規化して速度を一定にする
        Vector3 dir = new Vector3(diagonalDirection.x, diagonalDirection.y, 0f).normalized;
        transform.position += dir * Speed * Time.deltaTime;
    }
}
