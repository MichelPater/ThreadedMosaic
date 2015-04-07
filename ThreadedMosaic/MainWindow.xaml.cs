using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace ThreadedMosaic
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SeedFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            DialogResult result = dialog.ShowDialog();
            SeedFolderTextbox.Text = dialog.SelectedPath;
        }

        private void MasterImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            DialogResult result = dialog.ShowDialog();
            MasterImageTextBox.Text = dialog.SelectedPath;
        }

        private void OutputImageButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();

            dlg.DefaultExt = ".jpg"; // Default file extension
            dlg.Filter = "Image files (.jpg)|*.jpg"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                // Save document
                OutputImageTextbox.Text = dlg.FileName;
            }
        }

        private void ConvertButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckValidPath(SeedFolderTextbox.Text)
                && CheckValidPath(MasterImageTextBox.Text)
                && CheckValidPath(OutputImageTextbox.Text))
            {
                //start analysis
                
            }
            else
            {
                //Display error
            }
        }

        private Boolean CheckValidPath(String pathname)
        {
            try
            {
                Path.GetFullPath(pathname);
                return true;
            }
            catch (Exception)
            {

                return false;
            }
        }
    }
}
