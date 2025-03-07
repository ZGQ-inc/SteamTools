using NLog;
using System.Application.Services;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace System.Application.UI
{
    static partial class Program
    {
        static readonly HashSet<Exception> exceptions = new();
        static readonly object lock_global_ex_log = new();

#if WINDOWS_DESKTOP_BRIDGE
        //static string[] OnActivated(string[] main_args, global::Windows.ApplicationModel.Activation.IActivatedEventArgs args)
        //{
        //    if (args.Kind == global::Windows.ApplicationModel.Activation.ActivationKind.StartupTask)
        //    {
        //        // 静默启动（不弹窗口）
        //        return IDesktopPlatformService.SystemBootRunArguments.Split(' ');
        //    }
        //    return main_args;
        //}
#endif

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
#if WINDOWS_DESKTOP_BRIDGE
        [SupportedOSPlatform("Windows10.0.17763.0")]
#endif
        static int Main(string[] args)
        {
#if WINDOWS_DESKTOP_BRIDGE
            if (!DesktopBridgeHelper.Init()) return 0;
            //var activatedArgs = global::Windows.ApplicationModel.AppInstance.GetActivatedEventArgs();
            //if (activatedArgs != null) args = OnActivated(args, activatedArgs);
#elif !__MOBILE__
#if MAC
            AppDelegateHelper.Init(args);
            FileSystemDesktopMac.InitFileSystem();
#else
            FileSystemDesktop.InitFileSystem();
#endif
#endif
#if StartupTrace
            StartupTrace.Restart();
#endif
            // 目前桌面端默认使用 SystemTextJson 如果出现兼容性问题可取消下面这行代码
            // Serializable.DefaultJsonImplType = Serializable.JsonImplType.NewtonsoftJson;
            IsMainProcess = args.Length == 0;
            IsCLTProcess = !IsMainProcess && args.FirstOrDefault() == "-clt";

            AppHelper.InitLogDir();
#if StartupTrace
            StartupTrace.Restart("InitLogDir");
#endif

            void InitCefNetApp() => CefNetApp.Init(AppHelper.LogDirPath, args);
            void InitAvaloniaApp() => BuildAvaloniaAppAndStartWithClassicDesktopLifetime(args);
            void InitStartup(DILevel level) => Startup.Init(level);
            Startup.InitGlobalExceptionHandler();
            try
            {
                string[] args_clt;
                if (IsCLTProcess) // 命令行模式
                {
                    args_clt = args.Skip(1).ToArray();
                    if (args_clt.Length == 1 && args_clt[0].Equals(command_main, StringComparison.OrdinalIgnoreCase)) return default;
                }
                else
                {
                    args_clt = new[] { command_main };
                }
                return CommandLineTools.Run(args_clt, InitStartup, InitAvaloniaApp, InitCefNetApp);
            }
            catch (Exception ex)
            {
                Startup.GlobalExceptionHandler(ex, nameof(Main));
                throw;
            }
            finally
            {
                appInstance?.Dispose();
                // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
                LogManager.Shutdown();
                ArchiSteamFarm.LogManager.Shutdown();
            }
        }

        /// <summary>
        /// 是否最小化启动
        /// </summary>
        public static bool IsMinimize { get; set; }

        /// <summary>
        /// 当前是否是命令行工具进程
        /// </summary>
        public static bool IsCLTProcess { get; private set; }

        /// <summary>
        /// 当前是否是主进程
        /// </summary>
        public static bool IsMainProcess { get; private set; }
    }
}