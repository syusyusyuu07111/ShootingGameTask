using System.Collections;
using UnityEngine;

public class Destroyer : MonoBehaviour
{

    public PoolManager PoolManager { get; set; }

    public void StartDestroyTimer(float time)
    {
        StartCoroutine(DestroyTimer(time));
    }

    IEnumerator DestroyTimer(float time)
    {
        yield return new WaitForSeconds(time);

        if (PoolManager != null)
        {
            PoolManager.ReleaseGameObject(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

}