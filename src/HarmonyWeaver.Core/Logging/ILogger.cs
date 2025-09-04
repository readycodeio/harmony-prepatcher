namespace HarmonyWeaver.Core.Logging
{
    /// <summary>
    /// Simple logging interface for patch methods
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Log an informational message
        /// </summary>
        /// <param name="message">The message to log</param>
        void LogInfo(string message);

        /// <summary>
        /// Log a warning message
        /// </summary>
        /// <param name="message">The message to log</param>
        void LogWarning(string message);

        /// <summary>
        /// Log an error message
        /// </summary>
        /// <param name="message">The message to log</param>
        void LogError(string message);
    }
}