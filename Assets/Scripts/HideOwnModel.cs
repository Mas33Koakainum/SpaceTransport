using UnityEngine;
using Unity.Netcode;

public class HideOwnModelPart : NetworkBehaviour
{
    [SerializeField] private Renderer[] partsToHide;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        foreach (Renderer part in partsToHide)
        {
            if (part != null)
                part.enabled = false;
        }
    }
}