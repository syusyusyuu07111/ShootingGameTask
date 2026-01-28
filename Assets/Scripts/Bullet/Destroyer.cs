using System.Collections;
using UnityEngine;

/*
     一定時間後にオブジェクトを消すためのクラス

     ・寿命タイマーが切れたら
       PoolManager があればプールに返却
       無ければ Destroy する
*/
public sealed class Destroyer : MonoBehaviour
{
    //================
    // Pool Reference
    //================

    private PoolManager OwnerPool;

    //================
    // Routine
    //================

    private Coroutine LifeRoutine;

    //================
    // Public
    //================

    /*
         PoolManager を設定する
    */
    public void SetPoolManager(PoolManager Pool)
    {
        OwnerPool = Pool;
    }

    /*
         寿命タイマーを開始（またはリセット）する
    */
    public void StartDestroyTimer(float LifeTimeSeconds)
    {
        if (LifeTimeSeconds <= 0f)
        {
            Debug.LogError($"[Destroyer] LifeTimeSeconds が不正です life={LifeTimeSeconds} name={name}");
            return;
        }

        if (LifeRoutine != null) StopCoroutine(LifeRoutine);

        LifeRoutine = StartCoroutine(LifeTimer(LifeTimeSeconds));
    }

    //================
    // Unity Event
    //================

    private void OnDisable()
    {
        /*
             プール返却（非アクティブ化）でも呼ばれるので参照をリセットする
        */
        LifeRoutine = null;
    }

    //================
    // Internal
    //================

    private IEnumerator LifeTimer(float LifeTimeSeconds)
    {
        yield return new WaitForSeconds(LifeTimeSeconds);

        if (OwnerPool != null)
        {
            OwnerPool.ReleaseGameObject(gameObject);
            yield break;
        }

        Destroy(gameObject);
    }
}
