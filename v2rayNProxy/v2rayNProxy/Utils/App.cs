using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using MonoMod.RuntimeDetour;
using Sys.Utils;
using Sys.Utils.Reflection;
using Sys.WinForms.UI;
using Sys.WinForms.UI.MessageBoxPositionManager;

namespace v2rayNProxy.Utils
{
    public sealed class App
    {
        private static readonly string _host = "127.0.0.1";
        private static readonly string _msgTitle = nameof(v2rayNProxy);
        private static readonly string _appDir = "app";
        private static readonly string _appName = "v2rayN";
        private static readonly string _appExeName = $"{App._appName}.exe";
        private static readonly string _appUpExeName = "v2rayUpgrade.exe";
        private static readonly string _appUpProxyExeName = "v2rayUpgradeProxy.exe";
        private static App _app;
        private static Mutex _mutexObj;
        private static List<Lazy<PluginBase>> _plugins;

        private App()
        {

        }

        public static App Instance => App._app ?? (App._app = new App());

        public void Start(string[] args)
        {
            if (App.IsAppRuning())
            {
                MSGBox.OK($"{nameof(v2rayNProxy)} 已经运行！", App._msgTitle);
                return;
            }

            var dir = Path.Combine(AppContext.BaseDirectory, App._appDir);

            if (!Directory.Exists(dir))
            {
                MSGBox.OK($"当前目录下未找到文件夹 {App._appDir} ！", App._msgTitle);
                return;
            }

            var path = Path.Combine(dir, App._appExeName);

            if (!File.Exists(path))
            {
                MSGBox.OK($"在当前目录的 {App._appDir} 文件夹下未找到程序文件 {App._appExeName} ！", App._msgTitle);
                return;
            }

            try
            {
                var domain = Domain.Create(nameof(App), new AppDomainSetup()
                {
                    PrivateBinPath = dir
                });

                using (domain)
                {
                    domain.Invoke(ctx =>
                    {
                        var d = ctx.Args[0] as string;

                        var p = ctx.Args[1] as string;

                        ctx.CurrentDomain.AppDomain.SetData("APPBASE", d);

                        var assembly = Assembly.LoadFile(p);

                        if (assembly.EntryPoint == null)
                        {
                            return;
                        }

                        var hooksDisp = App.Create_Hooks(assembly);

                        var disp = MessageBoxCenteringService.Initialize();

                        using (Disposable.Create(hooksDisp, disp))
                        {
                            assembly.EntryPoint.Invoke(null, new object[0]);
                        }
                    },
                    dir,
                    path);
                }
            }
            catch (Exception e)
            {
                MSGBox.OK($"错误：{(e.InnerException ?? e).Message}", App._msgTitle);
            }
        }

        private static IDisposable Create_Hooks(Assembly assembly)
        {
            return Disposable.Create(
                Get_Process_SetStartInfo_Hook(),
                Get_Program_Application_ThreadException_Hook(),
                Get_Program_CurrentDomain_UnhandledException_Hook(),
                Get_Utils_StartupPath_Hook(),
                Get_Utils_GetExePath_Hook(),
                Get_Utils_GetVersion_Hook(),
                Get_SysProxyHandle_UpdateSysProxy_Hook());

            Hook Get_Process_SetStartInfo_Hook()
            {
                var type = typeof(Process);

                var method = type.GetProperty(nameof(Process.StartInfo)).GetSetMethod();

                var methodNew = typeof(App).GetMethod(nameof(New_SetStartInfo), BindingFlags.NonPublic | BindingFlags.Static);

                return new Hook(method, methodNew);
            }

            Hook Get_Program_Application_ThreadException_Hook()
            {
                var type = assembly.GetType("v2rayN.Program");

                var method = type.GetMethod("Application_ThreadException", BindingFlags.NonPublic | BindingFlags.Static);

                var methodNew = typeof(App).GetMethod(nameof(New_Application_ThreadException), BindingFlags.NonPublic | BindingFlags.Static);

                return new Hook(method, methodNew);
            }

            Hook Get_Program_CurrentDomain_UnhandledException_Hook()
            {
                var type = assembly.GetType("v2rayN.Program");

                var method = type.GetMethod("CurrentDomain_UnhandledException", BindingFlags.NonPublic | BindingFlags.Static);

                var methodNew = typeof(App).GetMethod(nameof(New_CurrentDomain_UnhandledException), BindingFlags.NonPublic | BindingFlags.Static);

                return new Hook(method, methodNew);
            }

            Hook Get_Utils_StartupPath_Hook()
            {
                var type = assembly.GetType("v2rayN.Utils");

                var method = type.GetMethod("StartupPath", BindingFlags.Public | BindingFlags.Static);

                var methodNew = typeof(App).GetMethod(nameof(New_StartupPath), BindingFlags.NonPublic | BindingFlags.Static);

                return new Hook(method, methodNew);
            }

            Hook Get_Utils_GetExePath_Hook()
            {
                var type = assembly.GetType("v2rayN.Utils");

                var method = type.GetMethod("GetExePath", BindingFlags.Public | BindingFlags.Static);

                var methodNew = typeof(App).GetMethod(nameof(New_GetExePath), BindingFlags.NonPublic | BindingFlags.Static);

                return new Hook(method, methodNew);
            }

            Hook Get_Utils_GetVersion_Hook()
            {
                var type = assembly.GetType("v2rayN.Utils");

                var method = type.GetMethod("GetVersion", BindingFlags.Public | BindingFlags.Static);

                var methodNew = typeof(App).GetMethod(nameof(New_GetVersion), BindingFlags.NonPublic | BindingFlags.Static);

                return new Hook(method, methodNew);
            }

            Hook Get_SysProxyHandle_UpdateSysProxy_Hook()
            {
                var type = assembly.GetType("v2rayN.Handler.SysProxyHandle");

                var method = type.GetMethod("UpdateSysProxy");

                var methodNew = typeof(App).GetMethod(nameof(New_UpdateSysProxy), BindingFlags.NonPublic | BindingFlags.Static);

                return new Hook(method, methodNew);
            }
        }

        private static bool IsAppRuning()
        {
            var exePath = Application.ExecutablePath.Replace("\\", "/");
            App._mutexObj = new Mutex(initiallyOwned: false, typeof(App).FullName + "-" + exePath, out var createdNew);
            return !createdNew;
        }

        private static void Exe_Cmd(string exe, string args = null)
        {
            var processInfo = new ProcessStartInfo()
            {
                FileName = exe,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false,
                Arguments = args
            };

            using (var process = Process.Start(processInfo))
            {
                process.WaitForExit();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void New_SetStartInfo(Action<Process, ProcessStartInfo> orig, Process inst, ProcessStartInfo startInfo)
        {
            if (startInfo.FileName == App._appUpExeName)
            {
                startInfo.FileName = App._appUpProxyExeName;
                startInfo.WorkingDirectory = Application.StartupPath;
                startInfo.UseShellExecute = false;
            }

            orig.Invoke(inst, startInfo);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void New_Application_ThreadException(Action<object, ThreadExceptionEventArgs> orig, object sender, ThreadExceptionEventArgs e)
        {
            MSGBox.OK($"出错了！\n\n{e.Exception.Message}", App._msgTitle);

            Application.Exit();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void New_CurrentDomain_UnhandledException(Action<object, UnhandledExceptionEventArgs> orig, object sender, UnhandledExceptionEventArgs e)
        {
            orig.Invoke(sender, e);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string New_StartupPath()
        {
            return Path.Combine(Application.StartupPath, App._appDir);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string New_GetExePath()
        {
            return Path.Combine(Application.StartupPath, App._appDir, App._appExeName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string New_GetVersion(Func<bool, string> orig, bool blFull = true)
        {
            var version = orig.Invoke(blFull);

            var self_version = FileVersionInfo.GetVersionInfo(Application.ExecutablePath).FileVersion.ToString();

            return $"{version}（{nameof(v2rayNProxy)} - V{self_version}）";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool New_UpdateSysProxy(Func<object, bool, bool> orig, object config, bool forceDisable)
        {
            if (!orig.Invoke(config, forceDisable))
            {
                return false;
            }

            if (_plugins == null)
            {
                _plugins = typeof(App).Assembly
                    .GetTypes()
                    .Where(t => !t.IsAbstract && typeof(PluginBase).IsAssignableFrom(t))
                    .Select(t => new Lazy<PluginBase>(() => Activator.CreateInstance(t) as PluginBase))
                    .Where(p => p != null)
                    .ToList();
            }

            if (_plugins.Count == 0)
            {
                return true;
            }

            try
            {
                var type = config.GetType();
                var verstr = type.Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
                var sysProxyType = (int)ReflectHelper.GetInstanceProperty(config, "sysProxyType");

                Version.TryParse(verstr, out var ver);

                int port = 0;

                if (ver.Major <= 4)
                {
                    port = (int)ReflectHelper.GetStaticProperty(type.Assembly.GetType("v2rayN.Global"), "httpPort");
                }
                else if (ver.Major == 5)
                {
                    port = (int)type.GetMethod("GetLocalPort").Invoke(config, new object[] { "http" });
                }

                if (forceDisable && sysProxyType == 1)
                {
                    sysProxyType = 0;
                }

                for (int i = 0; i < _plugins.Count; i++)
                {
                    var plugin = _plugins[i];

                    if (!plugin.Value.IsHasCmd())
                    {
                        continue;
                    }

                    switch (sysProxyType)
                    {
                        case 0:
                            plugin.Value.OnCloseProxy(_host, port);
                            break;
                        case 1:
                            plugin.Value.OnOpenProxy(_host, port);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                MSGBox.OK($"错误：{e.Message}", App._msgTitle);
            }

            return true;
        }
    }
}