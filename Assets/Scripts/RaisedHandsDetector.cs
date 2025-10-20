using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class RaisedHandsDetector : MonoBehaviour
{
    [Tooltip("Main ZED Component")]
    public GameObject bodyTrackingViewer;

    [Header("Raycast Settings")]
    [Tooltip("Layer(s) to cast rays and bubbles into")]
    public LayerMask torusLayerMask;
    [Tooltip("Max distance (in meters) to check for a hit.")]
    public float rayDistance = 20f;

    [Header("Splash Settings")]
    public GameObject splashPrefab;
    public float splashInterval = 5f;      // perhand cooldown
    public float handOffset = 0.5f;    // gap between L & R same person
    public float splashLifetime = 2f;

    [Header("Ripple Settings")]
    public GameObject ripplePrefab;
    public float rippleLifetime = 2f;
    public float rippleOffset = 0.01f;   // behind surface
    public bool rippleUseSplashColor = false;

   

    private ZEDBodyTrackingManager tm;

    private readonly Dictionary<int, bool> leftHandActive = new();
    private readonly Dictionary<int, bool> rightHandActive = new();

    // per-hand last-time dictionary exactly as before
    private readonly Dictionary<string, float> lastSplash = new();

    // **NEW**  per-person “global” cooldown: one entry per personId
    private readonly Dictionary<int, float> nextAllowed = new();

    private readonly Dictionary<int, Gradient> personGradients = new();


    void Start()
    {
        if (!bodyTrackingViewer)
        {
            Debug.LogError("Please assign bodyTrackingViewer!");
            return;
        }
        InitializeBodyTrackingManager();
    }

    void InitializeBodyTrackingManager()
    {
        tm = bodyTrackingViewer.GetComponent<ZEDBodyTrackingManager>();
        if (!tm)
        {
            Debug.LogError("ZEDBodyTrackingManager not found!");
            return;
        }
        leftHandActive.Clear();
        rightHandActive.Clear();
        nextAllowed.Clear();
    }

    void Update()
    {
        if (!tm) { InitializeBodyTrackingManager(); if (!tm) return; }

        var list = tm.avatarControlList;
        if (list == null) return;

        var currentIds = new HashSet<int>(list.Keys);

        foreach (int pid in currentIds)
        {
            Vector3[] joints = list[pid].currentJoints;

            /* DEMO  always raised */
            bool isLeftRaised = true;
            bool isRightRaised = true;

            if (isLeftRaised)
                CastFromWrist(pid, true,
                    joints[SkeletonHandler.JointType_LEFT_ELBOW],
                    joints[SkeletonHandler.JointType_LEFT_WRIST]);

            if (isRightRaised)
                CastFromWrist(pid, false,
                    joints[SkeletonHandler.JointType_RIGHT_ELBOW],
                    joints[SkeletonHandler.JointType_RIGHT_WRIST]);

            leftHandActive[pid] = isLeftRaised;
            rightHandActive[pid] = isRightRaised;
        }

        /* clean-up for people who left */
        var gone = leftHandActive.Keys.Except(currentIds).ToList();
        foreach (int pid in gone)
        {
            leftHandActive.Remove(pid);
            rightHandActive.Remove(pid);
            nextAllowed.Remove(pid);
        }
    }


    void CastFromWrist(int pid, bool isLeft, Vector3 elbowPos, Vector3 wristPos)
    {
        Vector3 dir = (wristPos - elbowPos).normalized;

        if (Physics.Raycast(wristPos, dir, out RaycastHit hit,
                            rayDistance, torusLayerMask))
        {
            Debug.DrawRay(wristPos, dir * rayDistance, Color.red);
            TrySpawnSplash(pid, isLeft, hit);
        }
        else
            Debug.DrawRay(wristPos, dir * rayDistance, Color.green);
    }


    void TrySpawnSplash(int pid, bool isLeft, RaycastHit hit)
    {
        float now = Time.time;

        // get person-specific window (defaults to 0)
        nextAllowed.TryGetValue(pid, out float personNextAllowed);
        if (now < personNextAllowed) return;

        string thisKey = $"{pid}_{(isLeft ? "L" : "R")}";
        string otherKey = $"{pid}_{(isLeft ? "R" : "L")}";

        if (lastSplash.TryGetValue(thisKey, out float lastT) &&
            now - lastT < splashInterval) return;

        if (lastSplash.TryGetValue(otherKey, out float otherT) &&
            now - otherT < handOffset) return;

        Vector3 pos = hit.point + hit.normal * 0.01f;
        Quaternion rot = Quaternion.LookRotation(hit.normal);
        Gradient g = GetGradientForPerson(pid);

        GameObject splash = Instantiate(splashPrefab, pos, rot);
        Destroy(splash, splashLifetime);
        ApplyGradientRecursively(splash.transform, g);

        if (ripplePrefab)
        {
            Vector3 posR = hit.point - hit.normal * rippleOffset;
            GameObject ripple = Instantiate(ripplePrefab, posR, rot);
            Destroy(ripple, rippleLifetime);

            ApplyGradientRecursively(
                ripple.transform,
                rippleUseSplashColor ? g : solidWhiteGradient);
        }

        PlayAudio(hit);

        /* update cooldowns */
        lastSplash[thisKey] = now;
        nextAllowed[pid] = now + handOffset;   // perperson gate
    }


    private static readonly Gradient solidWhiteGradient = new Gradient
    {
        colorKeys = new[] {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f) },
        alphaKeys = new[] {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(1f, 1f) }
    };

    static void ApplyGradientRecursively(Transform root, Gradient g)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(g);
        }
    }

        private readonly Dictionary<Collider, (AudioSource[] sources, int next)> clipBank = new();

    private void PlayAudio(RaycastHit hit)
    {
        Debug.LogWarning("Trying to play audio");
        Collider col = hit.collider;
        if (col == null)
        {
            Debug.LogWarning("Collider not found");
            return;
        }                   // safety
        Debug.LogWarning("Collider Found!!!");

        // Get (or lazily build) the clip sequence for this collider
        if (!clipBank.TryGetValue(col, out var bank))
        {
            Debug.LogWarning("No audio in the bank!!!");
            // First look on the collider itself, then on its parents
            AudioSource[] found = col.GetComponents<AudioSource>();
            if (found.Length == 0)
            {
                Debug.LogWarning("No audio source in the ARCH!!?");
                found = col.GetComponentsInParent<AudioSource>();
            }

            if (found.Length == 0)
            {
                Debug.LogWarning("No audio source in the parent as well!!! ");
                return;
            }

            Debug.LogWarning("Found Source(s), adding to bank!!");
            bank = (found, 0);                             // start at first clip
            clipBank[col] = bank;
        }

        AudioSource src = bank.sources[bank.next];

        if (src != null && src.clip != null)
        {
            Debug.LogWarning("after all that BS, somehow, no source and no clip?!");
            src.Stop();
            src.time = 0f;
            src.Play();
        }

        // Advance index for next time - wraps automatically
        bank.next = (bank.next + 1) % bank.sources.Length;
        clipBank[col] = bank;                              // write back the tuple
    }

    private Gradient GetGradientForPerson(int personId)
    {
        if (personGradients.TryGetValue(personId, out var g)) return g;

        Color c1 = Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f);
        Color c2 = Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f);

        g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(c1, 0f), new GradientColorKey(c2, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );

        personGradients[personId] = g;
        return g;
    }

    private bool IsLeftHandRaised(Vector3[] joints)
    {
        float wristY = joints[SkeletonHandler.JointType_LEFT_WRIST].y;
        float elbowY = joints[SkeletonHandler.JointType_LEFT_ELBOW].y;
        float shoulderY = joints[SkeletonHandler.JointType_LEFT_SHOULDER].y;
        float fingerY = joints[SkeletonHandler.JointType_38_LEFT_HAND_MIDDLE_4].y;

        return (wristY > shoulderY || wristY > elbowY) && (fingerY > wristY);
    }

    private bool IsRightHandRaised(Vector3[] joints)
    {
        float wristY = joints[SkeletonHandler.JointType_RIGHT_WRIST].y;
        float elbowY = joints[SkeletonHandler.JointType_RIGHT_ELBOW].y;
        float shoulderY = joints[SkeletonHandler.JointType_RIGHT_SHOULDER].y;
        float fingerY = joints[SkeletonHandler.JointType_38_RIGHT_HAND_MIDDLE_4].y;

        return (wristY > shoulderY || wristY > elbowY) && (fingerY > wristY);
    }
}





//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;


//    public class RaisedHandsDetector : MonoBehaviour
//{
//    [Tooltip("Main ZED Component")]
//    public GameObject bodyTrackingViewer;

//    [Header("Raycast Settings")]
//    [Tooltip("Layer to cast rays and bubbles into")]
//    public LayerMask torusLayerMask;

//    [Tooltip("Max distance (in meters) to check for a hit.")]
//    public float rayDistance = 20f;

//    [Header("Splash Settings")]
//    public GameObject splashPrefab;
//    public float splashInterval = 5f;
//    public float handOffset = 0.5f;
//    public float splashLifetime = 2f;

//    [Header("Ripple Settings")]
//    public GameObject ripplePrefab;
//    public float rippleLifetime = 2f;

//    [Tooltip("Negative offset (m) along the surface normal so the ripple renders behind the splash")]
//    public float rippleOffset = 0.01f;

//    [Tooltip("If true, ripple copies the splash gradient; if false, ripple is solid white")]
//    public bool rippleUseSplashColor = false;

//    private ZEDBodyTrackingManager tm;

//    private Dictionary<int, bool> leftHandActive = new();
//    private Dictionary<int, bool> rightHandActive = new();
//    private readonly Dictionary<string, float> lastSplash = new();
//    private float nextGlobalAllowed = 0f;

//    private readonly Dictionary<int, Gradient> personGradients = new();

//    void Start()
//    {
//        if (bodyTrackingViewer == null)
//        {
//            Debug.LogError("Please assign bodyTrackingViewer in the Inspector!");
//            return;
//        }

//        InitializeBodyTrackingManager();
//    }

//    void InitializeBodyTrackingManager()
//    {
//        tm = bodyTrackingViewer.GetComponent<ZEDBodyTrackingManager>();
//        if (tm == null)
//        {
//            Debug.LogError("ZEDBodyTrackingManager component not found on bodyTrackingViewer!");
//            return;
//        }

//        leftHandActive.Clear();
//        rightHandActive.Clear();
//    }

//    void Update()
//    {
//        if (tm == null)
//        {
//            InitializeBodyTrackingManager();
//            if (tm == null) return;
//        }

//        var avatarControlList = tm.avatarControlList;
//        if (avatarControlList == null) return;

//        var currentPersonIds = new HashSet<int>(avatarControlList.Keys);

//        foreach (int personKey in currentPersonIds)
//        {
//            Vector3[] joints = avatarControlList[personKey].currentJoints;

//            // --- FOR DEMO ONLY: Force hand raise to true ---
//            bool isLeftRaised = true;   // TODO: replace with IsLeftHandRaised(joints);
//            bool isRightRaised = true;  // TODO: replace with IsRightHandRaised(joints);

//            if (isLeftRaised)
//            {
//                CastFromWrist(
//                    ownerId: personKey,
//                    isLeft: true,
//                    elbowPos: joints[SkeletonHandler.JointType_LEFT_ELBOW],
//                    wristPos: joints[SkeletonHandler.JointType_LEFT_WRIST]
//                );
//            }

//            if (isRightRaised)
//            {
//                CastFromWrist(
//                    ownerId: personKey,
//                    isLeft: false,
//                    elbowPos: joints[SkeletonHandler.JointType_RIGHT_ELBOW],
//                    wristPos: joints[SkeletonHandler.JointType_RIGHT_WRIST]
//                );
//            }

//            leftHandActive[personKey] = isLeftRaised;
//            rightHandActive[personKey] = isRightRaised;
//        }

//        // Remove tracking for people no longer detected
//        var toRemove = leftHandActive.Keys.Except(currentPersonIds).ToList();
//        foreach (int gone in toRemove)
//        {
//            leftHandActive.Remove(gone);
//            rightHandActive.Remove(gone);
//        }
//    }

//    private void CastFromWrist(int ownerId, bool isLeft, Vector3 elbowPos, Vector3 wristPos)
//    {
//        Vector3 direction = (wristPos - elbowPos).normalized;

//        if (Physics.Raycast(wristPos, direction, out RaycastHit hitInfo, rayDistance, torusLayerMask))
//        {
//            Debug.LogFormat("{0} hand of person {1} hit '{2}' at distance {3:F2}m",
//                isLeft ? "Left" : "Right", ownerId, hitInfo.collider.name, hitInfo.distance);

//            Debug.DrawRay(wristPos, direction * rayDistance, Color.red);
//            TrySpawnSplash(ownerId, isLeft, hitInfo);
//        }
//        else
//        {
//            Debug.DrawRay(wristPos, direction * rayDistance, Color.green);
//        }
//    }

//    private void TrySpawnSplash(int ownerId, bool isLeft, RaycastHit hit)
//    {
//        string thisKey = $"{ownerId}_{(isLeft ? "L" : "R")}";
//        string otherKey = $"{ownerId}_{(isLeft ? "R" : "L")}";
//        float now = Time.time;

//        if (now < nextGlobalAllowed) return;
//        if (lastSplash.TryGetValue(thisKey, out float lastT) && now - lastT < splashInterval) return;
//        if (lastSplash.TryGetValue(otherKey, out float otherT) && now - otherT < handOffset) return;

//        Vector3 hitPos = hit.point + hit.normal * 0.01f;
//        Quaternion rot = Quaternion.LookRotation(hit.normal);
//        Gradient g = GetGradientForPerson(ownerId);   // cache once

//        /* ----------  Splash ---------- */
//        GameObject splash = Instantiate(splashPrefab, hitPos, rot);
//        Destroy(splash, splashLifetime);
//        ApplyGradientRecursively(splash.transform, g);

//        /* ----------  Ripple ---------- */
//        if (ripplePrefab != null)
//        {
//            Vector3 ripplePos = hit.point - hit.normal * rippleOffset;
//            GameObject ripple = Instantiate(ripplePrefab, ripplePos, rot);
//            Destroy(ripple, rippleLifetime);

//            if (rippleUseSplashColor)
//                ApplyGradientRecursively(ripple.transform, g);
//            else
//                ApplyGradientRecursively(ripple.transform, solidWhiteGradient);
//        }

//        PlayAudio(hit);

//        lastSplash[thisKey] = now;
//        nextGlobalAllowed = now + handOffset;
//    }

//    /* ================================================================
//     * Helper: walk hierarchy once and change every ParticleSystem.
//     * ================================================================ */
//    private static readonly Gradient solidWhiteGradient = new Gradient
//    {
//        colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
//        alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
//    };

//    private static void ApplyGradientRecursively(Transform root, Gradient gradient)
//    {
//        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))   // includeInactive = true
//        {
//            var col = ps.colorOverLifetime;
//            col.enabled = true;
//            col.color = new ParticleSystem.MinMaxGradient(gradient);
//        }
//    }


//    //private void PlayAudio(RaycastHit hit)
//    //{
//    //    AudioSource src = hit.collider.GetComponent<AudioSource>() ?? hit.collider.GetComponentInParent<AudioSource>();
//    //    if (src && src.clip)
//    //    {
//    //        src.Stop();
//    //        src.time = 0f;
//    //        src.Play();
//    //    }
//    //}

//    // One entry per collider that owns audio. Keeps the list plus the index to use next.
//    private readonly Dictionary<Collider, (AudioSource[] sources, int next)> clipBank = new();

//    private void PlayAudio(RaycastHit hit)
//    {
//        Debug.LogWarning("Trying to play audio");
//        Collider col = hit.collider;
//        if (col == null)
//        {
//            Debug.LogWarning("Collider not found");
//            return;
//        }                   // safety
//        Debug.LogWarning("Collider Found!!!");

//        // Get (or lazily build) the clip sequence for this collider
//        if (!clipBank.TryGetValue(col, out var bank))
//        {
//            Debug.LogWarning("No audio in the bank!!!");
//            // First look on the collider itself, then on its parents
//            AudioSource[] found = col.GetComponents<AudioSource>();
//            if (found.Length == 0)
//            {
//                Debug.LogWarning("No audio source in the ARCH!!?");
//                found = col.GetComponentsInParent<AudioSource>();
//            }

//            if (found.Length == 0) {
//                Debug.LogWarning("No audio source in the parent as well!!! ");
//                return; 
//            }

//            Debug.LogWarning("Found Source(s), adding to bank!!");
//            bank = (found, 0);                             // start at first clip
//            clipBank[col] = bank;
//        }

//        AudioSource src = bank.sources[bank.next];

//        if (src != null && src.clip != null)
//        {
//            Debug.LogWarning("after all that BS, somehow, no source and no clip?!");
//            src.Stop();
//            src.time = 0f;
//            src.Play();
//        }

//        // Advance index for next time - wraps automatically
//        bank.next = (bank.next + 1) % bank.sources.Length;
//        clipBank[col] = bank;                              // write back the tuple
//    }

//    private Gradient GetGradientForPerson(int personId)
//    {
//        if (personGradients.TryGetValue(personId, out var g)) return g;

//        Color c1 = Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f);
//        Color c2 = Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f);

//        g = new Gradient();
//        g.SetKeys(
//            new[] { new GradientColorKey(c1, 0f), new GradientColorKey(c2, 1f) },
//            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
//        );

//        personGradients[personId] = g;
//        return g;
//    }

//    // -- Hand Raise Checks (disabled for demo) --
//    private bool IsLeftHandRaised(Vector3[] joints)
//    {
//        float wristY = joints[SkeletonHandler.JointType_LEFT_WRIST].y;
//        float elbowY = joints[SkeletonHandler.JointType_LEFT_ELBOW].y;
//        float shoulderY = joints[SkeletonHandler.JointType_LEFT_SHOULDER].y;
//        float fingerY = joints[SkeletonHandler.JointType_38_LEFT_HAND_MIDDLE_4].y;

//        return (wristY > shoulderY || wristY > elbowY) && (fingerY > wristY);
//    }

//    private bool IsRightHandRaised(Vector3[] joints)
//    {
//        float wristY = joints[SkeletonHandler.JointType_RIGHT_WRIST].y;
//        float elbowY = joints[SkeletonHandler.JointType_RIGHT_ELBOW].y;
//        float shoulderY = joints[SkeletonHandler.JointType_RIGHT_SHOULDER].y;
//        float fingerY = joints[SkeletonHandler.JointType_38_RIGHT_HAND_MIDDLE_4].y;

//        return (wristY > shoulderY || wristY > elbowY) && (fingerY > wristY);
//    }
//}
