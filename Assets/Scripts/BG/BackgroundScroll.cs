using UnityEngine;

public class BackgroundScroll : MonoBehaviour
{
    [Header("Scroll Settings")]
    public float scrollSpeed = 3.0f;

    [Tooltip("このX座標を超えたらループ")]
    public float resetBorderX = -4935f;

    [Tooltip("ループ後に戻すX座標")]
    public float resetX = 23f;

    void Update()
    {
        // 毎フレーム左にスクロール
        transform.position += Vector3.left * scrollSpeed * Time.deltaTime;

        // 一定位置まで行ったらXを戻す
        if (transform.position.x <= resetBorderX)
        {
            Vector3 pos = transform.position;
            pos.x = resetX;
            transform.position = pos;
        }
    }
}
