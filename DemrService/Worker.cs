using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DemrService.Utils;
using DemrService.Settings;


namespace DemrService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly AppSettings _appSettings;

        public Worker(ILogger<Worker> logger, IOptions<AppSettings> appSettings)
        {
            _logger = logger;
            _appSettings = appSettings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {   
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                try
                {
                    BinaryExecuter executer = new BinaryExecuter(
                        _appSettings.ExePath, 
                        _appSettings.ExeArgs, 
                        System.Guid.NewGuid().ToString(),
                        System.Guid.NewGuid().ToString(),
                        (int exitCode, string stdout, string stderr, string transactionId, string invocationId) =>
                        { //Caller should be in closure https://stackoverflow.com/questions/595482/what-are-closures-in-c
                            _logger.LogInformation("Call to: "
                                + _appSettings.ExePath
                                + " completed without exceptions with exitcode: " + exitCode.ToString()
                                + " and stdout: " + stdout
                                + " and stderr: " + stderr
                        );
                    });


                    _logger.LogInformation("Calling: " + _appSettings.ExePath + " with args: " + _appSettings.ExeArgs);
                     
                    await executer.Execute();
                }
                catch (Exception ex)
                {
                    _logger.LogError("Call to: " + _appSettings.ExePath + " resulted in exception: " + ex.Message);
                }
                
                await Task.Delay(100000, stoppingToken);
            }
        }
    }
}
