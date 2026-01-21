using System.Collections;
using UnityEngine;

/*
     一定時間後にオブジェクトを消すためのクラス
     ・弾の寿命管理に使う
     ・一定時間後に自動で消す
     ・PoolManager が設定されていればプールに返却する
     ・PoolManager が無ければ通常の Destroy を行う

     【オブジェクトプールについて】
     あらかじめ一定数のオブジェクトを作っておき、
     使い終わったら非アクティブ化して再利用する仕組み
     PoolManager はこの仕組みを管理するクラス
*/
public class Destroyer : MonoBehaviour
{
    /*
         オブジェクトプール管理者
         ・設定されていれば Destroy せずプールに戻す
         ・未設定なら通常の Destroy を行う
    */
    public PoolManager PoolManager { get; set; }

    /*
         現在動作中の破棄タイマー
         ・StartDestroyTimer が複数回呼ばれても
           タイマーが多重起動しないように管理する
    */
    Coroutine Routine;

    //================
    // 公開処理
    //================

    public void StartDestroyTimer(float Time)
    {
        /*
             すでにタイマーが動いている場合は一度止める
             ・多重起動防止
             ・寿命のリセット目的
        */
        if (Routine != null) StopCoroutine(Routine);

        // 新しい破棄タイマーを開始する
        Routine = StartCoroutine(DestroyTimer(Time));
    }

    //================
    // 内部処理
    //================

    IEnumerator DestroyTimer(float Time)
    {
        // 指定時間待機する
        yield return new WaitForSeconds(Time);

        // PoolManager が設定されているかどうかで処理を分ける
        if (PoolManager != null) PoolManager.ReleaseGameObject(gameObject);
        else Destroy(gameObject);

        // タイマー終了（次回に備えてリセット）
        Routine = null;
    }
}
