using DynamicData;
using ReactiveUI;
using System.Application.Models;
using System.Application.Models.Settings;
using System.Application.UI;
using System.Application.UI.Resx;
using System.Application.UI.ViewModels;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Application.Services
{
    public class SteamConnectService : ReactiveObject
    {
        #region static members
        public static SteamConnectService Current { get; } = new();
        #endregion

        private readonly ISteamworksLocalApiService ApiService = ISteamworksLocalApiService.Instance;
        //private readonly ISteamworksWebApiService SteamworksWebApiService = ISteamworksWebApiService.Instance;
        //private readonly ISteamDbWebApiService steamDbApiService = ISteamDbWebApiService.Instance;
        private readonly ISteamService SteamTool = ISteamService.Instance;
        public const int SteamAFKMaxCount = 32;

        public SteamConnectService()
        {
            SteamApps = new SourceCache<SteamApp, uint>(t => t.AppId);
        }

        #region Steam游戏列表
        public SourceCache<SteamApp, uint> SteamApps { get; }
        #endregion

        #region 运行中的游戏列表
        private ConcurrentDictionary<uint, SteamApp> _RuningSteamApps = new ConcurrentDictionary<uint, SteamApp>();
        public ConcurrentDictionary<uint, SteamApp> RuningSteamApps
        {
            get => _RuningSteamApps;
            set
            {
                if (_RuningSteamApps != value)
                {
                    _RuningSteamApps = value;
                    this.RaisePropertyChanged();
                }
            }
        }
        #endregion

        #region 当前steam登录用户
        private SteamUser? _CurrentSteamUser;
        public SteamUser? CurrentSteamUser
        {
            get => _CurrentSteamUser;
            set
            {
                if (_CurrentSteamUser != value)
                {
                    _CurrentSteamUser = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        const string DefaultAvaterPath = "avares://System.Application.SteamTools.Client.Desktop.Avalonia/Application/UI/Assets/AppResources/avater.jpg";
        object? _AvaterPath = DefaultAvaterPath;
        public object? AvaterPath
        {
            get => _AvaterPath;
            set => this.RaiseAndSetIfChanged(ref _AvaterPath, value);
        }
        #endregion

        #region 连接steamclient是否成功
        private bool _IsConnectToSteam;
        public bool IsConnectToSteam
        {
            get => _IsConnectToSteam;
            set
            {
                if (_IsConnectToSteam != value)
                {
                    _IsConnectToSteam = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private bool _IsSteamChinaLauncher;
        public bool IsSteamChinaLauncher
        {
            get => _IsSteamChinaLauncher;
            set
            {
                if (_IsSteamChinaLauncher != value)
                {
                    _IsSteamChinaLauncher = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private bool _IsDisposedClient = true;
        /// <summary>
        /// 是否已经释放SteamClient
        /// </summary>
        public bool IsDisposedClient
        {
            get => _IsDisposedClient;
            set
            {
                if (_IsDisposedClient != value)
                {
                    _IsDisposedClient = value;
                    this.RaisePropertyChanged();
                }
            }
        }
        #endregion

        private bool _IsLoadingGameList = true;
        public bool IsLoadingGameList
        {
            get => _IsLoadingGameList;
            set
            {
                if (_IsLoadingGameList != value)
                {
                    _IsLoadingGameList = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public void RunAFKApps()
        {
            if (GameLibrarySettings.AFKAppList.Value?.Count > 0)
            {
                foreach (var item in GameLibrarySettings.AFKAppList.Value)
                {
                    RuningSteamApps.TryGetValue(item.Key, out var runState);
                    if (runState == null)
                        RuningSteamApps.TryAdd(item.Key, new SteamApp
                        {
                            AppId = item.Key,
                            Name = item.Value
                        });
                }
                var t = new Task(() =>
                {
                    foreach (var item in RuningSteamApps.Values)
                    {
                        if (item.Process == null)
                            item.StartSteamAppProcess();
                    }
                });
                t.Start();
            }
        }

        public void Initialize()
        {
            if (!SteamTool.IsRunningSteamProcess && SteamSettings.IsAutoRunSteam.Value)
                SteamTool.StartSteam(SteamSettings.SteamStratParameter.Value);

            Task.Factory.StartNew(async () =>
            {
                Thread.CurrentThread.IsBackground = true;
                try
                {
                    while (true)
                    {
                        if (SteamTool.IsRunningSteamProcess)
                        {
                            if (!IsConnectToSteam && IsDisposedClient)
                            {
                                IsDisposedClient = false;
                                if (ApiService.Initialize())
                                {
                                    var id = ApiService.GetSteamId64();
                                    if (id == SteamUser.UndefinedId)
                                    {
                                        //该64位id的steamID3等于0，是steam未获取到当前登录用户的默认返回值，所以直接重新获取
                                        Current.DisposeSteamClient();
                                        continue;
                                    }
                                    IsConnectToSteam = true;
                                    CurrentSteamUser = await DI.Get<ISteamworksWebApiService>().GetUserInfo(id);
                                    CurrentSteamUser.AvatarStream = IHttpService.Instance.GetImageAsync(CurrentSteamUser.AvatarFull, ImageChannelType.SteamAvatars);
                                    AvaterPath = CircleImageStream.Convert(await CurrentSteamUser.AvatarStream);

                                    CurrentSteamUser.IPCountry = ApiService.GetIPCountry();
                                    IsSteamChinaLauncher = ApiService.IsSteamChinaLauncher();

                                    #region 初始化需要steam启动才能使用的功能
                                    if (SteamSettings.IsEnableSteamLaunchNotification.Value)
                                    {
                                        INotificationService.Instance.Notify($"{AppResources.Steam_CheckStarted}{(IsSteamChinaLauncher ? AppResources.Steam_SteamChina : AppResources.Steam_SteamWorld)}{Environment.NewLine}{AppResources.Steam_CurrentUser}{CurrentSteamUser.SteamNickName}{Environment.NewLine}{AppResources.Steam_CurrentIPCountry}{CurrentSteamUser.IPCountry}", NotificationType.Announcement);
                                    }


                                    if (GameLibrarySettings.IsAutoAFKApps.Value)
                                    {
                                        RunAFKApps();
                                    }

                                    //仅在有游戏数据情况下加载登录用户的游戏
                                    if (SteamApps.Items.Any())
                                    {
                                        LoadGames(ApiService.OwnsApps(await ISteamService.Instance.GetAppInfos()));
                                    }
                                    //var mainViewModel = (IWindowService.Instance.MainWindow as WindowViewModel);
                                    //await mainViewModel.SteamAppPage.Initialize();
                                    //await mainViewModel.AccountPage.Initialize(id);
                                    #endregion

                                    DisposeSteamClient();
                                }
                            }
                        }
                        else
                        {
                            IsConnectToSteam = false;
                            CurrentSteamUser = null;
                            AvaterPath = DefaultAvaterPath;
                        }
                        Thread.Sleep(2000);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(nameof(SteamConnectService), ex, "SteamConnect Task LongRunning");
                    ToastService.Current.Notify(ex.Message);
                }
            }, TaskCreationOptions.LongRunning).ConfigureAwait(false);
        }

        public bool Initialize(int appid)
        {
            if (SteamTool.IsRunningSteamProcess)
            {
                return IsConnectToSteam = ApiService.Initialize(appid);
            }
            return false;
        }

        private void LoadGames(IEnumerable<SteamApp>? apps)
        {
            SteamApps.Clear();
            if (apps.Any_Nullable())
                SteamApps.AddOrUpdate(apps!);
        }

        public async void InitializeGameList()
        {
            IsLoadingGameList = true;
            LoadGames(await ISteamService.Instance.GetAppInfos());
            //UpdateGamesImage();
            IsLoadingGameList = false;
        }

        public async void InitializeDownloadGameList()
        {

        }

        private bool _IsRefreshing;
        public /*async*/ void RefreshGamesList()
        {
            if (_IsRefreshing == false)
            {
                _IsRefreshing = true;
                if (SteamTool.IsRunningSteamProcess)
                {
                    Task.Factory.StartNew(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        try
                        {
                            while (true)
                            {
                                if (SteamTool.IsRunningSteamProcess && IsDisposedClient)
                                {
                                    IsDisposedClient = false;
                                    if (ApiService.Initialize())
                                    {
                                        SteamApps.Clear();
                                        InitializeGameList();
                                        if (SteamApps.Items.Any())
                                        {
                                            LoadGames(ApiService.OwnsApps(SteamApps.Items));
                                            Toast.Show(AppResources.GameList_RefreshGamesListSucess);
                                            DisposeSteamClient();
                                            _IsRefreshing = false;
                                            return;
                                        }
                                    }
                                }
                                Thread.Sleep(2000);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(nameof(SteamConnectService), ex, "Task RefreshGamesList");
                            ToastService.Current.Notify(ex.Message);
                        }
                    }, TaskCreationOptions.DenyChildAttach | TaskCreationOptions.PreferFairness).Forget().ConfigureAwait(false);
                }
                else
                {
                    InitializeGameList();
                    Toast.Show(AppResources.GameList_RefreshGamesListSucess);
                    _IsRefreshing = false;
                }
            }
        }

        public void Dispose()
        {
            foreach (var app in Current.RuningSteamApps.Values)
            {
                if (app.Process != null)
                    if (!app.Process.HasExited)
                        app.Process.Kill();
            }
            DisposeSteamClient();
        }

        public void DisposeSteamClient()
        {
            ApiService.DisposeSteamClient();
            IsDisposedClient = true;
        }
    }
}
