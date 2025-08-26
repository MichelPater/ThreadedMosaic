using System;
using System.Windows.Controls;

namespace ThreadedMosaic.Mosaic
{
    /// <summary>
    /// WPF implementation of IProgressReporter that updates UI controls.
    /// Handles dispatcher marshalling for thread-safe UI updates.
    /// </summary>
    public class WpfProgressReporter : IProgressReporter
    {
        private readonly ProgressBar _progressBar;
        private readonly Label _statusLabel;

        public WpfProgressReporter(ProgressBar progressBar, Label statusLabel)
        {
            if (progressBar == null) throw new ArgumentNullException("progressBar");
            if (statusLabel == null) throw new ArgumentNullException("statusLabel");
            
            _progressBar = progressBar;
            _statusLabel = statusLabel;
        }

        public void SetMaximum(int maximum)
        {
            _progressBar.Dispatcher.BeginInvoke(new Action(() =>
            {
                _progressBar.Maximum = maximum;
                _progressBar.Value = 0;
            }));
        }

        public void IncrementProgress()
        {
            _progressBar.Dispatcher.BeginInvoke(new Action(() =>
            {
                _progressBar.Value++;
            }));
        }

        public void UpdateStatus(string status)
        {
            _statusLabel.Dispatcher.BeginInvoke(new Action(() =>
            {
                _statusLabel.Content = status;
            }));
        }

        public void ReportProgress(int current, int maximum, string status)
        {
            _progressBar.Dispatcher.BeginInvoke(new Action(() =>
            {
                _progressBar.Maximum = maximum;
                _progressBar.Value = current;
            }));

            _statusLabel.Dispatcher.BeginInvoke(new Action(() =>
            {
                _statusLabel.Content = status;
            }));
        }
    }
}