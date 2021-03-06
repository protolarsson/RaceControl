﻿using LibVLCSharp.Shared;
using LibVLCSharp.Shared.Structures;
using NLog;
using Prism.Commands;
using Prism.Events;
using Prism.Services.Dialogs;
using RaceControl.Common.Interfaces;
using RaceControl.Common.Settings;
using RaceControl.Core.Mvvm;
using RaceControl.Enums;
using RaceControl.Events;
using RaceControl.Interfaces;
using RaceControl.Services.Interfaces.F1TV;
using RaceControl.Streamlink;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Application = System.Windows.Application;
using Cursors = System.Windows.Input.Cursors;
using Screen = System.Windows.Forms.Screen;
using Timer = System.Timers.Timer;

namespace RaceControl.ViewModels
{
    public class VideoDialogViewModel : DialogViewModelBase, IVideoDialogViewModel
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IApiService _apiService;
        private readonly IStreamlinkLauncher _streamlinkLauncher;
        private readonly ISettings _settings;
        private readonly LibVLC _libVLC;

        private ICommand _mouseDownVideoCommand;
        private ICommand _mouseMoveVideoCommand;
        private ICommand _mouseEnterVideoCommand;
        private ICommand _mouseLeaveVideoCommand;
        private ICommand _mouseMoveControlBarCommand;
        private ICommand _mouseEnterControlBarCommand;
        private ICommand _mouseLeaveControlBarCommand;
        private ICommand _togglePauseCommand;
        private ICommand _toggleMuteCommand;
        private ICommand _fastForwardCommand;
        private ICommand _syncSessionCommand;
        private ICommand _toggleRecordingCommand;
        private ICommand _toggleFullScreenCommand;
        private ICommand _moveToCornerCommand;
        private ICommand _audioTrackSelectionChangedCommand;
        private ICommand _scanChromecastCommand;
        private ICommand _startCastVideoCommand;
        private ICommand _stopCastVideoCommand;

        private Process _streamlinkProcess;
        private Process _streamlinkRecordingProcess;
        private IPlayableContent _playableContent;
        private string _token;
        private bool _isStreamlink;
        private bool _isPaused;
        private bool _isMuted;
        private bool _isRecording;
        private bool _isScanning;
        private bool _isCasting;
        private bool _showControls;
        private long _duration;
        private long _sliderTime;
        private TimeSpan _displayTime;
        private ObservableCollection<TrackDescription> _audioTrackDescriptions;
        private ObservableCollection<RendererItem> _rendererItems;
        private RendererItem _selectedRendererItem;
        private Timer _showControlsTimer;
        private SubscriptionToken _syncStreamsEventToken;
        private double _top;
        private double _left;
        private double _width = 960;
        private double _height = 550;
        private ResizeMode _resizeMode = ResizeMode.CanResize;
        private WindowState _windowState = WindowState.Normal;
        private WindowStartupLocation _startupLocation = WindowStartupLocation.CenterOwner;
        private bool _topmost;
        private string _carImageUrl;
        private string _headshotImageUrl;

        public VideoDialogViewModel(
            ILogger logger,
            IEventAggregator eventAggregator,
            IApiService apiService,
            IStreamlinkLauncher streamlinkLauncher,
            ISettings settings,
            LibVLC libVLC,
            MediaPlayer mediaPlayer)
            : base(logger)
        {
            _eventAggregator = eventAggregator;
            _apiService = apiService;
            _streamlinkLauncher = streamlinkLauncher;
            _settings = settings;
            _libVLC = libVLC;
            MediaPlayer = mediaPlayer;
        }

        public ICommand MouseDownVideoCommand => _mouseDownVideoCommand ??= new DelegateCommand<MouseButtonEventArgs>(MouseDownVideoExecute);
        public ICommand MouseMoveVideoCommand => _mouseMoveVideoCommand ??= new DelegateCommand(MouseEnterOrLeaveOrMoveVideoExecute);
        public ICommand MouseEnterVideoCommand => _mouseEnterVideoCommand ??= new DelegateCommand(MouseEnterOrLeaveOrMoveVideoExecute);
        public ICommand MouseLeaveVideoCommand => _mouseLeaveVideoCommand ??= new DelegateCommand(MouseEnterOrLeaveOrMoveVideoExecute);
        public ICommand MouseMoveControlBarCommand => _mouseMoveControlBarCommand ??= new DelegateCommand(MouseMoveControlBarExecute);
        public ICommand MouseEnterControlBarCommand => _mouseEnterControlBarCommand ??= new DelegateCommand(MouseEnterControlBarExecute);
        public ICommand MouseLeaveControlBarCommand => _mouseLeaveControlBarCommand ??= new DelegateCommand(MouseLeaveControlBarExecute);
        public ICommand TogglePauseCommand => _togglePauseCommand ??= new DelegateCommand(TogglePauseExecute).ObservesCanExecute(() => Initialized);
        public ICommand ToggleMuteCommand => _toggleMuteCommand ??= new DelegateCommand(ToggleMuteExecute).ObservesCanExecute(() => Initialized);
        public ICommand FastForwardCommand => _fastForwardCommand ??= new DelegateCommand<string>(FastForwardExecute, CanFastForwardExecute).ObservesProperty(() => Initialized).ObservesProperty(() => PlayableContent);
        public ICommand SyncSessionCommand => _syncSessionCommand ??= new DelegateCommand(SyncSessionExecute, CanSyncSessionExecute).ObservesProperty(() => Initialized).ObservesProperty(() => PlayableContent);
        public ICommand ToggleRecordingCommand => _toggleRecordingCommand ??= new DelegateCommand(ToggleRecordingExecute, CanToggleRecordingExecute).ObservesProperty(() => Initialized).ObservesProperty(() => PlayableContent).ObservesProperty(() => IsRecording).ObservesProperty(() => IsPaused);
        public ICommand ToggleFullScreenCommand => _toggleFullScreenCommand ??= new DelegateCommand(ToggleFullScreenExecute);
        public ICommand MoveToCornerCommand => _moveToCornerCommand ??= new DelegateCommand<WindowLocation?>(MoveToCornerExecute, CanMoveToCornerExecute).ObservesProperty(() => WindowState);
        public ICommand AudioTrackSelectionChangedCommand => _audioTrackSelectionChangedCommand ??= new DelegateCommand<SelectionChangedEventArgs>(AudioTrackSelectionChangedExecute);
        public ICommand ScanChromecastCommand => _scanChromecastCommand ??= new DelegateCommand(ScanChromecastExecute, CanScanChromecastExecute).ObservesProperty(() => Initialized).ObservesProperty(() => IsScanning);
        public ICommand StartCastVideoCommand => _startCastVideoCommand ??= new DelegateCommand(StartCastVideoExecute, CanStartCastVideoExecute).ObservesProperty(() => Initialized).ObservesProperty(() => SelectedRendererItem);
        public ICommand StopCastVideoCommand => _stopCastVideoCommand ??= new DelegateCommand(StopCastVideoExecute, CanStopCastVideoExecute).ObservesProperty(() => IsCasting);

        public override string Title => PlayableContent?.Title;

        public Guid UniqueIdentifier { get; } = Guid.NewGuid();

        public MediaPlayer MediaPlayer { get; }

        public IPlayableContent PlayableContent
        {
            get => _playableContent;
            set => SetProperty(ref _playableContent, value);
        }

        public bool IsStreamlink
        {
            get => _isStreamlink;
            set => SetProperty(ref _isStreamlink, value);
        }

        public bool IsPaused
        {
            get => _isPaused;
            set => SetProperty(ref _isPaused, value);
        }

        public bool IsMuted
        {
            get => _isMuted;
            set => SetProperty(ref _isMuted, value);
        }

        public bool IsRecording
        {
            get => _isRecording;
            set => SetProperty(ref _isRecording, value);
        }

        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        public bool IsCasting
        {
            get => _isCasting;
            set => SetProperty(ref _isCasting, value);
        }

        public bool ShowControls
        {
            get => _showControls;
            set => SetProperty(ref _showControls, value);
        }

        public long Duration
        {
            get => _duration;
            set => SetProperty(ref _duration, value);
        }

        public long SliderTime
        {
            get => _sliderTime;
            set
            {
                if (SetProperty(ref _sliderTime, value))
                {
                    SetMediaPlayerTime(_sliderTime);
                }
            }
        }

        public TimeSpan DisplayTime
        {
            get => _displayTime;
            set => SetProperty(ref _displayTime, value);
        }

        public ObservableCollection<TrackDescription> AudioTrackDescriptions
        {
            get => _audioTrackDescriptions ??= new ObservableCollection<TrackDescription>();
            set => SetProperty(ref _audioTrackDescriptions, value);
        }

        public ObservableCollection<RendererItem> RendererItems
        {
            get => _rendererItems ??= new ObservableCollection<RendererItem>();
            set => SetProperty(ref _rendererItems, value);
        }

        public RendererItem SelectedRendererItem
        {
            get => _selectedRendererItem;
            set => SetProperty(ref _selectedRendererItem, value);
        }

        public double Top
        {
            get => _top;
            set => SetProperty(ref _top, value);
        }

        public double Left
        {
            get => _left;
            set => SetProperty(ref _left, value);
        }

        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        public ResizeMode ResizeMode
        {
            get => _resizeMode;
            set => SetProperty(ref _resizeMode, value);
        }

        public WindowState WindowState
        {
            get => _windowState;
            set => SetProperty(ref _windowState, value);
        }

        public WindowStartupLocation StartupLocation
        {
            get => _startupLocation;
            set => SetProperty(ref _startupLocation, value);
        }

        public bool Topmost
        {
            get => _topmost;
            set => SetProperty(ref _topmost, value);
        }

        public string CarImageUrl
        {
            get => _carImageUrl;
            set => SetProperty(ref _carImageUrl, value);
        }

        public string HeadshotImageUrl
        {
            get => _headshotImageUrl;
            set => SetProperty(ref _headshotImageUrl, value);
        }

        public VideoDialogInstance GetVideoDialogInstance()
        {
            return new VideoDialogInstance
            {
                Top = Top,
                Left = Left,
                Width = Width,
                Height = Height,
                ResizeMode = (int)ResizeMode,
                WindowState = (int)WindowState,
                Topmost = Topmost,
                ChannelName = PlayableContent.Name
            };
        }

        public override async void OnDialogOpened(IDialogParameters parameters)
        {
            _token = parameters.GetValue<string>(ParameterNames.TOKEN);
            PlayableContent = parameters.GetValue<IPlayableContent>(ParameterNames.PLAYABLE_CONTENT);
            IsStreamlink = PlayableContent.IsLive && !_settings.DisableStreamlink;

            var instance = parameters.GetValue<VideoDialogInstance>(ParameterNames.INSTANCE);
            SetWindowLocation(instance);

            var streamUrl = await GenerateStreamUrlAsync();

            if (streamUrl == null)
            {
                Logger.Error("Closing video player, stream URL is empty.");
                CloseWindow(true);
                return;
            }

            if (IsStreamlink)
            {
                var (streamlinkProcess, streamlinkUrl) = await _streamlinkLauncher.StartStreamlinkExternal(streamUrl);
                _streamlinkProcess = streamlinkProcess;
                streamUrl = streamlinkUrl;
            }

            CreateMediaPlayer();
            var media = CreateMedia(streamUrl);
            StartPlayback(media);

            _showControlsTimer = new Timer(2000) { AutoReset = false };
            _showControlsTimer.Elapsed += ShowControlsTimer_Elapsed;
            _showControlsTimer.Start();
            _syncStreamsEventToken = _eventAggregator.GetEvent<SyncStreamsEvent>().Subscribe(OnSyncSession);

            await LoadDriverImageUrlsAsync();

            base.OnDialogOpened(parameters);
        }

        public override void OnDialogClosed()
        {
            if (_syncStreamsEventToken != null)
            {
                _eventAggregator.GetEvent<SyncStreamsEvent>().Unsubscribe(_syncStreamsEventToken);
            }

            if (_showControlsTimer != null)
            {
                _showControlsTimer.Stop();
                _showControlsTimer.Elapsed -= ShowControlsTimer_Elapsed;
                _showControlsTimer.Dispose();
                _showControlsTimer = null;
            }

            StopPlayback();
            RemoveMedia(MediaPlayer.Media);
            RemoveMediaPlayer();

            CleanupProcess(_streamlinkProcess);
            CleanupProcess(_streamlinkRecordingProcess);

            base.OnDialogClosed();
        }

        protected override void CloseWindowExecute()
        {
            var parameters = new DialogParameters
            {
                { ParameterNames.UNIQUE_IDENTIFIER, UniqueIdentifier }
            };

            RaiseRequestClose(new DialogResult(ButtonResult.None, parameters));
        }

        private void Media_DurationChanged(object sender, MediaDurationChangedEventArgs e)
        {
            Duration = e.Duration;
        }

        private void MediaPlayer_Playing(object sender, EventArgs e)
        {
            IsPaused = false;
        }

        private void MediaPlayer_Paused(object sender, EventArgs e)
        {
            IsPaused = true;
        }

        private void MediaPlayer_Unmuted(object sender, EventArgs e)
        {
            IsMuted = false;
        }

        private void MediaPlayer_Muted(object sender, EventArgs e)
        {
            IsMuted = true;
        }

        private void MediaPlayer_TimeChanged(object sender, MediaPlayerTimeChangedEventArgs e)
        {
            SetProperty(ref _sliderTime, e.Time, nameof(SliderTime));
            DisplayTime = TimeSpan.FromMilliseconds(e.Time);
        }

        private void MediaPlayer_ESAdded(object sender, MediaPlayerESAddedEventArgs e)
        {
            if (e.Id >= 0)
            {
                switch (e.Type)
                {
                    case TrackType.Audio:
                        var audioTrackDescription = MediaPlayer.AudioTrackDescription.First(p => p.Id == e.Id);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AudioTrackDescriptions.Add(audioTrackDescription);
                        });
                        break;
                }
            }
        }

        private void MediaPlayer_ESDeleted(object sender, MediaPlayerESDeletedEventArgs e)
        {
            if (e.Id >= 0)
            {
                switch (e.Type)
                {
                    case TrackType.Audio:
                        var audioTrackDescription = AudioTrackDescriptions.First(p => p.Id == e.Id);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AudioTrackDescriptions.Remove(audioTrackDescription);
                        });
                        break;
                }
            }
        }

        private void ShowControlsTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ShowControls = false;
                Mouse.OverrideCursor = Cursors.None;
            });
        }

        private void RendererDiscoverer_ItemAdded(object sender, RendererDiscovererItemAddedEventArgs e)
        {
            if (e.RendererItem.CanRenderVideo)
            {
                Logger.Info($"Found renderer '{e.RendererItem.Name}' of type '{e.RendererItem.Type}'.");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    RendererItems.Add(e.RendererItem);
                    SelectedRendererItem ??= e.RendererItem;
                });
            }
        }

        private void MouseDownVideoExecute(MouseButtonEventArgs args)
        {
            if (args.ChangedButton != MouseButton.Left)
            {
                return;
            }

            switch (args.ClickCount)
            {
                case 1:
                    if (args.Source is DependencyObject dependencyObject)
                    {
                        Window.GetWindow(dependencyObject)?.Owner?.DragMove();
                    }

                    break;

                case 2:
                    if (ToggleFullScreenCommand.CanExecute(null))
                    {
                        ToggleFullScreenCommand.Execute(null);
                    }

                    break;
            }
        }

        private void MouseEnterOrLeaveOrMoveVideoExecute()
        {
            _showControlsTimer?.Stop();

            Application.Current.Dispatcher.Invoke(() =>
            {
                ShowControls = true;
                Mouse.OverrideCursor = null;
            });

            _showControlsTimer?.Start();
        }

        private static void MouseMoveControlBarExecute()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
            });
        }

        private void MouseEnterControlBarExecute()
        {
            _showControlsTimer?.Stop();
        }

        private void MouseLeaveControlBarExecute()
        {
            _showControlsTimer?.Start();
        }

        private void TogglePauseExecute()
        {
            if (MediaPlayer.CanPause)
            {
                Logger.Info("Toggling pause...");
                MediaPlayer.Pause();
            }
        }

        private void ToggleMuteExecute()
        {
            Logger.Info("Toggling mute...");
            MediaPlayer.ToggleMute();
        }

        private bool CanFastForwardExecute(string arg)
        {
            return Initialized && !PlayableContent.IsLive;
        }

        private void FastForwardExecute(string value)
        {
            if (int.TryParse(value, out var seconds))
            {
                Logger.Info($"Fast forwarding stream {seconds} seconds...");
                var time = MediaPlayer.Time + seconds * 1000;
                SetMediaPlayerTime(time);
            }
        }

        private bool CanSyncSessionExecute()
        {
            return Initialized && !PlayableContent.IsLive;
        }

        private void SyncSessionExecute()
        {
            var payload = new SyncStreamsEventPayload(UniqueIdentifier, PlayableContent.SyncUID, MediaPlayer.Time);
            Logger.Info($"Syncing streams with sync-UID '{payload.SyncUID}' to timestamp '{payload.Time}'...");
            _eventAggregator.GetEvent<SyncStreamsEvent>().Publish(payload);
        }

        private bool CanToggleRecordingExecute()
        {
            return Initialized && PlayableContent.IsLive && (IsRecording || !IsPaused);
        }

        private async void ToggleRecordingExecute()
        {
            if (!IsRecording)
            {
                IsRecording = await StartRecordingAsync();
            }
            else
            {
                StopRecording();
                IsRecording = false;
            }
        }

        private void OnSyncSession(SyncStreamsEventPayload payload)
        {
            if (UniqueIdentifier != payload.RequesterIdentifier && PlayableContent.SyncUID == payload.SyncUID)
            {
                SetMediaPlayerTime(payload.Time);
            }
        }

        private void ToggleFullScreenExecute()
        {
            if (WindowState != WindowState.Maximized)
            {
                SetFullScreen();
            }
            else
            {
                SetWindowed();
            }
        }

        private bool CanMoveToCornerExecute(WindowLocation? location)
        {
            return WindowState != WindowState.Maximized && location != null;
        }

        private void MoveToCornerExecute(WindowLocation? location)
        {
            Logger.Info($"Moving window to corner '{location}'...");

            var screen = Screen.FromRectangle(new Rectangle((int)Left, (int)Top, (int)Width, (int)Height));
            var scale = Math.Max(Screen.PrimaryScreen.WorkingArea.Width / SystemParameters.PrimaryScreenWidth, Screen.PrimaryScreen.WorkingArea.Height / SystemParameters.PrimaryScreenHeight);
            var top = screen.WorkingArea.Top / scale;
            var left = screen.WorkingArea.Left / scale;
            var width = screen.WorkingArea.Width / 2D / scale;
            var height = screen.WorkingArea.Height / 2D / scale;

            ResizeMode = ResizeMode.NoResize;

            switch (location)
            {
                case WindowLocation.TopLeft:
                    Top = top;
                    Left = left;
                    break;

                case WindowLocation.TopRight:
                    Top = top;
                    Left = left + width;
                    break;

                case WindowLocation.BottomLeft:
                    Top = top + height;
                    Left = left;
                    break;

                case WindowLocation.BottomRight:
                    Top = top + height;
                    Left = left + width;
                    break;
            }

            Width = width;
            Height = height;

            Logger.Info($"Done moving window (top: {Top}, left: {Left}, width: {Width}, height: {Height}).");
        }

        private async void AudioTrackSelectionChangedExecute(SelectionChangedEventArgs args)
        {
            if (args.AddedItems.Count > 0 && args.AddedItems[0] is TrackDescription trackDescription)
            {
                Logger.Info($"Changing audio track to '{trackDescription.Id}'...");
                MediaPlayer.SetAudioTrack(trackDescription.Id);

                if (!IsStreamlink)
                {
                    // Workaround to fix audio out of sync after switching audio track
                    await Task.Delay(TimeSpan.FromMilliseconds(250));
                    SetMediaPlayerTime(MediaPlayer.Time - 500);
                }

                Logger.Info("Done changing audio track.");
            }
        }

        private bool CanScanChromecastExecute()
        {
            return Initialized && !IsScanning;
        }

        private async void ScanChromecastExecute()
        {
            IsScanning = true;
            Logger.Info("Scanning for Chromecast devices...");
            RendererItems.Clear();

            using (var rendererDiscoverer = new RendererDiscoverer(_libVLC))
            {
                rendererDiscoverer.ItemAdded += RendererDiscoverer_ItemAdded;

                if (rendererDiscoverer.Start())
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    rendererDiscoverer.Stop();
                }
                else
                {
                    Logger.Warn("Could not start scanning for Chromecast devices.");
                }

                rendererDiscoverer.ItemAdded -= RendererDiscoverer_ItemAdded;
            }

            Logger.Info("Done scanning for Chromecast devices.");
            IsScanning = false;
        }

        private bool CanStartCastVideoExecute()
        {
            return Initialized && SelectedRendererItem != null;
        }

        private async void StartCastVideoExecute()
        {
            Logger.Info($"Starting casting of video with renderer '{SelectedRendererItem.Name}'...");

            if (await ChangeRendererAsync(SelectedRendererItem))
            {
                IsCasting = true;
            }
        }

        private bool CanStopCastVideoExecute()
        {
            return IsCasting;
        }

        private async void StopCastVideoExecute()
        {
            Logger.Info("Stopping casting of video...");

            if (await ChangeRendererAsync(null))
            {
                IsCasting = false;
            }
        }

        private void SetWindowLocation(VideoDialogInstance instance)
        {
            if (instance != null)
            {
                StartupLocation = WindowStartupLocation.Manual;
                ResizeMode = (ResizeMode)instance.ResizeMode;
                WindowState = (WindowState)instance.WindowState;
                Topmost = instance.Topmost;
                Top = instance.Top;
                Left = instance.Left;

                if (WindowState != WindowState.Maximized)
                {
                    Width = instance.Width;
                    Height = instance.Height;
                }
            }
            else
            {
                StartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private async Task<string> GenerateStreamUrlAsync()
        {
            try
            {
                return await _apiService.GetTokenisedUrlAsync(_token, PlayableContent);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "An error occurred while trying to get tokenised URL.");
            }

            return null;
        }

        private async Task LoadDriverImageUrlsAsync()
        {
            if (string.IsNullOrWhiteSpace(PlayableContent.DriverUID))
            {
                return;
            }

            try
            {
                var driver = await _apiService.GetDriverAsync(PlayableContent.DriverUID);

                if (driver != null)
                {
                    CarImageUrl = driver.CarUrl;
                    HeadshotImageUrl = driver.HeadshotUrl;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "An error occurred while trying to get driver images.");
            }
        }

        private async Task<bool> ChangeRendererAsync(RendererItem renderer)
        {
            Logger.Info($"Changing renderer to '{renderer?.Name}'...");

            var time = MediaPlayer.Time;
            var media = MediaPlayer.Media;
            var streamUrl = IsStreamlink ? media?.Mrl : await GenerateStreamUrlAsync();

            if (streamUrl == null)
            {
                Logger.Error("Renderer not changed, stream URL is empty.");
                return false;
            }

            StopPlayback();
            RemoveMedia(media);
            media = CreateMedia(streamUrl);
            StartPlayback(media, renderer);

            if (!PlayableContent.IsLive)
            {
                SetMediaPlayerTime(time);
            }

            Logger.Info("Done changing renderer.");

            return true;
        }

        private async Task<bool> StartRecordingAsync()
        {
            Logger.Info("Starting recording process...");
            var streamUrl = await GenerateStreamUrlAsync();

            if (streamUrl == null)
            {
                Logger.Error("Recording process not started, stream URL is empty.");
                return false;
            }

            _streamlinkRecordingProcess = _streamlinkLauncher.StartStreamlinkRecording(streamUrl, PlayableContent.Title);
            Logger.Info("Recording process started.");

            return true;
        }

        private void StopRecording()
        {
            Logger.Info("Stopping recording process...");
            CleanupProcess(_streamlinkRecordingProcess);
            _streamlinkRecordingProcess = null;
            Logger.Info("Recording process stopped.");
        }

        private void SetFullScreen()
        {
            Logger.Info("Changing to fullscreen mode...");
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }

        private void SetWindowed()
        {
            Logger.Info("Changing to windowed mode...");
            WindowState = WindowState.Normal;
        }

        private void SetMediaPlayerTime(long time)
        {
            Logger.Info($"Setting mediaplayer time to '{time}'...");
            MediaPlayer.Time = Math.Max(time, 0);
        }

        private void CreateMediaPlayer()
        {
            Logger.Info("Creating mediaplayer...");
            MediaPlayer.Playing += MediaPlayer_Playing;
            MediaPlayer.Paused += MediaPlayer_Paused;
            MediaPlayer.Muted += MediaPlayer_Muted;
            MediaPlayer.Unmuted += MediaPlayer_Unmuted;
            MediaPlayer.TimeChanged += MediaPlayer_TimeChanged;
            MediaPlayer.ESAdded += MediaPlayer_ESAdded;
            MediaPlayer.ESDeleted += MediaPlayer_ESDeleted;
            Logger.Info("Done creating mediaplayer.");
        }

        private Media CreateMedia(string url)
        {
            Logger.Info("Creating media...");
            var media = new Media(_libVLC, url, FromType.FromLocation);
            media.DurationChanged += Media_DurationChanged;
            Logger.Info("Done creating media.");

            return media;
        }

        private void StartPlayback(Media media, RendererItem renderer = null)
        {
            Logger.Info("Starting playback...");
            AudioTrackDescriptions.Clear();
            MediaPlayer.SetRenderer(renderer);
            MediaPlayer.Play(media);
            Logger.Info("Done starting playback.");
        }

        private void RemoveMediaPlayer()
        {
            Logger.Info("Removing mediaplayer...");
            MediaPlayer.Playing -= MediaPlayer_Playing;
            MediaPlayer.Paused -= MediaPlayer_Paused;
            MediaPlayer.Muted -= MediaPlayer_Muted;
            MediaPlayer.Unmuted -= MediaPlayer_Unmuted;
            MediaPlayer.TimeChanged -= MediaPlayer_TimeChanged;
            MediaPlayer.ESAdded -= MediaPlayer_ESAdded;
            MediaPlayer.ESDeleted -= MediaPlayer_ESDeleted;
            MediaPlayer.Dispose();
            Logger.Info("Done removing mediaplayer.");
        }

        private void RemoveMedia(Media media)
        {
            Logger.Info("Removing media...");

            if (media != null)
            {
                media.DurationChanged -= Media_DurationChanged;
                media.Dispose();
            }

            Logger.Info("Done removing media.");
        }

        private void StopPlayback()
        {
            Logger.Info("Stopping playback...");
            MediaPlayer.Stop();
            AudioTrackDescriptions.Clear();
            Logger.Info("Done stopping playback.");
        }

        private void CleanupProcess(Process process)
        {
            if (process != null)
            {
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill(true);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"An error occurred while trying to kill process with id '{process.Id}' and name '{process.ProcessName}'.");
                    }
                }

                process.Dispose();
            }
        }
    }
}