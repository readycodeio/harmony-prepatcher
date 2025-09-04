namespace HarmonyWeaver.Core.Logging
{
    /// <summary>
    /// Static logger provider for patch methods to access logging
    /// </summary>
    public static class LoggerProvider
    {
        private static ILogger? _logger;

        /// <summary>
        /// Set the logger instance to be used by patches
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public static void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Get the current logger instance
        /// </summary>
        public static ILogger Logger => _logger ?? new NullLogger();

        /// <summary>
        /// Clear the current logger
        /// </summary>
        public static void ClearLogger()
        {
            _logger = null;
        }
    }

    /// <summary>
    /// Null logger that does nothing (used as default)
    /// </summary>
    internal class NullLogger : ILogger
    {
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message) { }
    }
}