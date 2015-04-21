using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using ThreadedMosaic.Mosaic;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ThreadedMosaic
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
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
            dialog.ShowDialog();
            SeedFolderTextbox.Text = dialog.SelectedPath;
        }

        private void MasterImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();

            dialog.DefaultExt = ".jpg"; // Default file extension
            dialog.Filter = "Image files (.jpg)|*.jpg"; // Filter files by extension

            // Open document
            var result = dialog.ShowDialog();

            //Process the result and fill textbox
            if (result == true)
            {
                MasterImageTextBox.Text = dialog.FileName;
            }
        }

        private void OutputImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog();

            dialog.DefaultExt = ".jpg"; // Default file extension
            dialog.Filter = "Image files (.jpg)|*.jpg"; // Filter files by extension

            // Show save file dialog box
            var result = dialog.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                OutputImageTextbox.Text = dialog.FileName;
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
                if (files.Any() && !String.IsNullOrEmpty(MasterImageTextBox.Text))
                {
                    //Check which option is selected
                    if ((bool) MosaicColorRadioButton.IsChecked)
                    {
                        var colorMosaic = new ColorMosaic(MasterImageTextBox.Text, OutputImageTextbox.Text);
                        colorMosaic.SetProgressBar(ProgressBar, ProgressText);
                        colorMosaic.SetPixelSize(int.Parse(PixelWidth.Text), int.Parse(PixelHeight.Text));
                        var ColorMosaicThread = new Thread(colorMosaic.CreateColorMosaic);
                        ColorMosaicThread.Start();
                    }
                    else if ((bool) MosaicHueRadioButton.IsChecked)
                    {
                        var hueMosaic = new HueMosaic(files.ToList(), MasterImageTextBox.Text, OutputImageTextbox.Text);
                        hueMosaic.SetProgressBar(ProgressBar, ProgressText);
                        hueMosaic.SetPixelSize(int.Parse(PixelWidth.Text), int.Parse(PixelHeight.Text));
                        var HueMosaicThread = new Thread(hueMosaic.CreateColorMosaic);
                        HueMosaicThread.Start();
                    }
                    else if ((bool) MosaicPhotoRadioButton.IsChecked)
                    {
                        var photoMosaic = new PhotoMosaic(files.ToList(), MasterImageTextBox.Text, OutputImageTextbox.Text);
                        photoMosaic.SetProgressBar(ProgressBar, ProgressText);
                        photoMosaic.SetPixelSize(int.Parse(PixelWidth.Text), int.Parse(PixelHeight.Text));
                        var photoMosaicThread = new Thread(photoMosaic.CreatePhotoMosaic);
                        photoMosaicThread.Start();
                    }
                }
            }
            else
            {
                //Display error because something went wrong
                MessageBox.Show("Mosaic could not be started, are all filepaths correct?",
                    "Something went wrong", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        ///     Checks if a given path is a valid path by getting the full path and seeing if it generates an exception
        /// </summary>
        /// <param name="pathname"></param>
        /// <returns></returns>
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

        /// <summary>
        ///     Event for textbox to check input
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private new void PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        /// <summary>
        ///     Check if text matches numeric input
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private static bool IsTextAllowed(string text)
        {
            var regex = new Regex("[^0-9.-]+"); //regex that matches disallowed text
            return !regex.IsMatch(text);
        }
    }
}