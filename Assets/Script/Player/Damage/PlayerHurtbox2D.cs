using UnityEngine;

public class PlayerHurtbox2D : MonoBehaviour
{
    public PlayerController Player { get; private set; }

    private void Awake()
    {
        Player = GetComponentInParent<PlayerController>();
    }
}
