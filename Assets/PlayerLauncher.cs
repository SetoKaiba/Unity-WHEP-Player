using UnityEngine;

/// <summary>
/// Simple testing script because I'm too lazy to make some scene interactions
/// </summary>
public class PlayerLauncher : MonoBehaviour
{
    [SerializeField] private WebRTCPlayer _player;

    private void Start()
    {
        if (_player == null) return;
        _player.Play();
    }
}