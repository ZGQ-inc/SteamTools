using DynamicData;
using ReactiveUI;
using System.Application.Models;
using System.Application.Repositories;
using System.Application.Services;
using System.Application.UI.Resx;
using System.Collections.ObjectModel;
using System.Linq;
using System.Properties;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WinAuth;

namespace System.Application.UI.ViewModels
{
    public partial class AuthTradeWindowViewModel : IExplicitHasValue
    {
        private readonly MyAuthenticator? MyAuthenticator;
        private readonly GAPAuthenticatorValueDTO.SteamAuthenticator? _Authenticator;

        public static string DisplayName => AppResources.LocalAuth_SteamAuthTrade;

        public AuthTradeWindowViewModel() : base()
        {
            Title =
#if !__MOBILE__
                ThisAssembly.AssemblyTrademark + " | " +
#endif
                DisplayName;
            _ConfirmationsSourceList = new SourceList<WinAuthSteamClient.Confirmation>();

            _ConfirmationsSourceList
               .Connect()
               .ObserveOn(RxApp.MainThreadScheduler)
               //.Sort(SortExpressionComparer<WinAuthSteamClient.Confirmation>.Descending(x => x.))
               .Bind(out _Confirmations)
               .Subscribe(_ =>
               {
                   this.RaisePropertyChanged(nameof(IsConfirmationsEmpty));
                   this.RaisePropertyChanged(nameof(ConfirmationsConutMessage));
               });

#if __MOBILE__
            RegisterSelectAllObservable();
#endif

            Initialize();
        }

        public AuthTradeWindowViewModel(MyAuthenticator? auth) : this()
        {
            MyAuthenticator = auth;
            if (MyAuthenticator?.AuthenticatorData.Value is GAPAuthenticatorValueDTO.SteamAuthenticator authenticator)
            {
                _Authenticator = authenticator;
                UserName = _Authenticator.AccountName;
            }
            else
            {
                //非Steam令牌无法弹出确认交易框
                throw new NotSupportedException("Authenticator is not SteamAuthenticator");
            }

            Refresh_Click();
        }

        private string? AuthPassword;
        private bool AuthIsLocal;

        private async void Initialize()
        {
            var repository = DI.Get<IGameAccountPlatformAuthenticatorRepository>();
            var (success, password) = await AuthService.Current.HasPasswordEncryptionShowPassWordWindow();
            if (success)
            {
                AuthPassword = password;
            }
            else
            {
                AuthPassword = null;
            }
            var auths = await repository.GetAllSourceAsync();
            AuthIsLocal = repository.HasLocal(auths);
        }

        #region LoginData
        private string? _UserName;
        public string? UserName
        {
            get => _UserName;
            set
            {
                if (_UserName != value)
                {
                    _UserName = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private string? _Password;
        public string? Password
        {
            get => _Password;
            set
            {
                if (_Password != value)
                {
                    _Password = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private bool _RememberMe;
        public bool RememberMe
        {
            get => _RememberMe;
            set
            {
                if (_RememberMe != value)
                {
                    _RememberMe = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private string? _CodeImage;
        public string? CodeImage
        {
            get => _CodeImage;
            set
            {
                if (_CodeImage != value)
                {
                    _CodeImage = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private string? _CodeImageChar;
        public string? CodeImageChar
        {
            get => _CodeImageChar;
            set
            {
                if (_CodeImageChar != value)
                {
                    _CodeImageChar = value;
                    this.RaisePropertyChanged();
                }
            }
        }
        #endregion

        public bool IsLoggedIn
        {
            get => _Authenticator?.GetClient().IsLoggedIn() ?? false;
            set
            {
                this.RaisePropertyChanged();
            }
        }
        public bool IsRequiresCaptcha
        {
            get => _Authenticator!.GetClient().RequiresCaptcha;
            set
            {
                this.RaisePropertyChanged();
            }
        }

        /// <summary>
        /// Cancellation token for confirm all
        /// </summary>
        private CancellationTokenSource? CancelComfirmAll;

        /// <summary>
        /// Cancellation token for cancel all
        /// </summary>
        private CancellationTokenSource? CancelCancelAll;

        private readonly ReadOnlyObservableCollection<WinAuthSteamClient.Confirmation>? _Confirmations;
        public ReadOnlyObservableCollection<WinAuthSteamClient.Confirmation> Confirmations => _Confirmations ?? throw new ArgumentNullException(nameof(_Confirmations));

        private readonly SourceList<WinAuthSteamClient.Confirmation> _ConfirmationsSourceList;
        public SourceList<WinAuthSteamClient.Confirmation> ConfirmationsSourceList => _ConfirmationsSourceList;

        private bool _IsLoading;
        public bool IsLoading
        {
            get => _IsLoading;
            set
            {
                if (_IsLoading != value)
                {
                    _IsLoading = value;
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(ConfirmationsConutMessage));
                }
            }
        }

        public bool IsConfirmationsEmpty
        {
            get => _ConfirmationsSourceList.Items.Any_Nullable();
        }

        public string ConfirmationsConutMessage
        {
            get
            {
                if (IsLoading)
                {
                    return string.Empty;
                }
                if (!IsConfirmationsEmpty)
                {
                    return AppResources.LocalAuth_AuthTrade_ListNullTip;
                }
                return AppResources.LocalAuth_AuthTrade_ListCountTip.Format(_ConfirmationsSourceList.Count);
            }
        }

        public void LoginButton_Click()
        {
            if (!string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password))
            {
                if (!string.IsNullOrWhiteSpace(CodeImageChar))
                {
                    Process(_Authenticator!.GetClient().CaptchaId, CodeImageChar);
                }
                else
                {
                    Process();
                }
            }
            else
            {
                Toast.Show(AppResources.User_LoginError_Null);
                return;
            }
        }

        public void Refresh_Click()
        {
            if (IsLoggedIn)
            {
                Process();
            }
        }

        private void RefreshConfirmationsList()
        {
            var items = _ConfirmationsSourceList.Items.Where(s => s.IsOperate == 0);
            _ConfirmationsSourceList.Clear();
            if (items.Any())
                _ConfirmationsSourceList.AddRange(items);
        }

        public async void Logout_Click()
        {
            var r = await MessageBoxCompat.ShowAsync(AppResources.LocalAuth_LogoutTip, ThisAssembly.AssemblyTrademark, MessageBoxButtonCompat.OKCancel);
            if (r == MessageBoxResultCompat.OK)
            {
                var steam = _Authenticator!.GetClient();
                steam.Logout();

                if (string.IsNullOrEmpty(_Authenticator.SessionData) == false)
                {
                    IsLoggedIn = false;
                    _Authenticator.SessionData = null;
                    AuthService.AddOrUpdateSaveAuthenticators(MyAuthenticator!, AuthIsLocal, AuthPassword);
                }
            }
        }

        private void Process(string? captchaId = null, string? codeChar = null)
        {
            if (IsLoading || _Authenticator == null)
                return;
            Task.Run(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                IsLoading = true;
                var steam = _Authenticator.GetClient();
                if (!IsLoggedIn)
                {
#if !__MOBILE__
                    IsLoading = false;
                    ToastService.Current.Set(AppResources.Logining);
#else
                    LoadingText = AppResources.Logining;
#endif
                    //Toast.Show(AppResources.Logining);
                    var loginResult = steam.Login(UserName!, Password!, captchaId, codeChar, R.GetCurrentCultureSteamLanguageName());
#if __MOBILE__
                    LoadingText = null;
#endif
                    if (!loginResult)
                    {
                        if (steam.Error == "Incorrect Login")
                        {
                            Toast.Show(AppResources.User_LoginError);
                            return;
                        }

                        if (steam.Requires2FA == true)
                        {
                            Toast.Show(AppResources.User_LoginError_Auth);
                            return;
                        }

                        if (steam.RequiresCaptcha == true)
                        {
                            IsRequiresCaptcha = steam.RequiresCaptcha;
                            Toast.Show(AppResources.User_LoginError_CodeImage);
                            CodeImage = steam.CaptchaUrl;
                            return;
                        }
                        //loginButton.Enabled = true;
                        //captchaGroup.Visible = false;

                        if (string.IsNullOrEmpty(steam.Error) == false)
                        {
                            Toast.Show(steam.Error);
                            return;
                        }
                        return;
                    }
                    Toast.Show(AppResources.User_LoiginSuccess);
                    IsLoggedIn = true;
                    _Authenticator.SessionData = RememberMe ? steam.Session.ToString() : null;
                    AuthService.AddOrUpdateSaveAuthenticators(MyAuthenticator!, AuthIsLocal, AuthPassword);
                }
                try
                {
                    IsLoading = true;
#if __MOBILE__
                    LoadingText = AppResources.LocalAuth_AuthTrade_GetTip;
#endif
                    //Toast.Show(AppResources.LocalAuth_AuthTrade_GetTip);
                    var list = steam.GetConfirmations();

#if !__MOBILE__
                    Parallel.ForEach(list, confirmation =>
                    {
                        confirmation.ImageStream = IHttpService.Instance.GetImageAsync(confirmation.Image, ImageChannelType.SteamEconomys);
                    });
#endif

                    _ConfirmationsSourceList.Clear();
                    _ConfirmationsSourceList.AddRange(list);

                    // 获取新交易后保存
                    if (!string.IsNullOrEmpty(_Authenticator.SessionData))
                    {
                        AuthService.AddOrUpdateSaveAuthenticators(MyAuthenticator!, AuthIsLocal, AuthPassword);
                    }
                    IsLoading = false;
                }
                catch (WinAuthUnauthorisedSteamRequestException)
                {
                    // Family view is probably on
                    Toast.Show(AppResources.LocalAuth_AuthTrade_GetError);
                    IsLoading = false;
                    return;
                }
                catch (WinAuthInvalidSteamRequestException)
                {
                    // likely a bad session so try a refresh first
                    try
                    {
                        steam.Refresh();
                        var list = steam.GetConfirmations();

#if !__MOBILE__
                        Parallel.ForEach(list, confirmation =>
                        {
                            confirmation.ImageStream = IHttpService.Instance.GetImageAsync(confirmation.Image, ImageChannelType.SteamEconomys);
                        });
#endif

                        _ConfirmationsSourceList.Clear();
                        _ConfirmationsSourceList.AddRange(list);

                        IsLoading = false;
                    }
                    catch (Exception ex)
                    {
                        // reset and show normal login
                        Log.Error(nameof(Process), ex, "可能是没有开加速器导致无法连接Steam社区登录地址");
                        Toast.Show(AppResources.LocalAuth_AuthTrade_GetError2);
                        IsLoading = false;
                        //steam.Clear();
                        return;
                    }
                }
            }).ForgetAndDispose();
        }

        public void ConfirmTrade_Click(WinAuthSteamClient.Confirmation trade)
        {
            OperationTrade(true, trade);
        }

        public void CancelTrade_Click(WinAuthSteamClient.Confirmation trade)
        {
            OperationTrade(false, trade);
        }

        private void OperationTrade(bool accept, WinAuthSteamClient.Confirmation trade)
        {
            Task.Run(async () =>
            {
                bool result = false;
                if (accept)
                    result = await AcceptTrade(trade);
                else
                    result = await RejectTrade(trade);

                if (result)
                {
                    Toast.Show($"{(accept ? AppResources.Agree : AppResources.Cancel)}{trade.Details}");
                    MainThread2.BeginInvokeOnMainThread(() =>
                    {
                        trade.IsOperate = accept ? 1 : 2;
                        RefreshConfirmationsList();
                        //Confirmations.Remove(trade);
                    });
                    //Refresh_Click();
                }
            }).ForgetAndDispose();
        }

        /// <summary>
        /// Accept the trade Confirmation
        /// </summary>
        /// <param name="tradeId">Id of Confirmation</param>
        private async Task<bool> AcceptTrade(WinAuthSteamClient.Confirmation trade)
        {
            try
            {
                if (trade == null)
                {
                    //throw new ApplicationException(AppResources.LocalAuth_AuthTrade_TradeError);
                    Toast.Show(AppResources.LocalAuth_AuthTrade_TradeError);
                    return false;
                }

                var result = await Task.Run<bool>(() =>
                {
                    return _Authenticator!.GetClient().ConfirmTrade(trade.Id, trade.Key, true);
                });
                if (result == false)
                {
                    //throw new ApplicationException(AppResources.LocalAuth_AuthTrade_ConfirmError);
                    Toast.Show(AppResources.LocalAuth_AuthTrade_ConfirmError);
                    return false;
                }

                return true;
            }
            catch (WinAuthInvalidTradesResponseException ex)
            {
                Log.Error(nameof(RejectTrade), ex, nameof(AuthTradeWindowViewModel));
                Toast.Show(ex.Message);
                return false;
            }
            catch (ApplicationException ex)
            {
                Log.Error(nameof(RejectTrade), ex, nameof(AuthTradeWindowViewModel));
                Toast.Show(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Reject the trade Confirmation
        /// </summary>
        /// <param name="tradeId">ID of Confirmation</param>
        private async Task<bool> RejectTrade(WinAuthSteamClient.Confirmation trade)
        {
            try
            {
                if (trade == null)
                {
                    //throw new ApplicationException(AppResources.LocalAuth_AuthTrade_TradeError);
                    Toast.Show(AppResources.LocalAuth_AuthTrade_TradeError);
                    return false;
                }
                var result = await Task.Run<bool>(() =>
                {
                    return _Authenticator!.GetClient().ConfirmTrade(trade.Id, trade.Key, false);
                });
                if (result == false)
                {
                    //throw new ApplicationException(AppResources.LocalAuth_AuthTrade_CancelError);
                    Toast.Show(AppResources.LocalAuth_AuthTrade_CancelError);
                    return false;
                }

                return true;
            }
            catch (WinAuthInvalidTradesResponseException ex)
            {
                Log.Error(nameof(RejectTrade), ex, nameof(AuthTradeWindowViewModel));
                Toast.Show(ex.Message);
                return false;
            }
            catch (ApplicationException ex)
            {
                Log.Error(nameof(RejectTrade), ex, nameof(AuthTradeWindowViewModel));
                Toast.Show(ex.Message);
                return false;
            }
        }

        public void ConfirmAllButton_Click()
        {
            OperationTrades(true);
        }
        public void CancelAllButton_Click()
        {
            OperationTrades(false);
        }

        private async void OperationTrades(bool accept)
        {
            if (!_ConfirmationsSourceList.Items.Any_Nullable())
            {
                Toast.Show(AppResources.LocalAuth_AuthTrade_Null);
                return;
            }

            if (CancelComfirmAll != null)
            {
                return;
            }

            if (CancelCancelAll != null)
            {
                return;
            }

            var str = (accept ? AppResources.Agree : AppResources.Cancel);

            var result = await MessageBoxCompat.ShowAsync(AppResources.LocalAuth_AuthTrade_MessageBoxTip.Format(str), ThisAssembly.AssemblyTrademark, MessageBoxButtonCompat.OKCancel);

            if (result == MessageBoxResultCompat.OK)
            {
                var text = AppResources.LocalAuth_AuthTrade_ConfirmTip.Format(str);
#if __MOBILE__
                LoadingText = text;
#else
                ToastService.Current.Set(text);
#endif

                if (accept)
                    AcceptAllTrade();
                else
                    RejectAllTrade();
            }
        }

        private async void AcceptAllTrade()
        {
            if (CancelComfirmAll != null)
            {
                CancelComfirmAll.Cancel();
                return;
            }

            CancelComfirmAll = new CancellationTokenSource();

            try
            {
                foreach (var item in _ConfirmationsSourceList.Items)
                {
                    if (CancelComfirmAll.IsCancellationRequested)
                    {
                        return;
                    }

                    if (item.IsOperate != 0 || item.NotChecked)
                    {
                        continue;
                    }

                    DateTime start = DateTime.Now;

                    var result = await AcceptTrade(item);
                    if (result == false || CancelComfirmAll.IsCancellationRequested == true)
                    {
                        return;
                    }
                    await MainThread2.InvokeOnMainThreadAsync(() =>
                    {
                        item.IsOperate = 1;
                        //Confirmations.Remove(trades[i]);
                    });

                    var duration = (int)DateTime.Now.Subtract(start).TotalMilliseconds;
                    var delay = WinAuthSteamClient.CONFIRMATION_EVENT_DELAY + Random2.Next(WinAuthSteamClient.CONFIRMATION_EVENT_DELAY / 2); // delay is 100%-150% of CONFIRMATION_EVENT_DELAY
                    if (delay > duration)
                    {
                        await Task.Delay(delay - duration);
                    }
                }
            }
            finally
            {
                CancelComfirmAll = null;
                RefreshConfirmationsList();
#if __MOBILE__
                LoadingText = null;
#endif
                Toast.Show(AppResources.LocalAuth_AuthTrade_ConfirmSuccess);
                AuthService.AddOrUpdateSaveAuthenticators(MyAuthenticator!, AuthIsLocal, AuthPassword);
            }
        }

        private async void RejectAllTrade()
        {
            if (CancelCancelAll != null)
            {
                CancelCancelAll.Cancel();
                return;
            }

            CancelCancelAll = new CancellationTokenSource();

            try
            {
                foreach (var item in _ConfirmationsSourceList.Items)
                {
                    if (CancelCancelAll.IsCancellationRequested)
                    {
                        break;
                    }

                    if (item.IsOperate != 0 || item.NotChecked)
                    {
                        continue;
                    }

                    DateTime start = DateTime.Now;

                    var result = await RejectTrade(item);
                    if (result == false || CancelCancelAll.IsCancellationRequested == true)
                    {
                        break;
                    }
                    await MainThread2.InvokeOnMainThreadAsync(() =>
                    {
                        item.IsOperate = 2;
                        //Confirmations.Remove(tradeIds[i]);
                    });

                    var duration = (int)DateTime.Now.Subtract(start).TotalMilliseconds;
                    var delay = WinAuthSteamClient.CONFIRMATION_EVENT_DELAY + Random2.Next(WinAuthSteamClient.CONFIRMATION_EVENT_DELAY / 2); // delay is 100%-150% of CONFIRMATION_EVENT_DELAY
                    if (delay > duration)
                    {
                        await Task.Delay(delay - duration);
                    }
                }
            }
            finally
            {
                CancelCancelAll = null;
                RefreshConfirmationsList();
#if __MOBILE__
                LoadingText = null;
#endif
                Toast.Show(AppResources.LocalAuth_AuthTrade_ConfirmCancel);
                AuthService.AddOrUpdateSaveAuthenticators(MyAuthenticator!, AuthIsLocal, AuthPassword);
            }
        }

        bool IExplicitHasValue.ExplicitHasValue()
        {
            return MyAuthenticator != null && _Authenticator != null;
        }
    }
}