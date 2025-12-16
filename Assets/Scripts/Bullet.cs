using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public static List<Bullet> AllBullets = new List<Bullet>();

    void OnEnable()
    {
        // プールから出てきた or 生成されたときに登録
        AllBullets.Add(this);
    }

    void OnDisable()
    {
        // プールに戻る or Destroy されたときに解除
        AllBullets.Remove(this);
    }
}
