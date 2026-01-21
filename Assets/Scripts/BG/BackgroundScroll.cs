using UnityEngine;

/*
    背景を横方向にスクロールさせるクラス
    ・毎フレーム左方向へ移動
    ・一定のX座標を超えたら、右側へ瞬時に戻す

    設計
    ・transform.position を少しずつ動かすことで「スクロール」に見せる
    ・「画面外に出たら瞬間移動させる」ことで、無限スクロールを表現する
*/
public class BackgroundScroll : MonoBehaviour
{
    [Header("Scroll Settings")]
    public float ScrollSpeed = 3.0f;

    [Tooltip("このX座標を超えたらループ")]
    public float ResetBorderX = -4935f;

    [Tooltip("ループ後に戻すX座標")]
    public float ResetX = 23f;

    Transform CachedTransform;

    void Awake()
    {
        CachedTransform = transform;
    }

    void Update()
    {
        // =====================================================
        // ① 毎フレーム左にスクロールする処理
        // =====================================================

        // 「1フレームごとに、左へ少しずつ移動する」
        CachedTransform.position += Vector3.left * ScrollSpeed * Time.deltaTime;

        // =====================================================
        // ② 一定位置まで行ったらループさせる処理
        // =====================================================

        // ・背景が「画面の外まで完全に流れ切ったか？」を判定
        // ・ResetBorderX は「これ以上左に行ったら見えない」ライン
        if (CachedTransform.position.x > ResetBorderX) return;

        Vector3 pos = CachedTransform.position;

        // X座標だけを「右端の開始位置」に戻す
        // ・Y/Zはそのまま維持する
        pos.x = ResetX;

        // 新しい座標を反映
        CachedTransform.position = pos;
    }
}
