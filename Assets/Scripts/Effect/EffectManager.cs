using UnityEngine;

/*
     エフェクト生成を一括管理するクラス

     【主な役割】
     ・敵死亡などのタイミングでエフェクトPrefabを生成する
     ・一定時間後に自動でDestroyして残骸を残さない

     【設計方針】
     ・生成処理は呼ばれたタイミングだけ行う（Updateは使わない）
     ・Prefab未設定は不具合なので必ずエラーを出す
     ・生成座標のZは0に固定する
*/
public sealed class EffectManager : MonoBehaviour
{
    //================
    // Effect Settings
    //================

    [Header("Effect Settings")]

    [Tooltip("生成するエフェクトPrefab")]
    [SerializeField] private GameObject Eff;

    [Tooltip("エフェクトを消すまでの時間（秒）")]
    [SerializeField] private float EffTime = 2f;


    //================
    // Play
    //================

    /*
         指定した座標にエフェクトを生成する

         【想定用途】
         ・敵が倒された位置
         ・何かが破壊された位置
         ・ヒット演出の位置

         ・生成したエフェクトは一定時間後にDestroyする
    */
    public void PlayEffect(Vector3 Pos)
    {
        /*
             Prefab未設定は不具合

             ・エフェクトが出ないのは演出として致命
             ・呼び出し側のミスに気づけるようエラーを出す
        */
        if (Eff == null)
        {
            Debug.LogError("[EffectManager] Eff が未設定です");
            return;
        }

        //================
        // Position 修正
        //================

        /*
             Z座標を0に固定する

             ・2Dゲームで「カメラより手前/奥」に行って見えなくなる事故防止
             ・Zを使って演出する設計に変える場合はここを見直す
        */
        Pos.z = 0f;

        //================
        // Instantiate
        //================

        /*
             エフェクトを生成する

             ・回転は固定（必要なら引数で渡す設計に拡張できる）
        */
        GameObject EffectInstance = Instantiate(Eff, Pos, Quaternion.identity);

        //================
        // Auto Destroy
        //================

        /*
             一定時間後に自動削除する

             ・パーティクルが終了しても残り続けないようにする
             ・短すぎると演出が途中で消えるため、EffTimeで調整する
        */
        Destroy(EffectInstance, EffTime);
    }
}
