﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Research.Input.Gaze;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SeeSaw
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private ImageData _imageData;
        public MainPage()
        {
            this.InitializeComponent();
        }
        private async void SelectMedia_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".png");
            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                _imageData = new ImageData();
                _imageData.File = file;
                _imageData.GazeEvents = new List<GazeEventArgs>();
                SelectedMedia.Text = file.Path;
            }
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _imageData = e.Parameter as ImageData;
            if ((_imageData != null) && (_imageData.File != null))
            {
                SelectedMedia.Text = _imageData.File.Path;
            }
        }
        private void ViewMedia_Click(object sender, RoutedEventArgs e)
        {
            _imageData.TrackGaze = true;
            _imageData.ShowPoints = false;
            _imageData.ShowTracks = false;
            Frame.Navigate(typeof(MediaPage), _imageData);
        }
        private void ViewResults_Click(object sender, RoutedEventArgs e)
        {
            _imageData.TrackGaze = false;
            _imageData.ShowPoints = true;
            _imageData.ShowTracks = (bool)ShowTracks.IsChecked;
            Frame.Navigate(typeof(MediaPage), _imageData);
        }
    }
}