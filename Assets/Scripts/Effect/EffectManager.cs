using UnityEngine;

public class EffectManager : MonoBehaviour
{
    [Header("Effect Settings")]
    [Tooltip("生成するエフェクトPrefab")]
    public GameObject Eff;

    [Tooltip("エフェクトを消すまでの時間")]
    public float EffTime = 2f;

    public void PlayEffect(Vector3 diePos)
    {

        if (Eff == null)
        {
            return;
        }

        diePos.z = 0f;

        GameObject effect = Instantiate(Eff, diePos, Quaternion.identity);

        Destroy(effect, EffTime);
    }
}
