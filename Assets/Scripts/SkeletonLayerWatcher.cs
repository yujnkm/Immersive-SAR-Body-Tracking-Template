using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkeletonLayerWatcher : MonoBehaviour
{
    int trackedLayer;
    HashSet<ZEDSkeletonAnimator> processed = new HashSet<ZEDSkeletonAnimator>();

    void Start() => trackedLayer = LayerMask.NameToLayer("TrackedSkeleton");

    void LateUpdate()                      // runs once per frame
    {
        foreach (var zedAnim in FindObjectsOfType<ZEDSkeletonAnimator>())
        {
            if (processed.Contains(zedAnim)) continue;         // already done
            SetLayerRecursively(zedAnim.transform, trackedLayer);
            processed.Add(zedAnim);
        }

        foreach (var debugRoot in GameObject.FindObjectsOfType<Transform>())
            if (debugRoot.name.StartsWith("Skeleton_ID_"))
                SetLayerRecursively(debugRoot, trackedLayer);
    }

    void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform c in t) SetLayerRecursively(c, layer);
    }
}
