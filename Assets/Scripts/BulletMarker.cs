using System.Collections.Generic;
using UnityEngine;

public class BulletMarker : MonoBehaviour
{
    public static readonly List<BulletMarker> All = new List<BulletMarker>();

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }
}
