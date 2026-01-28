using UnityEngine;

/*
     背景を横方向にスクロールさせるクラス

     【挙動】
     ・毎フレーム、背景を左方向へ少しずつ移動させる
     ・一定のX座標を超えて画面外に出たら
       右端の開始位置へ瞬時に戻す
     ・これを繰り返すことで、無限に流れているように見せる

     【設計意図】
     ・transform.position を直接操作して移動させる
     ・物理演算は使わず、軽量な座標更新のみで処理する
     ・背景が画面外に完全に出たタイミングで再配置することで
       見た目上のつなぎ目をなくす
*/
public sealed class BackgroundScroll : MonoBehaviour
{
    //================
    // Scroll Settings
    //================

    /*
         1秒あたりに移動する速度
         値が大きいほどスクロールが速くなる
    */
    [Header("Scroll Settings")]
    [SerializeField] private float ScrollSpeed = 3.0f;

    /*
         このX座標より左に進んだらループさせる境界ライン
         「ここを超えたら画面外に完全に出た」と判断する
    */
    [Tooltip("このX座標を下回ったらループ")]
    [SerializeField] private float ResetBorderX = -4935f;

    /*
         ループ時に戻す開始位置のX座標
         右端の背景の初期配置位置として使用する
    */
    [Tooltip("ループ後に戻すX座標")]
    [SerializeField] private float ResetX = 23f;

    //================
    // Cache
    //================

    /*
         背景自身のTransformを保持する

         ・Update内で毎フレーム参照するためキャッシュしておく
         ・transformを毎回取得する処理コストを避ける目的
         ・位置操作はすべてこの変数経由で行う
    */
    private Transform BackgroundTransform;

    //================
    // Unity Event
    //================

    private void Awake()
    {
        /*
             自身のTransformを取得して保持する
             以降は transform を直接使わず、この変数を利用する
        */
        BackgroundTransform = transform;
    }

    private void Update()
    {
        //================
        // Scroll
        //================

        /*
             毎フレーム、背景を左方向へ移動させる

             ・Vector3.left      → 左方向ベクトル
             ・ScrollSpeed       → 移動速度
             ・Time.deltaTime    → フレーム依存防止

             これにより、FPSに依存しない一定速度でスクロールする
        */
        BackgroundTransform.position
            += Vector3.left * ScrollSpeed * Time.deltaTime;

        //================
        // Loop Check
        //================

        /*
             背景がまだ画面内にあるかを判定する

             X座標が ResetBorderX より大きい間は
             まだ表示範囲内なので何もしない
        */
        if (BackgroundTransform.position.x > ResetBorderX) return;

        //================
        // Loop Reset
        //================

        /*
             画面外に出た背景を右端へ戻す処理

             ・現在位置を一度Vector3に取得
             ・X座標だけを書き換える
             ・Y/Zはそのまま維持する
        */
        Vector3 pos = BackgroundTransform.position;

        // 右端の開始位置へ移動
        pos.x = ResetX;

        // 変更後の座標を反映
        BackgroundTransform.position = pos;
    }
}
