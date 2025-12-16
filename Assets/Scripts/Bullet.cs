using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public static readonly List<Bullet> AllBullets = new List<Bullet>();

    void OnEnable()
    {
        if (!AllBullets.Contains(this))
            AllBullets.Add(this);
    }

    void OnDisable()
    {
        AllBullets.Remove(this);
    }
}
