using System;
using v2rayNProxy.Utils;

namespace v2rayNProxy
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args) => App.Instance.Start(args);
    }
}