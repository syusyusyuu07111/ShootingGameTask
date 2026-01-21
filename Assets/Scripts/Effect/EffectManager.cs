using UnityEngine;

/*
    エフェクト生成を一括管理するクラス
    ・敵死亡などのタイミングでエフェクトPrefabを生成
    ・一定時間後に自動でDestroyする
*/
public class EffectManager : MonoBehaviour
{
    [Header("Effect Settings")]

    [Tooltip("生成するエフェクトPrefab")]
    public GameObject Eff;

    [Tooltip("エフェクトを消すまでの時間（秒）")]
    public float EffTime = 2f;

    /*
        指定した座標にエフェクトを生成する
        主に「敵がやられた位置」から呼ばれる
    */
    public void PlayEffect(Vector3 DiePos)
    {
        // エフェクトPrefabが未設定なら処理しない
        if (Eff == null)
        {
            Debug.LogError("[EffectManager] Effect Prefab is not set.");
            return;
        }

        // Z座標を0に固定
        // 2Dゲームで「カメラより手前/奥」に行って見えなくなる事故防止
        DiePos.z = 0f;

        // エフェクトを生成
        GameObject Effect = Instantiate(Eff, DiePos, Quaternion.identity);

        // EffTime秒後に自動で削除
        // パーティクルや演出が終わったら残らないようにする
        Destroy(Effect, EffTime);
    }
}
