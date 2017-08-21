﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using v2rayN.Mode;
using v2rayN.Properties;

namespace v2rayN.Handler
{
    class PrivoxyHandler
    {
        private static int _uid;
        private static string _uniqueConfigFile;
        private static Job _privoxyJob;
        private Process _process;
        private int _runningPort;

        static PrivoxyHandler()
        {
            try
            {
                _uid = Application.StartupPath.GetHashCode(); // Currently we use ss's StartupPath to identify different Privoxy instance.
                _uniqueConfigFile = string.Format("privoxy_{0}.conf", _uid);
                _privoxyJob = new Job();

                FileManager.UncompressFile(Utils.GetTempPath("v2ray_privoxy.exe"), Resources.privoxy_exe);
                FileManager.UncompressFile(Utils.GetTempPath("mgwz.dll"), Resources.mgwz_dll);
            }
            catch (IOException e)
            {
                //Logging.LogUsefulException(e);
            }
        }

        public int RunningPort
        {
            get
            {
                return _runningPort;
            }
        }
        public void Start(string localPort)
        {
            if (_process == null)
            {
                Process[] existingPrivoxy = Process.GetProcessesByName("v2ray_privoxy");
                foreach (Process p in existingPrivoxy.Where(IsChildProcess))
                {
                    KillProcess(p);
                }
                string privoxyConfig = Resources.privoxy_conf;
                _runningPort = GetFreePort(Convert.ToInt32(localPort));
                privoxyConfig = privoxyConfig.Replace("__SOCKS_PORT__", localPort);
                privoxyConfig = privoxyConfig.Replace("__PRIVOXY_BIND_PORT__", _runningPort.ToString());
                privoxyConfig = privoxyConfig.Replace("__PRIVOXY_BIND_IP__", "0.0.0.0");
                FileManager.ByteArrayToFile(Utils.GetTempPath(_uniqueConfigFile), Encoding.UTF8.GetBytes(privoxyConfig));

                _process = new Process
                {
                    // Configure the process using the StartInfo properties.
                    StartInfo =
                    {
                        FileName = "v2ray_privoxy.exe",
                        Arguments = _uniqueConfigFile,
                        WorkingDirectory = Utils.GetTempPath(),
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = true,
                        CreateNoWindow = true
                    }
                };
                _process.Start();

                /*
                 * Add this process to job obj associated with this ss process, so that
                 * when ss exit unexpectedly, this process will be forced killed by system.
                 */
                _privoxyJob.AddProcess(_process.Handle);
            }
        }

        public void Stop()
        {
            if (_process != null)
            {
                KillProcess(_process);
                _process.Dispose();
                _process = null;
            }
        }

        private static void KillProcess(Process p)
        {
            try
            {
                p.CloseMainWindow();
                p.WaitForExit(100);
                if (!p.HasExited)
                {
                    p.Kill();
                    p.WaitForExit();
                }
            }
            catch (Exception e)
            {
                //Logging.LogUsefulException(e);
            }
        }

        /*
         * We won't like to kill other ss instances' v2ray_privoxy.exe.
         * This function will check whether the given process is created
         * by this process by checking the module path or command line.
         * 
         * Since it's required to put ss in different dirs to run muti instances,
         * different instance will create their unique "privoxy_UID.conf" where
         * UID is hash of ss's location.
         */

        private static bool IsChildProcess(Process process)
        {
            try
            {
                /*
                 * Under PortableMode, we could identify it by the path of v2ray_privoxy.exe.
                 */
                var path = process.MainModule.FileName;

                return Utils.GetTempPath("v2ray_privoxy.exe").Equals(path);

            }
            catch (Exception ex)
            {
                /*
                 * Sometimes Process.GetProcessesByName will return some processes that
                 * are already dead, and that will cause exceptions here.
                 * We could simply ignore those exceptions.
                 */
                //Logging.LogUsefulException(ex);
                return false;
            }
        }

        private int GetFreePort(int localPort)
        {
            int defaultPort = 8123;
            try
            {
                //// TCP stack please do me a favor
                //TcpListener l = new TcpListener(IPAddress.Loopback, 0);
                //l.Start();
                //var port = ((IPEndPoint)l.LocalEndpoint).Port;
                //l.Stop();
                //return port;
                return localPort + 1000;
            }
            catch (Exception e)
            {
                // in case access denied
                //Logging.LogUsefulException(e);
                return defaultPort;
            }
        }
    }
}
