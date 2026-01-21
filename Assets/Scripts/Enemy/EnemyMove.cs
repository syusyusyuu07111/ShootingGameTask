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
    [SerializeField] private float Speed = 5f;

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
    [SerializeField] private MoveType CurrentMoveType = MoveType.Left;

    /*
        斜め移動時の方向ベクトル
        例：
        (-1, -1) → 左下
        ( 1, -1) → 右下
    */
    [SerializeField] private Vector2 DiagonalDirection = new Vector2(-1f, -1f);

    /*
        自身の Transform を保持
        Update 内で transform を直接触らないためのキャッシュしておく
    */
    Transform tr;

    //================
    // Unity Event
    //================

    void Awake()
    {
        /*
            Transform を保持しておく
        */
        tr = transform;
    }

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

    //================
    // 移動処理
    //================

    /*
        左方向へ一定速度で移動する

        ・Vector3.left = (-1, 0, 0)
        ・deltaTime を掛けてフレーム差を吸収
    */
    void MoveLeft()
    {
        tr.position += Vector3.left * Speed * Time.deltaTime;
    }

    /*
        指定した方向へ斜め移動する

        ・Vector2 を Vector3 に変換
        ・normalized で方向だけを使う
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

        tr.position += dir * Speed * Time.deltaTime;
    }
}
