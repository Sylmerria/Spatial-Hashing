using UnityEngine;

namespace HMH.ECS.SpatialHashing.Debug
{
    public class HashTeleport : MonoBehaviour
    {
        private void Update()
        {
            if (Random.Range(0, 3) == 2)
                transform.position += Random.insideUnitSphere * 5F;
        }
    }
}