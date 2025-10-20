using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;

/* ----------  DTOs (identical to avatar) ---------- */
[System.Serializable]
struct AvatarPose
{
    public uint id;
    public Vector3 position;
    public Vector3 rotation;   // only .y used for ZED
}
[System.Serializable]
class AvatarPayload
{
    public string clientId;
    public string rawDateTime;
    public long timestamp;
    public AvatarPose[] avatars;
}

/* ----------  ZEDDataSender ---------- */
public class ZedDataSender : MonoBehaviour
{

    [Tooltip("ZEDBodyTrackingManager is on this GameObject")]
    public GameObject bodyTrackingViewer;

    [Tooltip("Same Apps-Script URL used by Meta avatars (…/exec)")]
    public string apiUrl = "https://script.google.com/macros/s/insert_macro_id/exec";

    [Tooltip("Seconds between POSTs")]
    public float interval = 5f;

    private ZEDBodyTrackingManager _btm;

    void Start()
    {
        _btm = bodyTrackingViewer?.GetComponent<ZEDBodyTrackingManager>();
        if (!_btm) { Debug.LogError("ZEDBodyTrackingManager missing"); enabled = false; }
        else StartCoroutine(SenderLoop());
    }

    IEnumerator SenderLoop()
    {
        var wait = new WaitForSeconds(interval);

        while (true)
        {
            yield return wait;
            var acl = _btm.avatarControlList;
            if (acl == null || acl.Count == 0) continue;

            /* ---- build avatar-style list ---- */
            List<AvatarPose> poses = new();
            foreach (var kv in acl)
            {
                int pid = kv.Key;
                Vector3[] j = kv.Value.currentJoints;
                Vector3 head = j[SkeletonHandler.JointType_34_Head];

                poses.Add(new AvatarPose
                {
                    id = (uint)pid,          // personId looks like an avatar id
                    position = head,
                    rotation = new Vector3(0, 0, 0)  // you can compute Yaw if you need
                });
            }

            SendPayload(poses.ToArray());
        }
    }

    void SendPayload(AvatarPose[] poses)
    {
        var nowUtc = System.DateTime.UtcNow;
        var payload = new AvatarPayload
        {
            clientId = SystemInfo.deviceUniqueIdentifier,
            rawDateTime = nowUtc.ToString("o"),
            timestamp = new System.DateTimeOffset(nowUtc).ToUnixTimeMilliseconds(),
            avatars = poses
        };

        string json = JsonUtility.ToJson(payload);
        StartCoroutine(Post(json));
    }

    IEnumerator Post(string json)
    {
        using var req = new UnityWebRequest(apiUrl, UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer()
        };
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogError("[ZEDDataSender] " + req.error);
        else
            Debug.Log("[ZEDDataSender] Sent " + json.Length + " bytes");
    }
}
