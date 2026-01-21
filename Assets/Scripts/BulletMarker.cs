using System.Collections.Generic;
using UnityEngine;

/*
     弾のマーカーを管理するクラス
     シーン上に存在するすべての BulletMarker を一覧で管理する
*/
public class BulletMarker : MonoBehaviour
{
    /*
         現在「有効な」BulletMarker の一覧
         ・シーン上に存在するマーカーを一括で参照するために使う
         ・Disable / Destroy されたものは自動で外れる
    */
    public static readonly List<BulletMarker> All = new List<BulletMarker>();

    //================
    // Unity Event
    //================

    void OnEnable()
    {
        /*
             GameObject が有効化された瞬間に呼ばれる
             ・生成時
             ・オブジェクトプールから再利用された時
        */

        // 二重登録防止
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        /*
             GameObject が無効化された瞬間に呼ばれる
             ・Destroy された時
             ・オブジェクトプールに戻った時
        */

        // 管理リストから除外
        All.Remove(this);
    }
}
