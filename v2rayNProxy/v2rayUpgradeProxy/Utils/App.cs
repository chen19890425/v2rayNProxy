using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;
using MonoMod.RuntimeDetour;
using Sys.Utils;
using Sys.Utils.Reflection;
using Sys.WinForms.UI;
using Sys.WinForms.UI.MessageBoxPositionManager;

namespace v2rayUpgradeProxy.Utils
{
    public sealed class App
    {
        private static readonly string _msgTitle = nameof(v2rayUpgradeProxy);
        private static readonly string _appDir = "app";
        private static readonly string _appName = "v2rayUpgrade";
        private static readonly string _appExeName = $"{App._appName}.exe";
        private static App _app = null;

        private App()
        {

        }

        public static App Instance => App._app ?? (App._app = new App());

        public void Start(string[] args)
        {
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

                        var a = ctx.Args[2] as string[];

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
                            assembly.EntryPoint.Invoke(null, new object[] { a });
                        }
                    },
                    dir,
                    path,
                    args);
                }
            }
            catch (Exception e)
            {
                MSGBox.OK($"错误：{e.Message}", App._msgTitle);
            }
        }

        private static IDisposable Create_Hooks(Assembly assembly)
        {
            return Disposable.Create(Get_MainForm_BtnOK_Click_Hook());

            Hook Get_MainForm_BtnOK_Click_Hook()
            {
                var type = assembly.GetType("v2rayUpgrade.MainForm");

                var method = type.GetMethod("btnOK_Click", BindingFlags.NonPublic | BindingFlags.Instance);

                var methodNew = typeof(App).GetMethod(nameof(New_BtnOK_Click), BindingFlags.NonPublic | BindingFlags.Static);

                return new Hook(method, methodNew);
            }
        }

        private static void ShowMsg(string msg)
        {
            MSGBox.OK(msg, App._msgTitle);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void New_BtnOK_Click(Form instance, object sender, EventArgs e)
        {
            var name = "v2rayNProxy";

            var fileName = ReflectHelper.GetInstanceField(instance, "fileName") as string;

            var defaultFilename = ReflectHelper.GetInstanceField(instance, "defaultFilename") as string;

            try
            {
                var processesByName = Process.GetProcessesByName(name);

                foreach (var process in processesByName)
                {
                    if (process.MainModule.FileName == GetPath($"{name}.exe"))
                    {
                        process.Kill();
                        process.WaitForExit(100);
                    }
                }
            }
            catch (Exception ex)
            {
                App.ShowMsg("Failed to close v2rayN(关闭v2rayN失败).\nClose it manually, or the upgrade may fail.(请手动关闭正在运行的v2rayN，否则可能升级失败。\n\n" + ex.StackTrace);
            }

            var stringBuilder = new StringBuilder();

            try
            {
                if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
                {
                    if (!File.Exists(defaultFilename))
                    {
                        App.ShowMsg("Upgrade Failed, File Not Exist(升级失败,文件不存在).");
                        return;
                    }

                    fileName = defaultFilename;
                }

                var executablePath = GetPath(App._appExeName);

                var tmp = executablePath + ".tmp";

                if (File.Exists(tmp))
                {
                    File.Delete(tmp);
                }

                var str = "v2rayN/";

                using (var zipArchive = ZipFile.OpenRead(fileName))
                {
                    foreach (var entry in zipArchive.Entries)
                    {
                        try
                        {
                            if (entry.Length != 0L)
                            {
                                var fullName = entry.FullName;

                                if (fullName.StartsWith(str))
                                {
                                    fullName = fullName.Substring(str.Length, fullName.Length - str.Length);
                                }

                                if (string.Compare(executablePath, GetPath(fullName), true) == 0)
                                {
                                    File.Move(executablePath, tmp);
                                }

                                var path = GetPath(fullName);

                                new FileInfo(path).Directory.Create();

                                entry.ExtractToFile(path, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            stringBuilder.Append(ex.StackTrace);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.ShowMsg("Upgrade Failed(升级失败)." + ex.StackTrace);
                return;
            }

            if (stringBuilder.Length > 0)
            {
                App.ShowMsg("Upgrade Failed,Hold Ctrl + C to copy to clipboard.\n(升级失败,按住 Ctrl + C 可以复制到剪贴板)." + stringBuilder.ToString());
                return;
            }

            Process.Start($"{name}.exe");

            App.ShowMsg("Upgrade successed(升级成功)");

            instance.Close();

            string GetPath(string fName)
            {
                var dir = Path.Combine(Application.StartupPath, App._appDir);

                if (string.IsNullOrEmpty(fName))
                {
                    return dir;
                }

                return Path.Combine(dir, fName);
            }
        }
    }
}