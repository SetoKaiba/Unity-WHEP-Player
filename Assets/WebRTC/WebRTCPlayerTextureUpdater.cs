using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helper class allowing automated setting of the video texture on a RawImage.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class WebRTCPlayerTextureUpdater : MonoBehaviour
{
    private RawImage _rawImage;
    [SerializeField] private WebRTCPlayer _webRTCPlayer;

    private void Start()
    {
        _rawImage = GetComponent<RawImage>();
        if (_webRTCPlayer != null)
            _webRTCPlayer.OnVideoTextureChanged += TextureChanged;
    }

    private void OnDestroy()
    {
        if (_webRTCPlayer != null)
            _webRTCPlayer.OnVideoTextureChanged -= TextureChanged;
    }

    private void TextureChanged(Texture texture)
    {
        _rawImage.texture = texture;
    }
}