using System.Windows;

namespace OldenEra.TemplateEditor.Views
{
    public partial class PresetNameDialog : Window
    {
        public PresetNameDialog()
        {
            InitializeComponent();
            Loaded += (_, _) => TxtName.Focus();
        }

        public string PresetName => TxtName.Text;

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
