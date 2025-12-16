using System.Collections;
using UnityEngine;

public class Destroyer : MonoBehaviour
{
    public PoolManager PoolManager { get; set; }

    Coroutine routine;

    public void StartDestroyTimer(float time)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(DestroyTimer(time));
    }

    IEnumerator DestroyTimer(float time)
    {
        yield return new WaitForSeconds(time);

        if (PoolManager != null)
            PoolManager.ReleaseGameObject(gameObject);
        else
            Destroy(gameObject);

        routine = null;
    }
}
