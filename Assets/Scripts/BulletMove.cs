using UnityEngine;

/*
     弾を一定方向に移動させるクラス

     ・毎フレーム、弾を右方向へ移動させる
     ・速度は外部（PoolManagerなど）から設定できるようにする
*/
public sealed class BulletMove : MonoBehaviour
{
    //================
    // Move Settings
    //================

    [SerializeField] private float Speed = 5f;

    //================
    // Cache
    //================

    private Transform CachedTransform;

    //================
    // Public
    //================

    /*
         速度を設定する

         ・Speed を public にせず、設定口を関数にして事故を減らす
         ・PoolManager側からここを呼んで速度を統一管理する
    */
    public void SetSpeed(float Value)
    {
        Speed = Value;
    }

    //================
    // Unity Event
    //================

    private void Awake()
    {
        CachedTransform = transform;
    }

    private void Update()
    {
        CachedTransform.position += Vector3.right * Speed * Time.deltaTime;
    }
}
