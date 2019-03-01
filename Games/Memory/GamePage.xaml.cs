﻿//Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license.
//See LICENSE in the project root for license information.

using Microsoft.Toolkit.Uwp.Input.GazeInteraction;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;


namespace Memory
{
    public sealed partial class GamePage : Page
    {
        const byte MIN_CHAR = 0x21;
        const byte MAX_CHAR = 0xE8;

        Random _rnd;
        Button _firstButton;
        Button _secondButton;
        DispatcherTimer _flashTimer;
        int _remaining;
        int _numMoves;
        bool _usePictures;

        bool _interactionPaused = false;

        bool _animationActive = false;
        bool _reverseAnimationActive = false;

        bool _gameOver = false;

        SolidColorBrush _solidTileBrush;        
        SolidColorBrush _toolButtonBrush;
        SolidColorBrush _pausedButtonBrush = new SolidColorBrush(Colors.Black);

        int _boardRows = 6;
        int _boardColumns = 11;

        CompositionScopedBatch _reverseFlipBatchAnimation;
        CompositionScopedBatch _flipBatchAnimation;

        public GamePage()
        {
            InitializeComponent();

            _solidTileBrush = (SolidColorBrush)this.Resources["TileBackground"];
            _toolButtonBrush = (SolidColorBrush)this.Resources["ToolBarButtonBackground"];

            GazeInput.DwellFeedbackCompleteBrush = new SolidColorBrush(Colors.Transparent);

            _rnd = new Random();
            _flashTimer = new DispatcherTimer();
            _flashTimer.Interval = new TimeSpan(0, 0, 2);
            _flashTimer.Tick += OnFlashTimerTick;
            _usePictures = false;

            Loaded += MainPage_Loaded;

            var sharedSettings = new ValueSet();
            GazeSettingsHelper.RetrieveSharedSettings(sharedSettings).Completed = new AsyncActionCompletedHandler((asyncInfo, asyncStatus) => {
                var gazePointer = GazeInput.GetGazePointer(this);
                gazePointer.LoadSettings(sharedSettings);
            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);            

            switch (e.Parameter.ToString())
            {
                case "16":
                    _boardRows = 4;
                    _boardColumns = 4;
                    break;

                case "24":
                    _boardRows = 4;
                    _boardColumns = 6;
                    break;

                case "36":
                    _boardRows = 6;
                    _boardColumns = 6;
                    break;

                case "66":
                    _boardRows = 6;
                    _boardColumns = 11;
                    break;

                default:
                    _boardRows = 4;
                    _boardColumns = 4;
                    break;
            }
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            ArrangeBoardLayout();
            ResetBoard();
        }

        private void ArrangeBoardLayout()
        {
            buttonMatrix.Children.Clear();
            buttonMatrix.RowDefinitions.Clear();
            buttonMatrix.ColumnDefinitions.Clear();

            for (int row = 0; row < _boardRows; row++)
            {
                buttonMatrix.RowDefinitions.Add(new RowDefinition());                
            }

            for (int column = 0; column < _boardColumns; column++)
            {
                buttonMatrix.ColumnDefinitions.Add(new ColumnDefinition());

            }

            for (int row = 0; row < _boardRows; row++)
            {
                for (int col = 0; col < _boardColumns; col++)
                {
                    var button = new Button();
                    button.Click += OnButtonClick;
                    button.Style = Resources["ButtonStyle"] as Style;
                    Grid.SetRow(button, row);
                    Grid.SetColumn(button, col);
                    buttonMatrix.Children.Add(button);
                }
            }

            buttonMatrix.UpdateLayout();
        }

        private void OnFlashTimerTick(object sender, object e)
        {
            _reverseAnimationActive = true;

            //Flip button visual
            var btn1Visual = ElementCompositionPreview.GetElementVisual(_firstButton);
            var btn2Visual = ElementCompositionPreview.GetElementVisual(_secondButton);
            var compositor = btn1Visual.Compositor;

            //Get a visual for the content
            var btn1Content = VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(_firstButton, 0), 0);
            var btn1ContentVisual = ElementCompositionPreview.GetElementVisual(btn1Content as FrameworkElement);
            var btn2Content = VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(_secondButton, 0), 0);
            var btn2ContentVisual = ElementCompositionPreview.GetElementVisual(btn2Content as FrameworkElement);

            var easing = compositor.CreateLinearEasingFunction();

            if (_reverseFlipBatchAnimation != null)
            {
                _reverseFlipBatchAnimation.Completed -= ReverseFlipBatchAnimation_Completed;
                _reverseFlipBatchAnimation.Dispose();
            }

            _reverseFlipBatchAnimation = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            _reverseFlipBatchAnimation.Completed += ReverseFlipBatchAnimation_Completed;

            ScalarKeyFrameAnimation flipAnimation = compositor.CreateScalarKeyFrameAnimation();
            flipAnimation.InsertKeyFrame(0.000001f, 0);
            flipAnimation.InsertKeyFrame(1f, 180, easing);
            flipAnimation.Duration = TimeSpan.FromMilliseconds(400);
            flipAnimation.IterationBehavior = AnimationIterationBehavior.Count;
            flipAnimation.IterationCount = 1;
            btn1Visual.CenterPoint = new Vector3((float)(0.5 * _firstButton.ActualWidth), (float)(0.5f * _firstButton.ActualHeight), (float)(_firstButton.ActualWidth / 4));
            btn1Visual.RotationAxis = new Vector3(0.0f, 1f, 0f);

            ScalarKeyFrameAnimation appearAnimation = compositor.CreateScalarKeyFrameAnimation();
            appearAnimation.InsertKeyFrame(0.0f, 1);
            appearAnimation.InsertKeyFrame(0.499999f, 1);
            appearAnimation.InsertKeyFrame(0.5f, 0);
            appearAnimation.InsertKeyFrame(1f, 0);
            appearAnimation.Duration = TimeSpan.FromMilliseconds(400);
            appearAnimation.IterationBehavior = AnimationIterationBehavior.Count;
            appearAnimation.IterationCount = 1;

            btn1Visual.StartAnimation(nameof(btn1Visual.RotationAngleInDegrees), flipAnimation);
            btn2Visual.StartAnimation(nameof(btn2Visual.RotationAngleInDegrees), flipAnimation);
            btn1ContentVisual.StartAnimation(nameof(btn1ContentVisual.Opacity), appearAnimation);
            btn2ContentVisual.StartAnimation(nameof(btn2ContentVisual.Opacity), appearAnimation);
            _reverseFlipBatchAnimation.End();
           
            _flashTimer.Stop();
            GazeInput.SetInteraction(buttonMatrix, Interaction.Enabled);
        }

        private void ReverseFlipBatchAnimation_Completed(object sender, CompositionBatchCompletedEventArgs args)
        {
            _firstButton.Content = null;
            _secondButton.Content = null;
            _firstButton = null;
            _secondButton = null;
            _reverseAnimationActive = false;
        }

        List<Button> ShuffleList(List<Button> list)
        {
            var len = list.Count;
            for (var i = 0; i < len; i++)
            {
                var j = _rnd.Next(0, len);
                var k = list[i];
                list[i] = list[j];
                list[j] = k;
            }
            return list;
        }

        async Task<List<string>> GetPicturesContent(int len)
        {
            StorageFolder picturesFolder = KnownFolders.PicturesLibrary;            
            IReadOnlyList<StorageFile> pictures = await picturesFolder.GetFilesAsync();
            List<string> list = new List<string>();

            for (int i = 0; i < len; i++)
            {
                string filename;
                do
                {
                    filename = pictures[_rnd.Next(pictures.Count)].Path;
                }
                while (list.Contains(filename));

                list.Add(filename.ToString());
            }
            return list;
        }

        List<string> GetSymbolContent(int len)
        {
            List<string> list = new List<string>();
            for (int i = 0; i < len; i++)
            {
                char ch;
                do
                {
                    ch = Convert.ToChar(_rnd.Next(MIN_CHAR, MAX_CHAR));
                }
                while (list.Contains(ch.ToString()));

                list.Add(ch.ToString());
            }
            return list;
        }

        List<Button> GetButtonList()
        {
            List<Button> list = new List<Button>();            
            foreach (Button button in buttonMatrix.Children)
            {
                list.Add(button);
            }
            return ShuffleList(list);
        }

        async void ResetBoard()
        {
            PlayAgainText.Visibility = Visibility.Collapsed;
            _gameOver = false;
            _firstButton = null;
            _secondButton = null;
            _numMoves = 0;
            MoveCountTextBlock.Text = _numMoves.ToString();            
            _remaining = _boardRows * _boardColumns;
            var pairs = (_boardRows * _boardColumns) / 2;

            List<string> listContent;
            if (_usePictures)
            {
                try
                {                
                    listContent = await GetPicturesContent(pairs);
                }
                catch
                {
                    listContent = GetSymbolContent(pairs);
                    _usePictures = false;
                }
            }
            else
            {
                listContent = GetSymbolContent(pairs);
            }

            List<Button> listButtons = GetButtonList();

            for (int i = 0; i < _boardRows * _boardColumns; i += 2)
            {
                listButtons[i].Content = null;
                listButtons[i + 1].Content = null;

                listButtons[i].Tag = listContent[i / 2];
                listButtons[i + 1].Tag = listContent[i / 2];
            }
        }

        private async void OnButtonClick(object sender, RoutedEventArgs e)
        {
            if (_animationActive || _reverseAnimationActive) return;

            if (_gameOver)
            {                
                return;
            }

            var btn = sender as Button;
            if (btn.Content != null)
            {             
                return;
            }          

            if (_flashTimer.IsEnabled)
            {
                return;
                //OnFlashTimerTick(null, null);  //Unclear about the original idea of this
            }

            _animationActive = true;
            _numMoves++;
            MoveCountTextBlock.Text = _numMoves.ToString();

            if (_firstButton == null)
            {
                _firstButton = btn;
            }
            else
            {
                _secondButton = btn;
            }

            //Flip button visual
            var btnVisual = ElementCompositionPreview.GetElementVisual(btn);
            var compositor = btnVisual.Compositor;

            //Get a visual for the content
            var btnContent = VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(btn, 0), 0);
            var btnContentVisual = ElementCompositionPreview.GetElementVisual(btnContent as FrameworkElement);

            var easing = compositor.CreateLinearEasingFunction();

            if (_flipBatchAnimation != null)
            {
                _flipBatchAnimation.Completed -= FlipBatchAnimation_Completed;
                _flipBatchAnimation.Dispose();
            }

            _flipBatchAnimation = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            _flipBatchAnimation.Completed += FlipBatchAnimation_Completed;

            ScalarKeyFrameAnimation flipAnimation = compositor.CreateScalarKeyFrameAnimation();
            flipAnimation.InsertKeyFrame(0.000001f, 180);
            flipAnimation.InsertKeyFrame(1f, 0, easing);
            flipAnimation.Duration = TimeSpan.FromMilliseconds(800);
            flipAnimation.IterationBehavior = AnimationIterationBehavior.Count;
            flipAnimation.IterationCount = 1;
            btnVisual.CenterPoint = new Vector3((float)(0.5 * btn.ActualWidth), (float)(0.5f * btn.ActualHeight), (float)(btn.ActualWidth / 4));
            btnVisual.RotationAxis = new Vector3(0.0f, 1f, 0f);

            ScalarKeyFrameAnimation appearAnimation = compositor.CreateScalarKeyFrameAnimation();
            appearAnimation.InsertKeyFrame(0.0f, 0);
            appearAnimation.InsertKeyFrame(0.499999f, 0);
            appearAnimation.InsertKeyFrame(0.5f, 1);
            appearAnimation.InsertKeyFrame(1f, 1);
            appearAnimation.Duration = TimeSpan.FromMilliseconds(800);
            appearAnimation.IterationBehavior = AnimationIterationBehavior.Count;
            appearAnimation.IterationCount = 1;

            btnVisual.StartAnimation(nameof(btnVisual.RotationAngleInDegrees), flipAnimation);
            btnContentVisual.StartAnimation(nameof(btnContentVisual.Opacity), appearAnimation);
            _flipBatchAnimation.End();

            if (_usePictures)
            {
                var file = await StorageFile.GetFileFromPathAsync(btn.Tag.ToString());
                using (var stream = await file.OpenAsync(FileAccessMode.Read))
                {
                    var image = new Image();
                    var bmp = new BitmapImage();
                    await bmp.SetSourceAsync(stream);
                    image.Source = bmp;
                    btn.Content = image;
                }
            }
            else
            {
                btn.Content = btn.Tag.ToString();
            }           
        }

        private void FlipBatchAnimation_Completed(object sender, CompositionBatchCompletedEventArgs args)
        {
            _animationActive = false;

            if (_secondButton == null)
            {
                return;
            }

            if (_secondButton.Tag.ToString() != _firstButton.Tag.ToString())
            {
                GazeInput.SetInteraction(buttonMatrix, Interaction.Disabled);
                _flashTimer.Start();            
            }
            else            
            {            
                _firstButton = null;
                _secondButton = null;
                _remaining -= 2;

                CheckGameCompletion();
            }
        }

        void CheckGameCompletion()
        {
            if (_remaining > 0)
            {
                return;
            }

            string message = $"You completed the board in {_numMoves} moves!";
            DialogText.Text = message;
            DialogGrid.Visibility = Visibility.Visible;
            _gameOver = true;
        }

        private void DialogButton_Click(object sender, RoutedEventArgs e)
        {
            GazeInput.DwellFeedbackProgressBrush = new SolidColorBrush(Colors.White);
            DialogGrid.Visibility = Visibility.Collapsed;
            ResetBoard();
        }

        private void DialogButton2_Click(object sender, RoutedEventArgs e)
        {
            GazeInput.DwellFeedbackProgressBrush = new SolidColorBrush(Colors.White);
            DialogGrid.Visibility = Visibility.Collapsed;
            Frame.Navigate(typeof(MainPage));
        }


        private void DismissButton(object sender, RoutedEventArgs e)
        {
            GazeInput.DwellFeedbackProgressBrush = new SolidColorBrush(Colors.White);
            DialogGrid.Visibility = Visibility.Collapsed;

            //RootGrid.Children.Remove(PlayAgainButton);
            //Grid.SetColumn(PlayAgainButton, Grid.GetColumn(_buttons[_boardSize - 1, _boardSize - 1]));
            //Grid.SetRow(PlayAgainButton, Grid.GetRow(_buttons[_boardSize - 1, _boardSize - 1]));
            //PlayAgainButton.MaxWidth = _buttons[_boardSize - 1, _boardSize - 1].ActualWidth;
            //PlayAgainButton.MaxHeight = _buttons[_boardSize - 1, _boardSize - 1].ActualHeight;
            //GameGrid.Children.Add(PlayAgainButton);
            //PlayAgainButton.Visibility = Visibility.Visible;

            PlayAgainText.Visibility = Visibility.Visible;
            OnPause(PauseButton, null);
        }


        private void OnPause(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;

            if (_interactionPaused)
            {
                PauseButtonText.Text = "\uE769";
                PauseButtonBorder.Background = _toolButtonBrush;
                GazeInput.SetInteraction(buttonMatrix, Interaction.Enabled);
                _interactionPaused = false;
                if (_gameOver)
                {
                    ResetBoard();
                }
            }
            else
            {
                PauseButtonText.Text = "\uE768";
                PauseButtonBorder.Background = _pausedButtonBrush;
                GazeInput.SetInteraction(buttonMatrix, Interaction.Disabled);
                _interactionPaused = true;
            }
        }

        private void OnExit(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        private void OnBack(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }
    }
}

