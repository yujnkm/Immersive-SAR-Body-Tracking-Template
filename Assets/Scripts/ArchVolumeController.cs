using UnityEngine;
using UnityEngine.Audio;

public class ArchVolumeController : MonoBehaviour
{
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string param = "ArchVolume";

    public void SetLinearVolume(float linear)
    {
        float dB = linear > 0f ? Mathf.Log10(linear) * 20f : -80f;
        mixer.SetFloat(param, dB);
    }
}
