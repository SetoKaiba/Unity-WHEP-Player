using UnityEngine;

/// <summary>
/// Simple behaviour to make creation of a singleton easier.
/// </summary>
/// <typeparam name="T">Singleton class</typeparam>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    protected static T _instance;

    public static bool hasInstance => _instance != null;

    public static T Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }
            else
            {
                var type = typeof(T);

                var go = GameObject.Find(type.ToString());
                if (go == null) go = GameObject.Find(type + "(Clone)");

                if (go != null)
                {
                    _instance = go.GetComponent<T>();
                    if (_instance == null)
                    {
                        Destroy(go);
                        go = null;
                    }
                }

                if (go != null) return null;
                go = new GameObject(type.ToString());
                _instance = go.AddComponent<T>();
                DontDestroyOnLoad(go);

                return _instance;
            }
        }
    }


    public static void ForceDestroy()
    {
        if (!hasInstance) return;
        Destroy(_instance.gameObject);
        _instance = null;
    }

    private void Awake()
    {
        if (hasInstance)
            Destroy(this);
        Awake_Impl();
    }

    private void OnDestroy()
    {
        if (hasInstance && _instance == this)
            _instance = null;
        OnDestroy_Impl();
    }

    protected virtual void Awake_Impl()
    {
    }

    protected virtual void OnDestroy_Impl()
    {
    }
}