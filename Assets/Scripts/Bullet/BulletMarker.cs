using System.Collections.Generic;
using UnityEngine;

/*
     弾のマーカーを管理するクラス

     【主な役割】
     ・シーン上に存在する「有効なBulletMarker」を一括で記録する
     ・外部（UIや演出など）が「全マーカー」を参照するために使う

     【設計方針】
     ・OnEnableで登録し、OnDisableで解除する
     ・Destroy / Pool返却どちらでも正しく一覧から外れる
     ・外部からListを直接編集できないように読み取り専用で公開する
*/
public sealed class BulletMarker : MonoBehaviour
{
    //================
    // All Markers
    //================

    /*
         現在「有効な」BulletMarker一覧（内部管理用）

         ・登録/解除は BulletMarker 自身が行う
         ・外部がAdd/Removeすると管理が壊れるので private にする
    */
    private static readonly List<BulletMarker> AllMarkersInternal = new List<BulletMarker>();

    /*
         外部公開用（読み取り専用）

         ・シーン上のマーカーを一括参照したい側が使う
         ・IReadOnlyListで返して、外側からAdd/Removeできないようにする
    */
    public static IReadOnlyList<BulletMarker> AllMarkers => AllMarkersInternal;


    //================
    // Unity Event
    //================

    private void OnEnable()
    {
        /*
             GameObject が有効化された瞬間に呼ばれる

             ・生成時
             ・オブジェクトプールから再利用された時
             上記のタイミングで「シーン上に存在するマーカー」として扱う
        */

        /*
             二重登録防止

             ・通常はOnEnableが重複しない想定だが
             ・特殊な有効/無効切り替えや再入の保険として入れている
        */
        if (!AllMarkersInternal.Contains(this))
            AllMarkersInternal.Add(this);
    }

    private void OnDisable()
    {
        /*
             GameObject が無効化された瞬間に呼ばれる

             ・Destroy された時
             ・オブジェクトプールに戻った時
             上記のタイミングで「シーン上に存在しないマーカー」として扱う
        */

        // 管理リストから除外する
        AllMarkersInternal.Remove(this);
    }
}
