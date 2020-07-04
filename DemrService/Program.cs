using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.IO;
using System.Reflection;

namespace DemrService
{
    public class Program
    {
        private static string contentRootPath;

        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                //.ConfigureServices((hostContext, services) =>
                //{
                //    services.AddHostedService<Worker>();
                //})
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    contentRootPath = hostingContext.HostingEnvironment.ContentRootPath;
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(serverOptions =>
                    {
                        serverOptions.Listen(IPAddress.Loopback, 5000);    //IPAddress.Any
                        serverOptions.Listen(IPAddress.Loopback, 5001,     //IPAddress.Any
                            listenOptions =>
                            {
                                // For some reason a, b, and c all look in a strange directory when run as a service: C:\WINDOWS\TEMP\.net\DemrService\idy0d2hl.pnv\ 
                                //string a = contentRootPath;

                                //string b = AppDomain.CurrentDomain.BaseDirectory;

                                //string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                                //UriBuilder uri = new UriBuilder(codeBase);
                                //string path = Uri.UnescapeDataString(uri.Path);
                                //string c = Path.GetDirectoryName(path);

                                string pfxFullPath = Path.Combine(Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName), "demrservice.pfx");
                                // for debugging: throw new Exception($"{a} - {b} - {c} - {pfxFullPath}");

                                listenOptions.UseHttps(pfxFullPath, "dentalemr");
                            });
                     })
                    .UseStartup<Startup>();
                });
    }
}
