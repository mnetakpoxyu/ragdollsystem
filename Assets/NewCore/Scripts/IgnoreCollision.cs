using UnityEngine;

public class IgnoreCollision : MonoBehaviour
{
    [SerializeField] private Collider thisCollider;
    [SerializeField] private Collider[] collidersToIgnore;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        foreach (Collider otherCollider in collidersToIgnore)
        {
            Physics.IgnoreCollision(thisCollider, otherCollider,true);
        }
    }
}
