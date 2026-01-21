using System.Collections.Generic;
using UnityEngine;

public class Animation : MonoBehaviour
{
    /*
        アニメーションに使用するスプライトのリスト
        Inspectorで設定する
    */
    public List<Sprite> Frames = new List<Sprite>();

    /*
        1秒間に何フレーム進めるか
        アニメーションの再生速度
    */
    public float Speed = 12f;

    /*
        現在表示中のスプライトのインデックス
    */
    int FrameIndex = 0;

    /*
        前のフレームからの経過時間
    */
    float Timer = 0f;

    /*
        スプライトを描画するためのSpriteRenderer
    */
    SpriteRenderer sr;

    /*
        初期化処理
        SpriteRendererを取得する
    */
    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        if (sr == null)
        {
            Debug.LogError("SpriteRenderer is not set.");
            enabled = false;
        }

        if (Speed <= 0f)
        {
            Debug.LogError("Speed must be greater than 0.");
            enabled = false;
        }
    }

    /*
        毎フレーム呼ばれる
        アニメーションの進行を制御する
    */
    void Update()
    {
        // スプライトが設定されていない場合は何もしない
        if (Frames.Count == 0) return;

        // 経過時間を加算
        Timer += Time.deltaTime;

        // 1フレームあたりの表示時間
        float FrameTime = 1f / Speed;

        if (Timer < FrameTime) return;

        // 経過時間から1フレーム分を減算
        // 余剰時間は次フレームに持ち越す
        Timer -= FrameTime;

        // 次のスプライトへインデックスを進める
        FrameIndex++;

        // インデックスがリストの範囲外になったら最初に戻す
        if (FrameIndex >= Frames.Count) FrameIndex = 0;

        // 現在のインデックスのスプライトを表示
        sr.sprite = Frames[FrameIndex];
    }
}
