using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 弾のマーカーを管理するクラス
/// シーン上に存在する全てのBulletMarkerインスタンスをリストで管理します
/// </summary>
public class BulletMarker : MonoBehaviour
{
    /// <summary>
    /// シーン上に存在する全てのBulletMarkerインスタンスを格納する静的リスト
    /// </summary>
    public static readonly List<BulletMarker> All = new List<BulletMarker>();

    /// <summary>
    /// このオブジェクトが有効化された時に呼ばれる
    /// リストに自身が含まれていなければ追加する
    /// </summary>
    void OnEnable()
    {
        if (!All.Contains(this))
        {
            All.Add(this);
        }
    }

    /// <summary>
    /// このオブジェクトが無効化された時に呼ばれる
    /// リストから自身を削除する
    /// </summary>
    void OnDisable()
    {
        All.Remove(this);
    }
}
