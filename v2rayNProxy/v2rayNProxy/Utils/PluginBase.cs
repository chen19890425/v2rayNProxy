using System.Diagnostics;

namespace v2rayNProxy.Utils
{
    public abstract class PluginBase
    {
        protected string Cmd { get; }

        protected PluginBase()
        {
            Cmd = GetCmd();
        }

        protected void Exe_Cmd(string exe, string args = null)
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

        protected abstract string GetCmd();

        public virtual bool IsHasCmd()
        {
            try
            {
                Exe_Cmd(Cmd);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public abstract void OnCloseProxy(string host, int port);

        public abstract void OnOpenProxy(string host, int port);
    }
}