using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Helper class for wrapping a coroutine in a async task.
/// </summary>
public static class TaskWrapperHelper
{
    /// <summary>
    /// Converts a CustomYieldInstruction to a standard C# Task.
    /// </summary>
    /// <param name="iterator">The instruction</param>
    /// <returns>C# Task</returns>
    public static async Task<T> YieldInstructionToTask<T>(this T iterator) where T : CustomYieldInstruction
    {
        if (iterator == null) return null;
        while (iterator.keepWaiting)
            await Task.Yield();
        return iterator;
    }

    
    /// <summary>
    /// Converts a Unity AsyncOperation (a part of Coroutine) to a standard C# Task.
    /// </summary>
    /// <param name="operation">The operation</param>
    /// <returns>C# Task</returns>
    public static async Task<T> AsyncOperationToTask<T>(this T operation) where T : AsyncOperation
    {
        if (operation == null) return null;
        while (!operation.isDone)
            await Task.Yield();
        return operation;
    }
}