using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Web.Administration;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace ARRReverseProxy
{
    class Program
    {
        private static ServerManager CreateServerManager(string sitename)
        {
            // 既存のサイトが出来るまで待機というかループ
            var serverManager = new ServerManager();
            var sc = serverManager.Sites[sitename];
            while (sc == null)
            {
                Thread.Sleep(TimeSpan.FromSeconds(100));
                serverManager.Dispose();
                serverManager = new ServerManager();
                sc = serverManager.Sites[sitename];
            }

            return serverManager;
        }

        static void Main(string[] args)
        {
            if (RoleEnvironment.IsEmulated)
                return;

            const string arrrootPath = @"C:\Resources\arrroot";
            const string cachePath = @"C:\Resources\cache";

            var timeout = double.Parse(RoleEnvironment.GetConfigurationSettingValue("ARRReverseProxy.Timeout"));

            // ARR用のディレクトリを作成
            if (!Directory.Exists(arrrootPath)) Directory.CreateDirectory(arrrootPath);
            if (!Directory.Exists(cachePath)) Directory.CreateDirectory(cachePath);

            // Web.configをコピー
            var sourceWebconfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Web.config");
            var destWebconfig = Path.Combine(arrrootPath, "Web.config");
            File.Copy(sourceWebconfig, destWebconfig, true);

            // サイト名を定義
            var webSiteName = RoleEnvironment.CurrentRoleInstance.Id + "_Web";
            var arrSiteName = RoleEnvironment.CurrentRoleInstance.Id + "_ARR";

            using (var serverManager = CreateServerManager(webSiteName))
            {
                // サイトを作成
                Trace.TraceInformation("ARRReverseProxy Adding Site {0}", arrSiteName);
                var arrsite = serverManager.Sites.Add(arrSiteName, @"http", @"*:80:", arrrootPath);

                // アプリケーションプールの作成
                Trace.TraceInformation("ARRReverseProxy Adding ApplicationPool {0}", arrSiteName);
                var appPool = serverManager.ApplicationPools.SingleOrDefault(ap => ap.Name.Equals(webSiteName, StringComparison.OrdinalIgnoreCase));
                if (appPool == null)
                {
                    appPool = serverManager.ApplicationPools.Add(arrSiteName);
                    appPool.ManagedRuntimeVersion = "v4.0";
                    appPool.ProcessModel.IdentityType = ProcessModelIdentityType.NetworkService;
                }
                arrsite.ApplicationDefaults.ApplicationPoolName = appPool.Name;

                // Cacheのドライブの設定
                Trace.TraceInformation("ARRReverseProxy Adding DiskCache");
                var config = serverManager.GetApplicationHostConfiguration();
                var diskCacheSection = config.GetSection("system.webServer/diskCache");
                var diskCacheCollection = diskCacheSection.GetCollection();
                var driveLocationElement = diskCacheCollection.CreateElement("driveLocation");
                driveLocationElement["path"] = cachePath;
                driveLocationElement["maxUsage"] = 1;
                diskCacheCollection.Add(driveLocationElement);

                // Proxyを有効にする
                Trace.TraceInformation("ARRReverseProxy Enabled Proxy");
                var proxySection = config.GetSection("system.webServer/proxy");
                proxySection.Attributes["enabled"].Value = true;
                proxySection.Attributes["httpVersion"].Value = "PassThrough";
                proxySection.Attributes["timeout"].Value = TimeSpan.FromSeconds(timeout);

                // bindingInformationを*を有効にする
                var sitesSection = config.GetSection("system.applicationHost/sites");
                var sitesCollection = sitesSection.GetCollection();

                var siteElement = FindElement(sitesCollection, "site", "name", webSiteName);
                var bindingsCollection = siteElement.GetCollection("bindings");
                var bindingElement = bindingsCollection.CreateElement("binding");
                bindingElement["protocol"] = @"http";
                bindingElement["bindingInformation"] = @"*:8080:";
                bindingsCollection.Add(bindingElement);

                try
                {
                    Trace.TraceInformation("ARRReverseProxy ServerManager.CommitChanges for {0}", arrSiteName);
                    serverManager.CommitChanges();
                }
                catch (Exception e)
                {
                    Trace.TraceError("ARRReverseProxy ServerManager.CommitChanges Error {0}", e.Message);
                }
            }

            Trace.TraceInformation("ARRReverseProxy Exit");
        }

        private static ConfigurationElement FindElement(ConfigurationElementCollection collection, string elementTagName, params string[] keyValues)
        {
            foreach (var element in collection)
            {
                if (String.Equals(element.ElementTagName, elementTagName, StringComparison.OrdinalIgnoreCase))
                {
                    var matches = true;
                    for (var i = 0; i < keyValues.Length; i += 2)
                    {
                        var o = element.GetAttributeValue(keyValues[i]);
                        string value = null;
                        if (o != null)
                        {
                            value = o.ToString();
                        }
                        if (!String.Equals(value, keyValues[i + 1], StringComparison.OrdinalIgnoreCase))
                        {
                            matches = false;
                            break;
                        }
                    }
                    if (matches)
                    {
                        return element;
                    }
                }
            }
            return null;
        }
    }
}
