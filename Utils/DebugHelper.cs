using System;
using System.Diagnostics;

namespace DeepBridgeWindowsApp.Utils
{
    /// <summary>
    /// Helper class for debugging and performance measurement.
    /// </summary>
    public static class DebugHelper
    {
        /// <summary>
        /// Executes an action and measures the time it takes.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="operationName">The name of the operation being measured.</param>
        /// <returns>The elapsed time in milliseconds.</returns>
        public static long MeasureExecutionTime(Action action, string operationName)
        {
            Debug.WriteLine($"Starting operation: {operationName}");
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                action();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Debug.WriteLine($"Exception during {operationName}: {ex.Message}");
                throw;
            }

            stopwatch.Stop();
            Debug.WriteLine($"Operation {operationName} completed in {stopwatch.ElapsedMilliseconds}ms");
            return stopwatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Measures memory usage before and after an action.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="operationName">The name of the operation being measured.</param>
        /// <returns>The memory change in bytes (can be negative if memory was freed).</returns>
        public static long MeasureMemoryUsage(Action action, string operationName)
        {
            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Measure memory before
            long memoryBefore = GC.GetTotalMemory(true);
            Debug.WriteLine($"Memory before {operationName}: {memoryBefore / 1024} KB");

            try
            {
                action();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception during {operationName}: {ex.Message}");
                throw;
            }

            // Force garbage collection again
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Measure memory after
            long memoryAfter = GC.GetTotalMemory(true);
            Debug.WriteLine($"Memory after {operationName}: {memoryAfter / 1024} KB");
            Debug.WriteLine($"Memory change: {(memoryAfter - memoryBefore) / 1024} KB");

            return memoryAfter - memoryBefore;
        }

        /// <summary>
        /// Logs the object state with its property values.
        /// </summary>
        /// <param name="obj">The object to inspect.</param>
        /// <param name="description">A description of the object.</param>
        public static void LogObjectState(object obj, string description)
        {
            if (obj == null)
            {
                Debug.WriteLine($"{description}: Object is null");
                return;
            }

            Debug.WriteLine($"{description}: {obj.GetType().Name}");
            var properties = obj.GetType().GetProperties();

            foreach (var property in properties)
            {
                try
                {
                    var value = property.GetValue(obj);
                    Debug.WriteLine($"  {property.Name} = {value ?? "null"}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"  {property.Name} = [Error: {ex.Message}]");
                }
            }
        }
    }
}