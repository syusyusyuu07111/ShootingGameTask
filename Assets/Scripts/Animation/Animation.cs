using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 連番スプライトによるアニメーションを制御するコンポーネント
/// </summary>
public class Animation : MonoBehaviour
{
    /// <summary>
    /// アニメーションに使用するスプライトのリスト（Inspectorで設定）
    /// </summary>
    public List<Sprite> frames = new List<Sprite>();

    /// <summary>
    /// 1秒間に何フレーム進めるか（アニメーション速度）
    /// </summary>
    public float Speed = 12f;

    /// <summary>
    /// 現在表示中のスプライトのインデックス
    /// </summary>
    int frameIndex = 0;

    /// <summary>
    /// 前のフレームからの経過時間
    /// </summary>
    float timer = 0f;

    /// <summary>
    /// スプライトを描画するためのSpriteRenderer
    /// </summary>
    SpriteRenderer sr;

    /// <summary>
    /// 初期化処理。SpriteRendererを取得する
    /// </summary>
    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// 毎フレーム呼ばれ、アニメーションの進行を制御する
    /// </summary>
    void Update()
    {
        // スプライトが設定されていない場合は何もしない
        if (frames.Count == 0)
        {
            return;
        }

        // 経過時間を加算
        timer += Time.deltaTime;

        // 1フレームあたりの表示時間を計算
        float frameTime = 1f / Speed;

        // 経過時間が1フレーム分を超えた場合、次のスプライトへ
        if (timer >= frameTime)
        {
            // 経過時間から1フレーム分を減算（余剰時間は次フレームに持ち越し）
            timer -= frameTime;

            // 次のスプライトへインデックスを進める
            frameIndex++;

            // インデックスがリストの範囲外になったら最初に戻す
            if (frameIndex >= frames.Count)
            {
                frameIndex = 0;
            }

            // 現在のインデックスのスプライトを表示
            sr.sprite = frames[frameIndex];
        }
    }
}
