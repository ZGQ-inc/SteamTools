using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Xamarin.Essentials;
using static System.Application.Services.IFilePickerPlatformService;
using static System.Application.Services.IFilePickerPlatformService.IServiceBase;

namespace System.Application.Services.Implementation
{
    partial class WindowsDesktopPlatformServiceImpl : IOpenFileDialogService
    {
        // https://github.com/xamarin/Essentials/blob/main/Xamarin.Essentials/FilePicker/FilePicker.uwp.cs

        [SupportedOSPlatform("Windows10.0.10240.0")]
        public async Task<IEnumerable<FileResult>> PlatformPickAsync(PickOptions? options, bool allowMultiple = false)
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };

            SetFileTypes(options, picker);

            var resultList = new List<StorageFile>();

            if (allowMultiple)
            {
                var fileList = await picker.PickMultipleFilesAsync();
                if (fileList != null)
                    resultList.AddRange(fileList);
            }
            else
            {
                var file = await picker.PickSingleFileAsync();
                if (file != null)
                    resultList.Add(file);
            }

            foreach (var file in resultList)
                StorageApplicationPermissions.FutureAccessList.Add(file);

            return resultList.Select(storageFile => new FileResult(storageFile.Path));
        }

        [SupportedOSPlatform("Windows10.0.10240.0")]
        static void SetFileTypes(PickOptions? options, FileOpenPicker picker)
        {
            FormatExtensions(options?.FileTypes?.Value, picker.FileTypeFilter.Add);
        }
    }
}