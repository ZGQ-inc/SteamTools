using System.Application.Models;
using System.Application.UI.Resx;
using System.Windows;
using APIConst = System.Application.Services.CloudService.Constants;

namespace System.Application.Security
{
    public static class IsNotOfficialChannelPackageDetectionHelper
    {
        /// <summary>
        /// <see cref="AppSettings.IsOfficialChannelPackage"/>
        /// </summary>
        /// <param name="showMessageBox"></param>
        /// <returns></returns>
        public static bool Check(bool showMessageBox = true)
        {
            var value = AppSettings.IsOfficialChannelPackage;
            if (!value)
            {
                static void IsNotOfficialChannelPackageWarning()
                {
                    var text = APIConst.IsNotOfficialChannelPackageWarning;
                    var title = AppResources.Warning;
                    MessageBoxCompat.Show(text, title, MessageBoxButtonCompat.OK, MessageBoxImageCompat.Warning);
                }
                if (showMessageBox) IsNotOfficialChannelPackageWarning();
            }
            return value;
        }
    }
}