using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Util;
using AndroidX.Core.Net;
using Binding;
using System.Application.Models;
using System.Application.UI.ViewModels;
using System.IO;
using static Android.Content.Intent;

namespace System.Application.UI.Activities
{
    [Register(JavaPackageConstants.Activities + nameof(TextBlockActivity))]
    [Activity(Theme = ManifestConstants.MainTheme_NoActionBar,
      LaunchMode = LaunchMode.SingleTask,
      ConfigurationChanges = ManifestConstants.ConfigurationChanges)]
    [IntentFilter(new[] { ActionView }, Categories = new[] { CategoryDefault }, DataMimeType = MediaTypeNames.TXT)]
    internal sealed class TextBlockActivity : BaseActivity<activity_textblock>
    {
        protected override int? LayoutResource => Resource.Layout.activity_textblock;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            var vm = this.GetViewModel<TextBlockViewModel>();
            if (vm == null)
            {
                if (Intent?.Action == ActionView && Intent.Data != null)
                {
                    try
                    {
                        var filePath = Intent.Data.EnsurePhysicalPath();
                        var fileContent = File.ReadAllText(filePath);
                        vm = new TextBlockViewModel
                        {
                            Title = Path.GetFileName(filePath),
                            Content = fileContent,
                        };
                    }
                    catch (Exception e)
                    {
                        Log.Error(nameof(TextBlockActivity), nameof(OnCreate), e);
                    }
                }
            }

            if (vm == null)
            {
                Finish();
                return;
            }

            this.SetSupportActionBarWithNavigationClick(binding!.toolbar, true);

            if (!string.IsNullOrWhiteSpace(vm.Title)) Title = vm.Title;

            if (!string.IsNullOrWhiteSpace(vm.Content)) binding!.tvContent.Text = vm.Content;
            else if (vm.ContentSource != default) binding!.tvContent.Text = vm.ContentSource switch
            {
                TextBlockViewModel.ContentSourceEnum.OpenSourceLibrary => OpenSourceLibrary.StringValues,
                TextBlockViewModel.ContentSourceEnum.Translators => AboutPageViewModel.TextTranslators,
                _ => string.Empty,
            };

            if (vm.FontSizeResId.HasValue)
            {
                binding!.tvContent.SetTextSize(ComplexUnitType.Px, Resources!.GetDimension(vm.FontSizeResId.Value));
            }
        }

        public static void StartActivity(Activity activity, TextBlockViewModel viewModel)
        {
            GoToPlatformPages.StartActivity<TextBlockActivity, TextBlockViewModel>(activity, viewModel);
        }
    }
}