using System;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DemrService.Settings;
using DemrService.Utils;

namespace DemrService.Hubs
{
    public class DemrHub : Hub
    {
        private readonly ILogger<DemrHub> _logger;
        private readonly AppSettings _appSettings;

        public DemrHub(ILogger<DemrHub> logger, IOptions<AppSettings> appSettings)
        {
            _logger = logger;
            _appSettings = appSettings.Value;

        }

        public async Task InvokeBinary(string binaryPath, string binaryArgs)
        {
            try
            {
                BinaryExecuter executer = new BinaryExecuter(binaryPath, binaryArgs);

                _logger.LogInformation("{0} Invoke: Calling: {1}: with args: {2}.",
                    DateTimeOffset.Now, 
                    binaryPath, 
                    binaryArgs);

                int exitCode = await executer.Execute();

                _logger.LogInformation("{0} Invoke: {1} completed without exceptions returning exitCode: {2} and stdout: {3} and stderr: {4}",
                    DateTimeOffset.Now, 
                    binaryPath, 
                    exitCode.ToString(), 
                    executer.Output, 
                    executer.Error);

                await Clients.Caller.SendAsync("InvokeBinarySucceeded", exitCode, executer.Output, executer.Error);

            }
            catch (Exception ex)
            {
                _logger.LogError("{0} Invoke: {1} threw exception {2}",
                    DateTimeOffset.Now, 
                    binaryPath, 
                    ex.Message);
                await Clients.Caller.SendAsync("InvokeBinaryExceptioned", ex.Message);
            }

        }
    }
}
