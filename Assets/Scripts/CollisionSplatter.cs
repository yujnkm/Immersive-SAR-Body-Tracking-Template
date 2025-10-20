using UnityEngine;
using System.Collections.Generic;

public class CollisionSplatter : MonoBehaviour
{
    [Header("Prefabs & Assets")]
    public GameObject splashPrefab;
    public AudioClip collisionSound;

    [Header("Splash Settings")]
    public float splashFrequency = 0.5f;
    public float splashLifetime = 2.0f;

    private AudioSource audioSource;
    private Collider torusCol;

    class SplashData
    {
        public float nextStampTime;
    }
    Dictionary<Collider, SplashData> active = new();
    int activeCollisionCount = 0;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        torusCol = GetComponent<Collider>();

        if (audioSource == null) Debug.LogError("Missing AudioSource.");
        if (torusCol == null) Debug.LogError("Missing Collider.");
    }

    void OnTriggerEnter(Collider other) => TrySplash(other);
    void OnTriggerStay(Collider other) => TrySplash(other);

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("armExtension")) return;

        if (active.ContainsKey(other))
        {
            active.Remove(other);
            activeCollisionCount = Mathf.Max(0, activeCollisionCount - 1);
            if (activeCollisionCount == 0 && audioSource.isPlaying) audioSource.Stop();
        }
    }

    void TrySplash(Collider other)
    {
        if (!other.CompareTag("armExtension")) return;

        if (!active.TryGetValue(other, out var data))
        {
            data = new SplashData { nextStampTime = 0f };
            active.Add(other, data);
        }

        if (Time.time < data.nextStampTime) return;
        data.nextStampTime = Time.time + splashFrequency;

        Vector3 hitPos = torusCol.ClosestPoint(other.transform.position);
        Vector3 normal = (hitPos - torusCol.bounds.center).normalized;

        GameObject splash = Instantiate(splashPrefab, hitPos + normal * 0.01f,
                                         Quaternion.LookRotation(normal));

        var main = splash.GetComponent<ParticleSystem>().main;
        main.startColor = Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f);

        Destroy(splash, splashLifetime);

        if (++activeCollisionCount == 1 && collisionSound && audioSource)
        {
            audioSource.clip = collisionSound;
            audioSource.loop = true;
            audioSource.time = 0f;
            audioSource.Play();
        }
    }

    void Update()
    {
        var toRemove = new List<Collider>();
        foreach (var kvp in active)
            if (kvp.Key == null || !kvp.Key.gameObject.activeInHierarchy)
                toRemove.Add(kvp.Key);

        foreach (var col in toRemove)
        {
            active.Remove(col);
            activeCollisionCount = Mathf.Max(0, activeCollisionCount - 1);
        }
        if (activeCollisionCount == 0 && audioSource.isPlaying) audioSource.Stop();
    }
}
