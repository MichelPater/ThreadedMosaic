﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

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
            SaveFileDialog dlg = new SaveFileDialog();

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
                if (files.Any() && !String.IsNullOrEmpty(MasterImageTextBox.Text))
                {
                    //start analysis
                    Mosaic mosaic = new Mosaic(files.ToList(), MasterImageTextBox.Text);
                    SetProgressBarMaximum(files.Count());


                    //Check which option is selected
                    if ((bool)MosaicColorRadioButton.IsChecked)
                    {

                        var MosaicThread = new Thread(mosaic.CreateOutput);
                        MosaicThread.Start();
                    }
                    else if ((bool)MosaicHueRadioButton.IsChecked)
                    {

                    }
                    else if ((bool)MosaicPhotoRadioButton.IsChecked)
                    {
                        var MosaicThread = new Thread(mosaic.LoadImages);
                        MosaicThread.Start();
                    }



                }
            }
            else
            {
                //Display error
                //either the folders are incorrect or there are no files
            }
        }

        private void SetProgressBarMaximum(int limit)
        {
            ConversionProgress.Maximum = limit;
        }

        public void SetPogressBarValue(int value)
        {
            ConversionProgress.Value = value;
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
            SeedFolderTextbox.Text = @"E:\Downloads\Internet Destroying Wallpaper Dump\3000";
            //SeedFolderTextbox.Text = @"E:\Downloads\Internet Destroying Wallpaper Dump\imgur_abaz1";
            MasterImageTextBox.Text = @"E:\Downloads\Internet Destroying Wallpaper Dump\2yk0c4-bLLw6IL.jpg";

            OutputImageTextbox.Text = @"C:\Users\Michel\Desktop\Output folder\" + DateTime.Now + ".jpg";
        }
    }
}
