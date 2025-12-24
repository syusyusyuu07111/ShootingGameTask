using System.Collections;
using UnityEngine;

/// <summary>
/// 一定時間後にオブジェクトを消す（弾の寿命管理用）
/// ・弾の寿命を管理し、一定時間後に自動で消す
/// ・PoolManager が設定されていればプールに返却
/// ・PoolManager が無ければ通常の Destroy を行う
///
/// オブジェクトプール
/// あらかじめ一定数のオブジェクトを作っておき、
/// 使い終わったら非アクティブ化して再利用する仕組み
/// PoolManager はこの仕組みを管理するクラス
/// </summary>
public class Destroyer : MonoBehaviour
{
    /// <summary>
    /// オブジェクトプール管理者（任意）
    /// ・設定されていれば Destroy せずプールに戻す
    /// ・未設定なら通常 Destroy
    /// </summary>
    public PoolManager PoolManager { get; set; }

    /// <summary>
    /// 現在動作中の破棄タイマー（多重起動防止用）
    /// ・StartDestroyTimerが複数回呼ばれてもタイマーが重複しないよう管理
    /// </summary>
    private Coroutine routine;

    /// <summary>
    /// 指定した秒数後にオブジェクトを破棄するタイマーを開始する
    /// ・弾発射時などに呼び出す
    /// ・すでにタイマーが動いていた場合はリセットして再スタート
    /// </summary>
    /// <param name="time">破棄までの待ち時間（秒）</param>
    public void StartDestroyTimer(float time)
    {
        // すでに動いているタイマーがあれば停止（多重起動防止）
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        // 新しいタイマーを開始
        routine = StartCoroutine(DestroyTimer(time));
    }

    /// <summary>
    /// 指定時間待ってからオブジェクトを破棄またはプールに返却するコルーチン
    /// </summary>
    /// <param name="time">待機時間（秒）</param>
    /// <returns>IEnumerator（コルーチン用）</returns>
    private IEnumerator DestroyTimer(float time)
    {
        // 指定時間だけ待機
        yield return new WaitForSeconds(time);

        // PoolManager が設定されていればプールに返却
        if (PoolManager != null)
        {
            PoolManager.ReleaseGameObject(gameObject);
        }
        // PoolManager が未設定なら通常通り破棄
        else
        {
            Destroy(gameObject);
        }

        // タイマー終了（多重起動防止用のフラグをリセット）
        routine = null;
    }
}
