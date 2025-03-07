using Android.App;
using Android.Content;
using Android.Text;
using Android.Views;
using Binding;
using Google.Android.Material.Dialog;
using Google.Android.Material.TextField;
using System.Application.Models;
using System.Application.UI.Activities;
using System.Application.UI.Resx;
using System.Application.UI.ViewModels;
using System.Threading.Tasks;
using System.Windows;
using Xamarin.Essentials;
using static Xamarin.Essentials.Platform;
using AlertDialog = AndroidX.AppCompat.App.AlertDialog;

namespace System.Application.Services.Implementation
{
    internal sealed class ShowWindowServiceImpl : IShowWindowService
    {
        static string PositiveButtonText => AppResources.Confirm;
        static string NegativeButtonText => AppResources.Cancel;

        async Task<bool> PlatformShowWindow(CustomWindow customWindow, PageViewModel? viewModel = null, string title = "")
        {
            var currentActivity = await WaitForActivityAsync();
            return await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                return await PlatformShowWindowCore(currentActivity, customWindow, viewModel, title);
            });
        }

        Task<bool> PlatformShowWindowCore(Activity currentActivity, CustomWindow customWindow, PageViewModel? viewModel = null, string title = "")
        {
            switch (customWindow)
            {
                case CustomWindow.ShowAuth:
                case CustomWindow.AuthTrade:
                    if (viewModel is MyAuthenticatorWrapper viewModel_auth_w)
                    {
                        switch (customWindow)
                        {
                            case CustomWindow.ShowAuth:
                                // Android Activity 传参需要序列化后再反序列化，不能直接传递
                                // 所以此处传递 Id，在 Activity 中从关联的集合中根据 Id 取值
                                SteamAuthDataActivity.StartActivity(currentActivity, viewModel_auth_w.Authenticator.Id);
                                break;
                            case CustomWindow.AuthTrade:
                                SteamAuthTradeActivity.StartActivity(currentActivity, viewModel_auth_w.Authenticator.Id);
                                break;
                        }
                    }
                    return Task.FromResult(false);
            }

            var activityType = customWindow switch
            {
                CustomWindow.AddAuth => typeof(AddAuthActivity),
                CustomWindow.ExportAuth => typeof(ExportAuthActivity),
                CustomWindow.EncryptionAuth => typeof(EncryptionAuthActivity),
                _ => null,
            };
            if (activityType != null)
            {
                currentActivity.StartActivity(activityType);
                return Task.FromResult(false);
            }

            TaskCompletionSource<bool> tcs = new();
            try
            {
                AlertDialog? dialog = null;

                void SetConfirmButton(MaterialAlertDialogBuilder b)
                {
                    b.SetPositiveButton(PositiveButtonText, (_, e) =>
                    {
                        tcs.TrySetResult(true);
                    });
                }
                void SetCancelButton(MaterialAlertDialogBuilder b)
                {
                    b.SetNegativeButton(NegativeButtonText, (_, e) =>
                    {
                        tcs.TrySetResult(false);
                    });
                }

                Action<AlertDialog>? CreateMessageBoxDialogWindow(MaterialAlertDialogBuilder b)
                {
                    SetConfirmButton(b);
                    var isCancelcBtn = false;
                    if (viewModel is MessageBoxWindowViewModel viewModel_mb)
                    {
                        b.SetMessage(viewModel_mb.Content);
                        isCancelcBtn = viewModel_mb.IsCancelcBtn;
                    }
                    if (isCancelcBtn) SetCancelButton(b);
                    return null;
                }
                Action<AlertDialog>? CreateTextBoxDialogWindowCore(MaterialAlertDialogBuilder b, Action<textbox_password, TextBoxWindowViewModel>? action = null)
                {
                    if (viewModel is not TextBoxWindowViewModel viewModel_tb) throw new NotSupportedException();
                    // https://material.io/components/text-fields/android
                    // https://material.io/components/dialogs#specs
                    IDialogInterfaceOnClickListener? listener = null;
                    b.SetPositiveButton(PositiveButtonText, listener);
                    SetCancelButton(b);
                    var view = LayoutInflater.From(b.Context)!.Inflate(Resource.Layout.textbox_password, null, false)!;
                    var binding = new textbox_password(view);
                    var selectionIndex = 0;
                    if (!string.IsNullOrEmpty(viewModel_tb.Value))
                    {
                        binding.tbPassword.Text = viewModel_tb.Value;
                        selectionIndex = viewModel_tb.Value!.Length;
                    }
                    binding.layoutPassword.Hint = viewModel_tb.Placeholder;
                    action?.Invoke(binding, viewModel_tb);
                    b.SetView(view);
                    return d =>
                    {
                        if (binding.tbPassword.KeyListener != null)
                        {
                            currentActivity.ShowSoftInput(binding.tbPassword, inOnCreate: true);
                            if (selectionIndex > 0)
                            {
                                binding.tbPassword.SetSelection(selectionIndex);
                            }
                        }
                        d.GetButton((int)DialogButtonType.Positive).Click += (_, _) =>
                        {
                            if (viewModel is ITextBoxWindowViewModel viewModel_p)
                            {
                                viewModel_p.Value = binding.tbPassword.Text;
                                if (viewModel_p.InputValidator())
                                {
                                    d.Dismiss();
                                    tcs.TrySetResult(true);
                                }
                            }
                        };
                    };
                }
                Action<AlertDialog>? CreateTextBoxDialogWindow(MaterialAlertDialogBuilder b) => CreateTextBoxDialogWindowCore(b, static (binding, viewModel) =>
                {
                    switch (viewModel.InputType)
                    {
                        case TextBoxWindowViewModel.TextBoxInputType.TextBox:
                            binding.layoutPassword.EndIconMode = TextInputLayout.EndIconClearText;
                            binding.tbPassword.InputType = InputTypes.ClassText | InputTypes.TextVariationNormal;
                            break;
                        case TextBoxWindowViewModel.TextBoxInputType.ReadOnlyText:
                            binding.layoutPassword.EndIconMode = TextInputLayout.EndIconNone;
                            binding.tbPassword.InputType = InputTypes.ClassText | InputTypes.TextVariationNormal;
                            binding.tbPassword.SetReadOnly();
                            break;
                    }
                });

                Func<MaterialAlertDialogBuilder, Action<AlertDialog>?> @delegate = customWindow switch
                {
                    CustomWindow.MessageBox => CreateMessageBoxDialogWindow,
                    CustomWindow.TextBox => CreateTextBoxDialogWindow,
                    _ => throw new NotImplementedException(),
                };
                if (!string.IsNullOrEmpty(title) && viewModel != null)
                {
                    viewModel.Title = title;
                }
                var builder = new MaterialAlertDialogBuilder(currentActivity);
                if (!string.IsNullOrWhiteSpace(viewModel?.Title))
                {
                    builder.SetTitle(viewModel!.Title);
                }
                var dialog_delegate = @delegate(builder);
                dialog = builder.Create();
                dialog.SetCanceledOnTouchOutside(false);
                dialog.CancelEvent += (_, _) =>
                {
                    tcs.TrySetResult(false);
                };
                if (dialog_delegate != null)
                {
                    dialog.ShowEvent += (_, _) =>
                    {
                        dialog_delegate?.Invoke(dialog);
                    };
                }
                dialog.Show();
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
            return tcs.Task;
        }

        public async Task Show<TWindowViewModel>(CustomWindow customWindow, TWindowViewModel? viewModel = null, string title = "", ResizeModeCompat resizeMode = default, bool isDialog = false, bool isParent = true) where TWindowViewModel : PageViewModel, new()
        {
            await PlatformShowWindow(customWindow, viewModel, title);
        }

        public async Task Show(Type typeWindowViewModel, CustomWindow customWindow, PageViewModel? viewModel = null, string title = "", ResizeModeCompat resizeMode = default, bool isDialog = false, bool isParent = true)
        {
            await PlatformShowWindow(customWindow, viewModel, title);
        }

        public async Task<bool> ShowDialog<TWindowViewModel>(CustomWindow customWindow, TWindowViewModel? viewModel = null, string title = "", ResizeModeCompat resizeMode = default, bool isDialog = true) where TWindowViewModel : PageViewModel, new()
        {
            return await PlatformShowWindow(customWindow, viewModel, title);
        }

        public async Task ShowDialog(Type typeWindowViewModel, CustomWindow customWindow, PageViewModel? viewModel = null, string title = "", ResizeModeCompat resizeMode = default, bool isDialog = true)
        {
            await PlatformShowWindow(customWindow, viewModel, title);
        }

        public void Pop() => CurrentActivity?.OnBackPressed();
    }
}