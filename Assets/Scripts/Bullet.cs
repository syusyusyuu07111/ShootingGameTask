using System.Collections.Generic;
using UnityEngine;

/*
     弾を管理するためのクラス
     現在シーン内に存在している弾をすべて記録する
*/
public class Bullet : MonoBehaviour
{
    /*
         現在「生きている」弾の一覧
         ・EnemyController から参照
         ・Destroy / Pool で非アクティブになると自動で外れる
    */
    public static readonly List<Bullet> AllBullets = new List<Bullet>();

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
        if (!AllBullets.Contains(this)) AllBullets.Add(this);
    }

    void OnDisable()
    {
        /*
             GameObject が無効化された瞬間に呼ばれる
             ・Destroy された時
             ・オブジェクトプールに戻った時
        */

        // 管理リストから除外
        AllBullets.Remove(this);
    }
}
