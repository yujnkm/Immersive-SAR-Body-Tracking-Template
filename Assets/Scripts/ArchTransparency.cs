using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArchTransparency : MonoBehaviour
{
    public Material newMaterial;
    void Start()
    {
        ChangeMaterialOnChildren();
    }


    public void ChangeMaterialOnChildren()
    {
        Renderer[] childrenRenderers = GetComponentsInChildren<Renderer>();

        foreach (Renderer childRenderer in childrenRenderers)
        {
            if (childRenderer.sharedMaterial != newMaterial)
            {
                childRenderer.sharedMaterial = newMaterial;
            }

            Debug.Log("Transparent Arch");
        }
    }
}