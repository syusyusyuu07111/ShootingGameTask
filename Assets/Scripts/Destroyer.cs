using System.Collections;
using UnityEngine;

/// <summary>
/// 一定時間後にオブジェクトを消す>弾を一定時間後に消す
/// ・弾の寿命管理に使用
/// ・PoolManager があればプールへ返却
/// ・無ければ通常の Destroy を行う
/// </summary>
public class Destroyer : MonoBehaviour
{
    /// <summary>
    /// オブジェクトプール管理者（任意）
    /// ・設定されていれば Destroy せずプールに戻す
    /// ・未設定なら通常 Destroy
    /// </summary>
    public PoolManager PoolManager { get; set; }

    // 現在動作中の破棄タイマー（多重起動防止用）
    Coroutine routine;

    /// <summary>
    /// 破棄タイマーを開始する
    /// ・弾発射時などに呼ばれる
    /// ・すでにタイマーが動いていたらリセットする
    /// </summary>
    /// <param name="time">破棄までの待ち時間（秒）</param>
    public void StartDestroyTimer(float time)
    {
        // すでに動いているタイマーがあれば停止
        if (routine != null)
            StopCoroutine(routine);

        // 新しいタイマーを開始
        routine = StartCoroutine(DestroyTimer(time));
    }

    /// <summary>
    /// 指定時間待ってからオブジェクトを破棄 / プール返却する
    /// </summary>
    IEnumerator DestroyTimer(float time)
    {
        // 指定時間待つ
        yield return new WaitForSeconds(time);

        // PoolManager がある場合はプールに返す
        if (PoolManager != null)
        {
            PoolManager.ReleaseGameObject(gameObject);
        }
        // PoolManager が無ければ通常 Destroy
        else
        {
            Destroy(gameObject);
        }

        // タイマー終了
        routine = null;
    }
}
