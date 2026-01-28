using System.Collections.Generic;
using UnityEngine;

/*
     弾の管理クラス（当たり判定対応版）

     【役割】
     ・全弾をリストで管理する（EnemyController用）
     ・通常弾 / チャージ弾の当たり半径を管理する
*/
public sealed class Bullet : MonoBehaviour
{
    //================
    // All Bullets
    //================

    /*
         現在有効な弾の一覧
    */
    public static readonly List<Bullet> AllBullets = new List<Bullet>();


    //================
    // Hit Radius
    //================

    [Header("Hit Settings")]

    [Tooltip("通常時の当たり半径")]
    [SerializeField] private float BaseHitRadius = 0.4f;

    /*
         チャージ時などで倍率をかける用
         （1 = 通常）
    */
    private float HitRadiusMultiplier = 1f;


    //================
    // Unity Event
    //================

    private void OnEnable()
    {
        if (!AllBullets.Contains(this))
        {
            AllBullets.Add(this);
        }

        // プール再利用対策：倍率を初期化
        HitRadiusMultiplier = 1f;
    }

    private void OnDisable()
    {
        AllBullets.Remove(this);
    }


    //================
    // Public API
    //================

    /*
         EnemyController が使う当たり半径
    */
    public float GetHitRadius()
    {
        return BaseHitRadius * HitRadiusMultiplier;
    }

    /*
         チャージ用：半径倍率を変更
    */
    public void SetHitRadiusMultiplier(float multiplier)
    {
        HitRadiusMultiplier = Mathf.Max(0.01f, multiplier);
    }
}
