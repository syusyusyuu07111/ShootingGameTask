using UnityEngine;

/*
    敵キャラクターの移動を制御するクラス

    ・移動タイプによって挙動を切り替える
    ・Left     ：画面左方向へ水平移動
    ・Diagonal ：指定した方向へ斜め移動

    【このクラスの役割】
    ・毎フレーム位置を更新するだけ
    ・攻撃 / 当たり判定 / 死亡処理は持たない
*/
public class EnemyMove : MonoBehaviour
{
    /*
        1秒あたりに進む距離
        値を大きくすると移動が速くなる
    */
    public float Speed = 5f;

    /*
        敵の移動タイプ定義
        Inspector から選択できるよう enum にしている
    */
    public enum MoveType
    {
        Left,
        Diagonal
    }

    /*
        現在使用している移動タイプ
        Prefab ごとに設定を変えられる
    */
    public MoveType CurrentMoveType = MoveType.Left;

    /*
        斜め移動時の方向ベクトル
        例：
        (-1, -1) → 左下
        ( 1, -1) → 右下
    */
    public Vector2 DiagonalDirection = new Vector2(-1f, -1f);

    void Update()
    {
        /*
            毎フレーム呼ばれる

            CurrentMoveType に応じて
            実際の移動処理を切り替える
        */
        switch (CurrentMoveType)
        {
            case MoveType.Left:
                MoveLeft();
                break;

            case MoveType.Diagonal:
                MoveDiagonal();
                break;
        }
    }

    /*
        左方向へ一定速度で移動する

        ・Vector3.left = (-1, 0, 0)
        ・deltaTime を掛けてフレーム差を吸収
    */
    void MoveLeft()
    {
        transform.position += Vector3.left * Speed * Time.deltaTime;
    }

    /*
        指定した方向へ斜め移動する

        ・normalized でベクトルを正規化
        ・斜め移動でも速度が速くならないようにする
    */
    void MoveDiagonal()
    {
        Vector3 dir = new Vector3(
            DiagonalDirection.x,
            DiagonalDirection.y,
            0f
        );

        dir = dir.normalized;

        transform.position += dir * Speed * Time.deltaTime;
    }
}
