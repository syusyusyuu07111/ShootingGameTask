using System.Collections.Generic;
using UnityEngine;

public class Animation : MonoBehaviour
{
    // 連番スプライトをInspectorで登録
    public List<Sprite> frames = new List<Sprite>();

    public float Speed = 12f;//何秒ごとに画像を変えるか

    int frameIndex = 0;//表示する画像番号
    float timer = 0f;//前のコマからどのくらい時間がたったか

    SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (frames.Count == 0)
        {
            return;
        }

        timer += Time.deltaTime;

        float frameTime = 1f / Speed;
        if (timer >= frameTime)
        {
            timer -= frameTime;//経過時間が指定の秒数に行ったら余った秒を捨てる　かくつかないように0にせずに引く　次のコマに行くときに余った時間を次のコマに持ってく
            frameIndex++;//indexを足し続ける
            //indexが最後まで行ったら最初に戻す　今入ってる要素数まで行ったら
            if (frameIndex >= frames.Count)
            {
                frameIndex = 0;
            }
            //足したindexの画像を表示させる
            sr.sprite = frames[frameIndex];
        }
    }
}
