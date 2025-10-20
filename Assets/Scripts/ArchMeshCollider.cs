using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

[DisallowMultipleComponent]
public class ArchMeshCollider : MonoBehaviour
{
    void Awake()
    {
        foreach (Transform child in transform)
        {

            Debug.Log("Added Mesh Collider!");

            // Add or reuse a MeshCollider
            var col = child.GetComponent<MeshCollider>() ??
                      child.gameObject.AddComponent<MeshCollider>();

            col.convex = false;                       
        }
    }

    static bool IsArch(string n) => n == "Arch" || Regex.IsMatch(n, @"Arch\s*\([0-6]\)$");
}



