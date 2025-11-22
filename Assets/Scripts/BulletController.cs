using UnityEngine;

public class BulletController : MonoBehaviour
{
    public Transform Player;
    public GameObject Bullet;
    InputSystem_Actions input;

    private void Awake()
    {
        input = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        input.Player.Attack.Enable();
    }

    // Update is called once per frame
    void Update()
    {
        if(input.Player.Attack.WasPressedThisFrame())
        {
            Instantiate(Bullet, Player.position, Quaternion.identity);
        }
    }
}
