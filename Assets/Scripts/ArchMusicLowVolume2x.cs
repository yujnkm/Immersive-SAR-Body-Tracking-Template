using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

[DisallowMultipleComponent]
public class ArchMusicLowVolume2x : MonoBehaviour
{
    [Tooltip("Exactly seven note clips, ordered: Arch, Arch (1), Arch (2), ..., Arch (6)")]
    public AudioClip[] noteClips = new AudioClip[7];

    private static readonly Regex ArchNamePattern = new Regex(@"^Arch\s*(?:\((\d)\))?$", RegexOptions.Compiled);

    void Awake()
    {
        if (noteClips == null || noteClips.Length != 7)
        {
            Debug.LogError($"{name}: Expected 7 AudioClips in 'noteClips', found {noteClips?.Length ?? 0}.");
            return;
        }

        foreach (Transform child in transform)
        {
            int index = GetIndexFromName(child.name);
            if (index == -1) continue;

            var audioSource = child.GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = child.gameObject.AddComponent<AudioSource>();

            audioSource.clip = noteClips[index];
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
            audioSource.volume = 0.3f;
        }

        var firstChild = GetChildByIndex(0);
        var fifthChild = GetChildByIndex(4);

        var lastNoteclip = noteClips[noteClips.Length - 1];
        var secondToLastNoteclip = noteClips[noteClips.Length - 2];

        var firstChildAudioSource = firstChild.gameObject.AddComponent<AudioSource>();
        firstChildAudioSource.clip = secondToLastNoteclip;
        firstChildAudioSource.playOnAwake = false;
        firstChildAudioSource.loop = false;
        firstChildAudioSource.spatialBlend = 1f;

        var fifthChildAudioSource = fifthChild.gameObject.AddComponent<AudioSource>();
        fifthChildAudioSource.clip = lastNoteclip;
        fifthChildAudioSource.playOnAwake = false;
        fifthChildAudioSource.loop = false;
        fifthChildAudioSource.spatialBlend = 1f;

    }

    private static int GetIndexFromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return -1;

        var match = ArchNamePattern.Match(name);
        if (!match.Success) return -1;

        if (!match.Groups[1].Success) return 0; // matches "Arch" exactly
        if (int.TryParse(match.Groups[1].Value, out int idx) && idx >= 1 && idx <= 6)
            return idx;

        return -1;
    }

    private Transform GetChildByIndex(int index)
    {
        // quick bounds check
        if (index < 0 || index > 6)
        {
            Debug.LogError($"{name}: Requested child index {index} is out of range (0–6).");
            return null;
        }

        // scan each child and return the one whose name parses to this index
        foreach (Transform child in transform)
        {
            if (GetIndexFromName(child.name) == index)
                return child;
        }

        Debug.LogWarning($"{name}: No child matching index {index}.");
        return null;
    }

}


//using System.Collections.Generic;
//using System.Text.RegularExpressions;
//using UnityEngine;
//using UnityEngine.Audio;

//[DisallowMultipleComponent]
//public class ArchMusicLowVolume2x : MonoBehaviour
//{
//    [Tooltip("Exactly seven note clips, ordered: Arch, Arch (1), Arch (2), ..., Arch (6)")]
//    public AudioClip[] noteClips = new AudioClip[7];

//    [Header("Routing")]
//    [SerializeField] private AudioMixerGroup archGroup;

//    private static readonly Regex ArchNamePattern = new Regex(@"^Arch\s*(?:\((\d)\))?$", RegexOptions.Compiled);

//    void Awake()
//    {
//        if (noteClips == null || noteClips.Length != 7)
//        {
//            Debug.LogError($"{name}: Expected 7 AudioClips in 'noteClips', found {noteClips?.Length ?? 0}.");
//            return;
//        }

//        foreach (Transform child in transform)
//        {
//            int index = GetIndexFromName(child.name);
//            if (index == -1) continue;

//            var audioSource = child.GetComponent<AudioSource>();
//            if (audioSource == null)
//                audioSource = child.gameObject.AddComponent<AudioSource>();

//            audioSource.clip = noteClips[index];
//            audioSource.outputAudioMixerGroup = archGroup;
//            audioSource.playOnAwake = false;
//            audioSource.loop = false;
//            audioSource.spatialBlend = 1f;
//            audioSource.volume = 1f;
//        }

//        var firstChild = GetChildByIndex(0);
//        var fifthChild = GetChildByIndex(4);

//        var lastNoteclip = noteClips[noteClips.Length - 1];
//        var secondToLastNoteclip = noteClips[noteClips.Length - 2];

//        var firstChildAudioSource = firstChild.gameObject.AddComponent<AudioSource>();
//        firstChildAudioSource.clip = secondToLastNoteclip;
//        firstChildAudioSource.playOnAwake = false;
//        firstChildAudioSource.loop = false;
//        firstChildAudioSource.spatialBlend = 1f;

//        var fifthChildAudioSource = fifthChild.gameObject.AddComponent<AudioSource>();
//        fifthChildAudioSource.clip = lastNoteclip;
//        fifthChildAudioSource.playOnAwake = false;
//        fifthChildAudioSource.loop = false;
//        fifthChildAudioSource.spatialBlend = 1f;

//    }

//    private static int GetIndexFromName(string name)
//    {
//        if (string.IsNullOrWhiteSpace(name)) return -1;

//        var match = ArchNamePattern.Match(name);
//        if (!match.Success) return -1;

//        if (!match.Groups[1].Success) return 0; // matches "Arch" exactly
//        if (int.TryParse(match.Groups[1].Value, out int idx) && idx >= 1 && idx <= 6)
//            return idx;

//        return -1;
//    }

//    private Transform GetChildByIndex(int index)
//    {
//        // quick bounds check
//        if (index < 0 || index > 6)
//        {
//            Debug.LogError($"{name}: Requested child index {index} is out of range (0–6).");
//            return null;
//        }

//        // scan each child and return the one whose name parses to this index
//        foreach (Transform child in transform)
//        {
//            if (GetIndexFromName(child.name) == index)
//                return child;
//        }

//        Debug.LogWarning($"{name}: No child matching index {index}.");
//        return null;
//    }

//}