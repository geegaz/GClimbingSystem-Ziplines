
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ImpulseFeedback : UdonSharpBehaviour
{
    [Header("Visual")]
    [SerializeField] private ParticleSystem _particleSystem;

    [Space]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _animatorTrigger = "Start";

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip[] _audioClips;
    [SerializeField] private Vector2 _audioVolumeMinMax;
    [SerializeField] private Vector2 _audioPitchMinMax;
    

    #region Feedback Methods
    public void StartFeedback() {
        if(_animator) _animator.SetTrigger(_animatorTrigger);
        if (_particleSystem) _particleSystem.Play();
        if (_audioSource) {
            AudioClip clip = _audioSource.clip;
            if (_audioClips.Length > 0) {
                int random_clip = Random.Range(0, _audioClips.Length);
                clip = _audioClips[random_clip];
            }
            if (clip) {
                _audioSource.pitch = Random.Range(_audioPitchMinMax.x, _audioPitchMinMax.y);
                _audioSource.PlayOneShot(clip, Random.Range(_audioVolumeMinMax.x, _audioVolumeMinMax.y));
            }
        }
    }
    #endregion
}
