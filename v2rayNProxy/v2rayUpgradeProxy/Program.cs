using System;
using v2rayUpgradeProxy.Utils;

namespace v2rayUpgradeProxy
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args) => App.Instance.Start(args);
    }
}