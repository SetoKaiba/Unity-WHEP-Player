/**
   MIT License

    Copyright (c) 2018, Cloudflare, Inc. All rights reserved.

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
 */

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Networking;

public delegate void StreamReadyDelegate(MediaStream stream);
public delegate void StreamErrorDelegate(string error);
public delegate void StreamLogDelegate(string log);

/// <summary>
/// Simple WebRTC client for unity, implementing the WHEP specification.
/// </summary>
/// <remarks>
/// This class is based on Cloudflare's implementation of the WHEP specification, see: https://github.com/cloudflare/templates/blob/main/stream/webrtc/src/WHEPClient.ts
/// same as the original implementation, this class only implements WHEP SDP negotiation, and does not implement the WHEP resource management.
/// </remarks>
public class WHEPClient : IDisposable
{
    private readonly RTCPeerConnection _peerConnection;
    private readonly MediaStream _stream;

    /// <summary>
    /// Stream is ready and is connected to the server.
    /// </summary>
    public event StreamReadyDelegate OnStreamReady;

    /// <summary>
    /// Stream has errored during negotiation.
    /// </summary>
    public event StreamErrorDelegate OnError;

    /// <summary>
    /// Logging event.
    /// </summary>
    public event StreamLogDelegate OnLog;

    public WHEPClient(string endpoint)
    {
        _stream = new MediaStream();
        /*
         * Create a new WebRTC connection, using public STUN servers with ICE,
         * allowing the client to discover its own IP address.
         */
        RTCConfiguration config = default;
        // TODO: Re-enable ICE for proper NAT traversal (in my case unity cannot handle it and times out - for some reason)
        // TODO: Also make configurable?
        // config.iceServers = new[] {new RTCIceServer {urls = new[] {"stun:stun.cloudflare.com:3478"}}};
        config.bundlePolicy = RTCBundlePolicy.BundlePolicyMaxBundle;
        _peerConnection = new RTCPeerConnection(ref config);

        _peerConnection.AddTransceiver(TrackKind.Video, new RTCRtpTransceiverInit
        {
            direction = RTCRtpTransceiverDirection.RecvOnly
        });
        _peerConnection.AddTransceiver(TrackKind.Audio, new RTCRtpTransceiverInit
        {
            direction = RTCRtpTransceiverDirection.RecvOnly
        });

        _peerConnection.OnTrack = e =>
        {
            var track = e.Track;
            var currentTracks = _stream.GetTracks().ToList();
            var streamAlreadyHasVideoTrack = currentTracks.Any(t => t.Kind == TrackKind.Video);
            var streamAlreadyHasAudioTrack = currentTracks.Any(t => t.Kind == TrackKind.Audio);

            switch (track.Kind)
            {
                case TrackKind.Audio:
                    if (!streamAlreadyHasAudioTrack) _stream.AddTrack(track);
                    break;
                case TrackKind.Video:
                    if (!streamAlreadyHasVideoTrack) _stream.AddTrack(track);
                    break;
                default:
                    OnLog?.Invoke($"Received unknown track kind: {track.Kind}");
                    break;
            }
        };

        _peerConnection.OnConnectionStateChange = state =>
        {
            OnLog?.Invoke($"Connection state changed to: {state}");
            if (state != RTCPeerConnectionState.Connected) return;
            OnLog?.Invoke("Connection established, starting the show.");
            OnStreamReady?.Invoke(_stream);
        };

        _peerConnection.OnNegotiationNeeded = () =>
        {
            // Run asynchronously, to avoid blocking the main thread.
            _ = NegotiateConnectionWithClientOffset(endpoint);
        };
    }

    public void Dispose()
    {
        _peerConnection?.Dispose();
        _stream?.Dispose();
    }

    // TODO: Move following code to separate class since it is potentially shared with WHIPClient too
    private async Task<string> NegotiateConnectionWithClientOffset(string endpoint)
    {
        try
        {
            var offer = await _peerConnection.CreateOffer().YieldInstructionToTask();
            var desc = offer.Desc;
            OnLog?.Invoke($"SDP OFFER:\n{desc.sdp}");

            await _peerConnection.SetLocalDescription(ref desc).YieldInstructionToTask();

            // Wait for ICE gathering to complete
            var ofr = await WaitToCompleteICEGathering();
            if (ofr == null)
            {
                OnError?.Invoke("ICE gathering timed out.");
                return null;
            }

            /*
             * As long as the connection is open, attempt to...
             */
            while (_peerConnection.ConnectionState != RTCPeerConnectionState.Closed)
            {
                /*
                 * This response contains the server's SDP offer.
                 * This specifies how the client should communicate,
                 * and what kind of media client and server have negotiated to exchange.
                 */
                var request = new UnityWebRequest(endpoint, "POST");
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(ofr.Value.sdp));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/sdp");
                await request.SendWebRequest().AsyncOperationToTask();
                switch (request.responseCode)
                {
                    case 201:
                    {
                        var answerSdp = request.downloadHandler.text;
                        OnLog?.Invoke($"SDP ANSWER:\n{answerSdp}");
                        var answerDesc = new RTCSessionDescription { type = RTCSdpType.Answer, sdp = answerSdp };
                        await _peerConnection.SetRemoteDescription(ref answerDesc).YieldInstructionToTask();
                        return request.GetResponseHeader("Location");
                    }
                    case 405:
                        OnError?.Invoke("Reserved for future use, implementation needed.");
                        break;
                    default:
                        OnError?.Invoke(
                            $"Server returned error: {request.responseCode} {request.downloadHandler.text}");
                        break;
                }
                // TODO: Handle 409, read the Retry-After header and retry after the specified time instead of hardcoding + exponential backoff
                // Also add option to disable retry or limit number of retries

                await Task.Delay(5000);
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Error negotiating connection: {ex}");
        }

        return null;
    }

    /// <summary>
    /// Receives an RTCPeerConnection and waits until  the connection is initialized or a timeout passes.
    /// </summary>
    private async Task<RTCSessionDescription?> WaitToCompleteICEGathering()
    {
        var tcs = new TaskCompletionSource<RTCSessionDescription>();
        _peerConnection.OnIceGatheringStateChange = state =>
        {
            Debug.Log("ICE gathering state changed to: " + state);
            if (state != RTCIceGatheringState.Complete) return;
            _peerConnection.OnIceGatheringStateChange = null;
            tcs.TrySetResult(_peerConnection.LocalDescription);
        };

        /* Wait at most 1 second for ICE gathering. */
        if (await Task.WhenAny(tcs.Task, Task.Delay(10000)) == tcs.Task)
        {
            await tcs.Task;
            return tcs.Task.Result;
        }
        else
        {
            return null;
        }
    }
}