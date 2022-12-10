using System;
using System.Collections.Generic;
using System.Linq;
using Unity.WebRTC;
using UnityEngine;

public delegate void VideoTextureChangedDelegate(Texture texture);

public class WebRTCPlayer : MonoBehaviour
{
    public enum PlayerState
    {
        /// <summary>
        /// The player is not initialized or has been stopped.
        /// </summary>
        None = 0,

        /// <summary>
        /// The player is connecting to the signaling server.
        /// </summary>
        Connecting,

        /// <summary>
        /// The player is connected and is playing the stream.
        /// </summary>
        Playing,

        /// <summary>
        /// The player has errored during connection.
        /// </summary>
        Error,
    }

    [Serializable]
    public struct MaterialPropertyEntry
    {
        public Material material;
        public string property;
    }

    private WHEPClient _client;

    /// <summary>
    /// URL to a WHEP endpoint.
    /// </summary>
    [SerializeField] public string Url;

    /// <summary>
    /// List of materials to apply to the video stream texture on automatically.
    /// </summary>
    [SerializeField] public List<MaterialPropertyEntry> Materials = new List<MaterialPropertyEntry>();

    /// <summary>
    /// List of audio sources to apply the audio stream automatically.
    /// </summary>
    [SerializeField] public List<AudioSource> AudioSources = new List<AudioSource>();

    public PlayerState State { get; private set; } = PlayerState.None;

    /// <summary>
    /// Current video texture of the player, if any.
    /// </summary>
    /// <remarks>
    /// The way unity code is written, this texture can change at any time.
    /// </remarks>
    public Texture CurrentTexture { get; private set; }

    public event VideoTextureChangedDelegate OnVideoTextureChanged;


    private void Awake()
    {
        // Doesn't really do anything, just makes sure the WebRTC manager is initialized and so Unity.WebRTC package is started
        WebRTCManager.Instance.RegisterPlayer(this);
    }

    /// <summary>
    /// Starts the stream
    /// </summary>
    public void Play()
    {
        if (State != PlayerState.None)
            Stop();

        State = PlayerState.Connecting;

        _client = new WHEPClient(Url);
        _client.OnStreamReady += (stream) =>
        {
            State = PlayerState.Playing;
            var videoTrack = stream.GetVideoTracks().FirstOrDefault();
            if (videoTrack != null)
            {
                videoTrack.OnVideoReceived += SetTexture;
                SetTexture(videoTrack.Texture);
            }

            var audioTrack = stream.GetAudioTracks().FirstOrDefault();
            if (audioTrack != null)
            {
                SetAudio(audioTrack);
            }
        };

        _client.OnError += (error) =>
        {
            State = PlayerState.Error;
            Debug.LogError(error);
        };

        _client.OnLog += Debug.Log;
    }

    /// <summary>
    ///  Stops the stream
    /// </summary>
    public void Stop()
    {
        SetTexture(null);
        _client?.Dispose();
        _client = null;
        State = PlayerState.None;
    }

    // Set the texture and update given texture to all bound materials
    private void SetTexture(Texture texture)
    {
        CurrentTexture = texture;
        OnVideoTextureChanged?.Invoke(texture);
        foreach (var material in Materials)
        {
            if (!material.material || string.IsNullOrEmpty(material.property)) continue;
            material.material.SetTexture(material.property, texture);
        }
    }

    // Set the audio track to all bound audio sources
    private void SetAudio(AudioStreamTrack track)
    {
        foreach (var source in AudioSources)
        {
            source.SetTrack(track);
            source.Play();
        }
    }
}