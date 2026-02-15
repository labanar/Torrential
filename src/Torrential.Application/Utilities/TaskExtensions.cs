namespace Torrential.Application.Utilities;

internal static class TaskExtensions
{
    public static bool InProgress(this Task? task)
    {
        return !task.IsCompleted();
    }


    public static bool IsCompleted(this Task? task)
    {
        if (task == null)
            return true;

        if (task.IsCompleted)
            return true;

        if (task.IsFaulted)
            return true;

        if (task.IsCanceled)
            return true;

        return false;
    }
}
