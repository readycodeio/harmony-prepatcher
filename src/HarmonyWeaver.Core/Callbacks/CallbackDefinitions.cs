using System;

namespace HarmonyWeaver.Core.Callbacks
{
    /// <summary>
    /// Delegate for prefix callbacks that can skip the original method
    /// </summary>
    /// <param name="args">Arguments passed to the original method</param>
    /// <param name="result">Output parameter for custom result (when skipping)</param>
    /// <returns>True to continue with original method, false to skip and use result</returns>
    public delegate bool PrefixCallback(object[] args, out object result);

    /// <summary>
    /// Delegate for postfix callbacks that can modify the result
    /// </summary>
    /// <param name="args">Arguments passed to the original method</param>
    /// <param name="result">Result from the original method (can be modified)</param>
    public delegate void PostfixCallback(object[] args, ref object result);

    /// <summary>
    /// Delegate for finalizer callbacks that handle exceptions
    /// </summary>
    /// <param name="args">Arguments passed to the original method</param>
    /// <param name="exception">Exception thrown by the original method (null if no exception)</param>
    public delegate void FinalizerCallback(object[] args, Exception exception);

    /// <summary>
    /// Container for all callback types for a single method
    /// </summary>
    public class MethodCallbacks
    {
        /// <summary>
        /// Prefix callback (executed before original method)
        /// </summary>
        public PrefixCallback? Prefix { get; set; }

        /// <summary>
        /// Postfix callback (executed after original method)
        /// </summary>
        public PostfixCallback? Postfix { get; set; }

        /// <summary>
        /// Finalizer callback (executed in exception handler)
        /// </summary>
        public FinalizerCallback? Finalizer { get; set; }

        /// <summary>
        /// Check if any callbacks are set
        /// </summary>
        public bool HasAnyCallbacks => Prefix != null || Postfix != null || Finalizer != null;

        /// <summary>
        /// Clear all callbacks
        /// </summary>
        public void Clear()
        {
            Prefix = null;
            Postfix = null;
            Finalizer = null;
        }
    }
}