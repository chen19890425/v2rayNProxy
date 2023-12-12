namespace v2rayNProxy.Utils.Impls
{
    public sealed class GitPlugin : PluginBase
    {
        protected override string GetCmd()
        {
            return "git";
        }

        public override void OnCloseProxy(string host, int port)
        {
            Exe_Cmd(Cmd, "config --global --unset http.proxy");
            Exe_Cmd(Cmd, "config --global --unset https.proxy");
        }

        public override void OnOpenProxy(string host, int port)
        {
            Exe_Cmd(Cmd, $"config --global http.sslVerify false");
            Exe_Cmd(Cmd, $"config --global http.proxy http://{host}:{port}");
            Exe_Cmd(Cmd, $"config --global https.proxy https://{host}:{port}");
        }
    }
}