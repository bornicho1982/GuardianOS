using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Dispatches actions to the main thread for Unity API calls from async code
/// </summary>
public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher instance;
    private static readonly Queue<Action> actionQueue = new Queue<Action>();
    private static readonly object lockObject = new object();

    public static MainThreadDispatcher Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("MainThreadDispatcher");
                instance = go.AddComponent<MainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        lock (lockObject)
        {
            while (actionQueue.Count > 0)
            {
                Action action = actionQueue.Dequeue();
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MainThreadDispatcher] Error: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Enqueue an action to be executed on the main thread
    /// </summary>
    public static void Enqueue(Action action)
    {
        if (action == null) return;

        lock (lockObject)
        {
            actionQueue.Enqueue(action);
        }

        // Ensure instance exists
        _ = Instance;
    }

    /// <summary>
    /// Execute action immediately if on main thread, enqueue otherwise
    /// </summary>
    public static void Execute(Action action)
    {
        if (action == null) return;

        if (IsMainThread())
        {
            action();
        }
        else
        {
            Enqueue(action);
        }
    }

    private static bool IsMainThread()
    {
        return System.Threading.Thread.CurrentThread.ManagedThreadId == 1;
    }
}
