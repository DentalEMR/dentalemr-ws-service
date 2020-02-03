using System;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;
using System.IO.Compression;

using DemrService.Settings;
using DemrService.Utils;
using Microsoft.AspNetCore.StaticFiles;
using System.Net.Http;
using System.Collections.Generic;

namespace DemrService.Hubs
{
    public class DemrHub : Hub
    {
        private readonly ILogger<DemrHub> _logger;
        private readonly AppSettings _appSettings;
        private readonly IHubContext<DemrHub> _demr_hub;

        public DemrHub(ILogger<DemrHub> logger, IOptions<AppSettings> appSettings, IHubContext<DemrHub> demr_hub)
        {
            _logger = logger;
            _appSettings = appSettings.Value;
            _demr_hub = demr_hub;

        }

        #region InvokeBinary
        public Task InvokeBinary(string binaryPath, string binaryArgs, string transactionId)
        {
            return InvokeBinary(binaryPath, binaryArgs, transactionId, true);
        }

        public Task InvokeBinaryAsync(string binaryPath, string binaryArgs, string transactionId)
        {
            return InvokeBinary(binaryPath, binaryArgs, transactionId, false);
        }

        protected async Task InvokeBinary(string binaryPath, string binaryArgs, string transactionId, Boolean synchronous)
        {
            string connectionId = Context.ConnectionId;
            try
            {
                BinaryExecuter executer = new BinaryExecuter(binaryPath, 
                    binaryArgs, 
                    transactionId, 
                    connectionId, 
                    async (int exitCode, string stdout, string stderr, string transactionId, string connectionId) =>
                    { //Caller should be in closure https://stackoverflow.com/questions/595482/what-are-closures-in-c
                        _logger.LogInformation("{0} InvokeBinary: {1} {2} completed without exceptions returning exitCode: {3} and stdout: {4} and stderr: {5}",
                            DateTimeOffset.Now,
                            transactionId,
                            binaryPath,
                            exitCode.ToString(),
                            stdout,
                            stderr);

                        await _demr_hub.Clients.Client(connectionId).SendAsync("InvokeBinarySucceeded", exitCode, stdout, stderr, transactionId);
                    });

                _logger.LogInformation("{0} InvokeBinary {1}: Calling: {2}: with args: {3}.",
                    DateTimeOffset.Now,
                    transactionId,
                    binaryPath, 
                    binaryArgs);

                if (synchronous)
                {
                    await executer.Execute();
                }
                else
                {
                    _ = executer.Execute(); // see https://docs.microsoft.com/en-us/dotnet/csharp/discards
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("{0} InvokeBinary {1}: {2} threw exception {3}",
                    DateTimeOffset.Now, 
                    transactionId,
                    binaryPath, 
                    ex.Message);
                await Clients.Caller.SendAsync("InvokeBinaryExceptioned", ex.Message, transactionId);
            }

        }

        #endregion

        #region RetrieveFileAndPostAsync

        public struct MultipartFormData
        {
            public MultipartFormData(string Name, string Value)
            {
                this.Name = Name;
                this.Value = Value;
            }

            public string Name { get; set; }
            public string Value { get; set; }
        }
        public async Task RetrieveFileAndPostAsync(string path, Boolean isDir, string url, List<MultipartFormData> additionalMultipartFormData, string transactionId)
        {
            string connectionId = Context.ConnectionId;

            try
            {
                _logger.LogInformation("{0} RetrieveFileAndPost {1}: Path: {2}; isDir: {3}; url: {4}.",
                    DateTimeOffset.Now,
                    transactionId,
                    path,
                    isDir.ToString(),
                    url);

                _ = Task.Run( async () =>  //run asynchronously by assigning to discard. Run synchronously by 'await Task.Run(...'
                {
                    try
                    {

                        string filePath = path;
                        string tmpZip = Guid.NewGuid().ToString();
                        string fileName = Path.GetFileName(path);

                        if (isDir)
                        {
                            ZipFile.CreateFromDirectory(path, tmpZip);
                            filePath = tmpZip;
                            fileName = Directory.GetParent(path).Name;
                        }

                        using (FileStream fileStream = System.IO.File.OpenRead(filePath))
                        {
                            HttpContent fileStreamContent = new StreamContent(fileStream);
                            using (var client = new HttpClient())
                            using (var formData = new MultipartFormDataContent())
                            {
                                foreach (MultipartFormData data in additionalMultipartFormData)
                                {
                                    formData.Add(new StringContent(data.Value), data.Name);
                                }
                                formData.Add(fileStreamContent, fileName, fileName);
                                var response = await client.PostAsync(url, formData);
                                if (isDir)
                                {
                                    if (System.IO.File.Exists(tmpZip))
                                    {
                                        System.IO.File.Delete(tmpZip);
                                    }
                                }
                                if (!response.IsSuccessStatusCode)
                                {
                                    _logger.LogInformation("{0} RetrieveFileAndPost {1}: {2}; {3}; {4} returned status failed. Status code: {5}; Reason: {6}.",
                                         DateTimeOffset.Now,
                                         transactionId,
                                         path,
                                         isDir.ToString(),
                                         url,
                                         response.StatusCode.ToString(),
                                         response.ReasonPhrase);
                                    await _demr_hub.Clients.Client(connectionId).SendAsync("RetrieveFileAndPostFailed", response.StatusCode.ToString(), response.ReasonPhrase, transactionId);
                                }
                                else
                                {
                                    string content = "";
                                    Stream contentStream = await response.Content.ReadAsStreamAsync();
                                    using (StreamReader reader = new StreamReader(contentStream))
                                    {
                                        content = reader.ReadToEnd();
                                        // Do something with the value
                                    }
                                    _logger.LogInformation("{0} RetrieveFileAndPost {1}: {2}; {3}; {4} returned status success with response content: {5}",
                                        DateTimeOffset.Now,
                                        transactionId,
                                        path,
                                        isDir.ToString(),
                                        url,
                                        content);
                                    await _demr_hub.Clients.Client(connectionId).SendAsync("RetrieveFileAndPostSucceeded", content, transactionId);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("{0} RetrieveFileAndPost {1}: {2}; {3}; {4} threw exception {5}",
                            DateTimeOffset.Now,
                            transactionId,
                            path,
                            isDir.ToString(),
                            url,
                            ex.Message);
                        await _demr_hub.Clients.Client(connectionId).SendAsync("RetrieveFileAndPostExceptioned", ex.Message, transactionId);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("{0} RetrieveFileAndPost {1}: {2}; {3}; {4} threw exception {5}",
                    DateTimeOffset.Now,
                    transactionId,
                    path,
                    isDir.ToString(),
                    url,
                    ex.Message);
                await Clients.Caller.SendAsync("RetrieveFileAndPostExceptioned", ex.Message, transactionId);
            }
        }

        #endregion
    }
}
