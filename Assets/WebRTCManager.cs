using Unity.WebRTC;

/// <summary>
/// A class that initializes the WebRTC library and starts it's update loop.
/// </summary>
/// <remarks>
/// Should be implemented deeper in game systems, but for testing this works.
/// </remarks>
public class WebRTCManager : Singleton<WebRTCManager>
{
    protected override void Awake_Impl()
    {
        WebRTC.Initialize(true, false);
        StartCoroutine(WebRTC.Update());
    }

    private void OnApplicationQuit()
    {
        WebRTC.Dispose();
    }

    public void RegisterPlayer(WebRTCPlayer player)
    {
        // Do nothing, just make sure compiler doesn't optimize away the call to Instance getter.
    }
}