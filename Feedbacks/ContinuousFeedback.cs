
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ContinuousFeedback : UdonSharpBehaviour
{
    public bool active = false;
    
    [Header("Visual")]
    [SerializeField] private ParticleSystem _particleSystem;

    [Space]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _animatorStartTrigger = "Start";
    [SerializeField] private string _animatorStopTrigger = "Stop";
    [SerializeField] private string _animatorActiveBool = "Active";

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;

    private bool _previouslyActive = false;


    private void Update() {
        if (active && !_previouslyActive) {
            StartFeedback();
        }
        else if (!active && _previouslyActive) {
            StopFeedback();
        }
    }

    #region Feedback Methods
    public void StartFeedback() {
        active = true;
        _previouslyActive = true;

        if(_particleSystem) _particleSystem.Play();
        if(_animator) {
            _animator.SetTrigger(_animatorStartTrigger);
            _animator.SetBool(_animatorActiveBool, true);
        }
        if(_audioSource) _audioSource.Play();
    }

    public void StopFeedback() {
        active = false;
        _previouslyActive = false;
        
        if(_particleSystem) _particleSystem.Stop();
        if(_animator) {
            _animator.SetTrigger(_animatorStopTrigger);
            _animator.SetBool(_animatorActiveBool, false);
        }
        if (_audioSource) _audioSource.Stop();
    }
    #endregion
}
