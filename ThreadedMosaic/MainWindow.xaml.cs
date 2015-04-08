using System;
using System.IO;
using System.Linq;
using System.Threading;
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
            InitFields();
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
            //Check Seed and Master Folders
            if (CheckValidPath(SeedFolderTextbox.Text)
                && CheckValidPath(MasterImageTextBox.Text))
            {
                //Check if there are files in the SeedFolder
                var files = Directory.EnumerateFiles(SeedFolderTextbox.Text, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(s => s.EndsWith(".png") || s.EndsWith(".jpg"));
                if (files.Any())
                {
                    //start analysis
                    Mosaic mosaic = new Mosaic(files.ToList());
                    var MosaicThread = new Thread(mosaic.CreateTiles);
                    MosaicThread.Start();
                    
                }
            }
            else
            {
                //Display error
                //either the folders are incorrect or there are no files
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

        private void InitFields()
        {
            SeedFolderTextbox.Text = @"E:\Downloads\Internet Destroying Wallpaper Dump\30";
            MasterImageTextBox.Text = @"E:\Downloads\Internet Destroying Wallpaper Dump\033_PMmglpV.jpg";
            OutputImageTextbox.Text = @"C:\Users\Michel\Desktop\Output folder\" + DateTime.Now + ".jpg";
        }
    }
}
