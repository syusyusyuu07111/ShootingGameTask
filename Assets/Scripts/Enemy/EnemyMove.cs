using UnityEngine;

/*
     敵キャラクターの移動を制御するクラス

     【挙動】
     ・移動タイプによって挙動を切り替える
     ・Left     ：画面左方向へ水平移動する
     ・Diagonal ：指定した方向へ斜め移動する

     【このクラスの役割】
     ・毎フレーム位置を更新するだけ
     ・攻撃 / 当たり判定 / 死亡処理は持たない

     【設計方針】
     ・transform参照はキャッシュして使う
     ・斜め移動の方向は毎フレーム正規化せず、事前にキャッシュする
*/
public sealed class EnemyMove : MonoBehaviour
{
    //================
    // Move Settings
    //================

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
         Prefabごとに設定を変えられる
    */
    [SerializeField] private MoveType CurrentMoveType = MoveType.Left;

    /*
         斜め移動時の方向ベクトル（入力値）

         例：
         (-1, -1) → 左下
         ( 1, -1) → 右下
    */
    [SerializeField] private Vector2 DiagonalDirection = new Vector2(-1f, -1f);


    //================
    // Cache
    //================

    /*
         自身のTransformを保持する

         ・Update内で transform を直接触らない
         ・可読性と統一のため役割名で持つ
    */
    private Transform EnemyTransform;

    /*
         斜め移動方向（正規化済み）を保持する

         ・normalizedは毎フレーム計算しない
         ・設定値から「方向だけ」を作り、Updateではそれを使う
    */
    private Vector3 DiagonalDirNormalized;


    //================
    // Unity Event
    //================

    private void Awake()
    {
        /*
             Transformをキャッシュする
        */
        EnemyTransform = transform;

        /*
             斜め移動方向を作って保持する

             ・Inspector値はVector2なのでVector3に変換する
             ・Zは2D運用のため0固定
        */
        DiagonalDirNormalized =
            new Vector3(DiagonalDirection.x, DiagonalDirection.y, 0f);

        /*
             方向が(0,0)の場合は正規化できない

             ・この場合は移動できないのでエラーを出す
             ・方向が無効なら斜め移動は止める（ゼロ方向にする）
        */
        if (DiagonalDirNormalized.sqrMagnitude <= 0.000001f)
        {
            Debug.LogError($"[EnemyMove] DiagonalDirection が(0,0)です name={name}");
            DiagonalDirNormalized = Vector3.zero;
        }
        else
        {
            DiagonalDirNormalized = DiagonalDirNormalized.normalized;
        }
    }

    private void Update()
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
    // Move
    //================

    /*
         左方向へ一定速度で移動する

         ・Vector3.left = (-1, 0, 0)
         ・deltaTime を掛けてフレーム差を吸収する
    */
    private void MoveLeft()
    {
        EnemyTransform.position += Vector3.left * Speed * Time.deltaTime;
    }

    /*
         指定した方向へ斜め移動する

         ・DiagonalDirNormalized はAwakeで正規化済み
         ・Updateでは方向を作り直さず、そのまま使う
    */
    private void MoveDiagonal()
    {
        EnemyTransform.position += DiagonalDirNormalized * Speed * Time.deltaTime;
    }
}
