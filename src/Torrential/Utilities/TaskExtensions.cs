namespace Torrential.Utilities
{
    internal static class TaskExtensions
    {
        public static bool InProgress(this Task? task)
        {
            return task != null
                && !task.IsCompleted
                && !task.IsFaulted
                && !task.IsCanceled
                && task.Status != TaskStatus.WaitingForActivation
                && task.Status != TaskStatus.WaitingToRun
                && task.Status != TaskStatus.WaitingForChildrenToComplete;
        }
    }
}
