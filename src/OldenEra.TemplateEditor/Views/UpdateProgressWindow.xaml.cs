using System;
using System.Threading;
using System.Windows;

namespace OldenEra.TemplateEditor.Views
{
    public partial class UpdateProgressWindow : Window, IProgress<double>
    {
        private readonly CancellationTokenSource _cts = new();

        public UpdateProgressWindow()
        {
            InitializeComponent();
        }

        public CancellationToken CancellationToken => _cts.Token;

        public void Report(double value)
        {
            int pct = (int)Math.Round(Math.Clamp(value, 0.0, 1.0) * 100);
            if (Dispatcher.CheckAccess())
                ApplyProgress(pct);
            else
                Dispatcher.BeginInvoke(new Action(() => ApplyProgress(pct)));
        }

        private void ApplyProgress(int pct)
        {
            PbDownload.Value = pct;
            TxtPercent.Text = pct + "%";
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            BtnCancel.IsEnabled = false;
            TxtStatus.Text = "Cancelling...";
            _cts.Cancel();
        }

        protected override void OnClosed(EventArgs e)
        {
            _cts.Dispose();
            base.OnClosed(e);
        }
    }
}
