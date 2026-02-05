using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Zenox.NET.Core.Common.MVVM.ViewModelSupport;

namespace Zenox.Wpf.UI.Common
{
    public class UINotificationObject : NotificationObject
    {
        public new virtual UIAppKontext AppKontext
        {
            get
            {
                return base.AppKontext as UIAppKontext;
            }
            set
            {
                base.AppKontext = value;
            }
        }

        #region UI Dispatcher helpers

        /// <summary>
        /// Executes an action on the UI thread synchronously.
        /// Falls back to current Dispatcher if Application.Current is null.
        /// </summary>
        protected void ExecuteOnUI(Action action)
        {
            if (action == null) return;
            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            if (dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action);
        }

        /// <summary>
        /// Executes a function on the UI thread asynchronously and returns a Task.
        /// </summary>
        protected Task ExecuteOnUIAsync(Func<Task> func)
        {
            if (func == null) return Task.CompletedTask;
            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            if (dispatcher.CheckAccess())
                return func();
            return dispatcher.InvokeAsync(func).Task.Unwrap();
        }

        #endregion

        #region Notification suspension (bulk updates)

        private int _suspendNotifications;

        /// <summary>
        /// Returns true while notifications are suspended.
        /// Use using(SuspendNotifications()) { ... } to batch updates.
        /// </summary>
        protected bool NotificationsSuspended => _suspendNotifications > 0;

        protected IDisposable SuspendNotifications()
        {
            Interlocked.Increment(ref _suspendNotifications);
            return new NotificationSuspender(() => Interlocked.Decrement(ref _suspendNotifications));
        }

        private sealed class NotificationSuspender : IDisposable
        {
            private readonly Action _onDispose;
            private int _disposed;

            public NotificationSuspender(Action onDispose) => _onDispose = onDispose;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                    _onDispose?.Invoke();
            }
        }
        #endregion

        #region IsBusy helpers

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        /// <summary>
        /// Runs an async action while setting IsBusy true for the duration.
        /// Ensures IsBusy is cleared even if the action throws.
        /// </summary>
        protected async Task RunBusyAsync(Func<Task> action)
        {
            if (action == null) return;
            if (IsBusy) return; // optional: ignore nested calls
            try
            {
                IsBusy = true;
                await action().ConfigureAwait(false);
            }
            finally
            {
                // Ensure we update on UI thread
                ExecuteOnUI(() => IsBusy = false);
            }
        }

        #endregion

        #region INotifyDataErrorInfo (simple validation store)

        private readonly ConcurrentDictionary<string, List<string>> _errors = new();

        public bool HasErrors => _errors.Any(kv => kv.Value?.Count > 0);

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public IEnumerable GetErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return _errors.SelectMany(kv => kv.Value).ToList();
            if (_errors.TryGetValue(propertyName, out var list))
                return list.ToList();
            return null;
        }

        protected void AddError(string propertyName, string error)
        {
            if (string.IsNullOrEmpty(propertyName) || string.IsNullOrEmpty(error)) return;
            var list = _errors.GetOrAdd(propertyName, _ => new List<string>());
            lock (list)
            {
                if (!list.Contains(error))
                {
                    list.Add(error);
                }
            }
            ExecuteOnUI(() => ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName)));
            OnPropertyChanged(nameof(HasErrors));
        }

        protected void ClearErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return;
            if (_errors.TryRemove(propertyName, out _))
            {
                ExecuteOnUI(() => ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName)));
                OnPropertyChanged(nameof(HasErrors));
            }
        }

        #endregion

        #region Convenience SetProperty that notifies on UI thread

        /// <summary>
        /// Sets a field and raises PropertyChanged on the UI thread.
        /// Uses NotificationObject.SetProperty for equality/logging.
        /// </summary>
        protected bool SetPropertyUI<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            ExecuteOnUI(() => OnPropertyChanged(propertyName));
            return true;
        }

        #endregion

        #region IDisposable

        private bool _disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                // free managed resources here (unregister events, etc.)
            }
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}