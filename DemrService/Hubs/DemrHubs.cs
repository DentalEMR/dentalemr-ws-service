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

        //NOTE: https://docs.microsoft.com/en-us/aspnet/core/signalr/hubcontext?view=aspnetcore-3.1 probably doesn't work for Hubs themselves since they are not controllers,
        // and Hubs are transient (https://docs.microsoft.com/en-us/aspnet/core/signalr/hubs?view=aspnetcore-3.1) .
        // so put callbacks in closures
        // https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/statements-expressions-operators/lambda-expressions#capture-of-outer-variables-and-variable-scope-in-lambda-expressions)
        // https://stackoverflow.com/questions/595482/what-are-closures-in-c

        private static Dictionary<string, FileSystemWatcher> watchers;

        static DemrHub() 
        {
            watchers = new Dictionary<string, FileSystemWatcher>();
        }
        public DemrHub(ILogger<DemrHub> logger, IOptions<AppSettings> appSettings)
        {
            _logger = logger;
            _appSettings = appSettings.Value;
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

        protected async Task InvokeBinary(string binaryPath, string binaryArgs, string transactionId, Boolean isSynchronous)
        {
            string connectionId = Context.ConnectionId;
            try
            {

                Func<IHubCallerClients, string, Action<int, string, string, string, string>> finishedHandlerClosure = (IHubCallerClients clients, string connectionId) =>
                    async (int exitCode, string stdout, string stderr, string transactionId, string connectionId) =>
                    { 
                        _logger.LogInformation("{0} InvokeBinary: {1} {2} completed without exceptions returning exitCode: {3} and stdout: {4} and stderr: {5}",
                            DateTimeOffset.Now,
                            transactionId,
                            binaryPath,
                            exitCode.ToString(),
                            stdout,
                            stderr);

                        await clients.Client(connectionId).SendAsync("InvokeBinarySucceeded", exitCode, stdout, stderr, transactionId);
                    };

                BinaryExecuter executer = new BinaryExecuter(
                    binaryPath,
                    binaryArgs,
                    transactionId,
                    connectionId,
                    new BinaryExecuter.FinishedHandler(finishedHandlerClosure(Clients, connectionId))
                );

                _logger.LogInformation("{0} InvokeBinary {1}: Calling: {2}: with args: {3}.",
                    DateTimeOffset.Now,
                    transactionId,
                    binaryPath, 
                    binaryArgs);

                if (isSynchronous)
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

        
        #region RetrieveFileAndPost

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

        public Task RetrieveFileAndPost(string path, Boolean deletePath, string url, string postId, List<MultipartFormData> additionalMultipartFormData, string transactionId)
        {
            return RetrieveFileAndPost(path, deletePath, url, postId, additionalMultipartFormData, transactionId, true);
        }

        public Task RetrieveFileAndPostAsync(string path, Boolean deletePath, string url, string postId, List<MultipartFormData> additionalMultipartFormData, string transactionId)
        {
            return RetrieveFileAndPost(path, deletePath, url, postId, additionalMultipartFormData, transactionId, false);
        }


        protected async Task RetrieveFileAndPost(string path, Boolean deletePath, string url, string postId, List<MultipartFormData> additionalMultipartFormData, string transactionId, Boolean isSynchronous)
        {
            string connectionId = Context.ConnectionId;
            bool isDir = (File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory;

            try
            {
                _logger.LogInformation("{0} RetrieveFileAndPost {1}: Path: {2}; isDir: {3}; url: {4}.",
                    DateTimeOffset.Now,
                    transactionId,
                    path,
                    isDir.ToString(),
                    url);

                Func<IHubCallerClients, string, Func<Task>> runClosure = (IHubCallerClients clients, string connectionId) =>
                async () =>
                {
                    string tmpZip = Guid.NewGuid().ToString();
                    try
                    {
                        string filePath = path;
                        string fileName = Path.GetFileName(path);

                        if (isDir)
                        {
                            ZipFile.CreateFromDirectory(path, tmpZip);
                            filePath = tmpZip;
                            fileName += ".zip"; //Directory.GetParent(path).Name;
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
                                formData.Add(fileStreamContent, "file", fileName);
                                // for testing:  await Task.Delay(10000);
                                client.Timeout = TimeSpan.FromMinutes(120);
                                var response = await client.PostAsync(url, formData);
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
                                    await clients.Client(connectionId).SendAsync("RetrieveFileAndPostFailed", response.StatusCode.ToString(), response.ReasonPhrase, transactionId);
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
                                    await clients.Client(connectionId).SendAsync("RetrieveFileAndPostSucceeded", content, postId, path, deletePath, transactionId);
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
                        await clients.Client(connectionId).SendAsync("RetrieveFileAndPostExceptioned", ex.Message, transactionId);
                    }
                    finally
                    {
                        if (System.IO.File.Exists(tmpZip))
                        {
                            System.IO.File.Delete(tmpZip);
                        }
                    }
                };
                if (isSynchronous) //Run synchronously by 'await Task.Run(...'
                {
                    await runClosure(Clients, connectionId)();
                }
                else //Run asynchronously by assigning to discard (no await)
                {
                    _ = runClosure(Clients, connectionId)(); // see  https://docs.microsoft.com/en-us/dotnet/csharp/discards
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
                await Clients.Caller.SendAsync("RetrieveFileAndPostExceptioned", ex.Message, transactionId);
            }
        }

        public Task DeletePath(string path)
        {
            return Task.Run(() =>
            {
                if ((File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    Directory.Delete(path, true);
                }
                else
                {
                    File.Delete(path);
                }
            });
        }

        #endregion

        #region WatchForCreateInDir
        public void WatchForCreateInDirBegin(string watchInDir, Boolean del)
        {
            string connectionId = Context.ConnectionId;
            FileSystemWatcher watcher = null;
            // remove existing watcher and re-create for this key so closure captures new clients and connectionId
            if (watchers.ContainsKey(watchInDir))
            { 
                watcher = watchers[watchInDir];
                watchers.Remove(watchInDir);
                watcher.Dispose();
                watcher = null;
            }
            watcher = new FileSystemWatcher();
            watcher.Path = watchInDir;

            Func<IHubCallerClients, string, Boolean, Action<object, FileSystemEventArgs>> sendClosure = (IHubCallerClients clients, string connectionId, Boolean del) =>
                (object source, FileSystemEventArgs e) =>
                    clients.Client(connectionId).SendAsync(
                        "CreatedInWatchDir",
                        e.FullPath,
                        (File.GetAttributes(e.FullPath) & FileAttributes.Directory) == FileAttributes.Directory,
                        del);
            watcher.Created += new FileSystemEventHandler(sendClosure(Clients, connectionId, del));

            watcher.EnableRaisingEvents = true;
            watchers.Add(watchInDir, watcher);
        }

        public void WatchForCreateInDirEnd(string watchInDir)
        {
            if (watchers.ContainsKey(watchInDir))
            {
                FileSystemWatcher watcher = watchers[watchInDir];
                watchers.Remove(watchInDir);
                watcher.Dispose();
                watcher = null;
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing) //invoked by Hub.dispose()
            {
                base.Dispose(disposing);
            }
        }
    }
}
