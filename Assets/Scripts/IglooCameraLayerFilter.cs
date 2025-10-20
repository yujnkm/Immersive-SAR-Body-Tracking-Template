using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Igloo.Common;

public class IglooCameraLayerFilter: MonoBehaviour
{
    const string HIDDEN_LAYER = "TrackedSkeleton";
    int hiddenMask;
    HashSet<Camera> done = new HashSet<Camera>();

    void Awake() => hiddenMask = 1 << LayerMask.NameToLayer(HIDDEN_LAYER);

    void LateUpdate()
    {
        // IglooManager builds cameras via code, so at every frame we get those cameras in case they update
        var igloo = IglooManager.instance;
        if (igloo == null || igloo.DisplayManager == null) return;

        foreach (Camera cam in igloo.DisplayManager.GetCameras())
        {
            if (cam == null || done.Contains(cam)) continue;
            cam.cullingMask &= ~hiddenMask;      // strip layer using bitwise operator
            done.Add(cam);
        }
    }
}
