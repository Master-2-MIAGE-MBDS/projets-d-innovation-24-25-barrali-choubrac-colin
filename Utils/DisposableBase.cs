using System;
using System.Diagnostics;

namespace DeepBridgeWindowsApp.Utils
{
    /// <summary>
    /// Base class for implementing the disposable pattern consistently across the application.
    /// Provides standard implementation of IDisposable with safeguards against multiple disposals.
    /// </summary>
    public abstract class DisposableBase : IDisposable
    {
        // Flag to detect redundant disposal calls
        private bool _disposed = false;

        /// <summary>
        /// Gets a value indicating whether this instance has been disposed.
        /// </summary>
        protected bool IsDisposed => _disposed;

        /// <summary>
        /// Public implementation of Dispose pattern.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern.
        /// </summary>
        /// <param name="disposing">True if managed resources should be disposed; otherwise, false.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            try
            {
                if (disposing)
                {
                    // Dispose managed resources
                    DisposeManagedResources();
                }

                // Dispose unmanaged resources
                DisposeUnmanagedResources();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception during disposal of {GetType().Name}: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                _disposed = true;
            }
        }

        /// <summary>
        /// Dispose managed resources. Override this method to dispose managed resources.
        /// </summary>
        protected virtual void DisposeManagedResources() { }

        /// <summary>
        /// Dispose unmanaged resources. Override this method to dispose unmanaged resources.
        /// </summary>
        protected virtual void DisposeUnmanagedResources() { }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~DisposableBase()
        {
            Dispose(false);
        }

        /// <summary>
        /// Throws an ObjectDisposedException if this object has been disposed.
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}