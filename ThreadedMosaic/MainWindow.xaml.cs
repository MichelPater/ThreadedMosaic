using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace ThreadedMosaic
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //This is a comment
        }

        private void SeedFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            DialogResult result = dialog.ShowDialog();
            SeedFolderTextbox.Text = dialog.SelectedPath;

            SeedFolderTextbox.CaretIndex = dialog.SelectedPath.Length;
        }
    }
}
