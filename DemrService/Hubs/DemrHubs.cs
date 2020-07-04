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
using System.Drawing;
using System.Drawing.Imaging;

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

        #region RetrieveFileAndPost

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
                    string tmpZip = Path.GetTempPath() + Guid.NewGuid().ToString();
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
                            ex.ToString());
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

        #endregion

        #region DeletePath

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

        #region RetrieveImageAndPost

        public Task RetrieveImageAndPost(
            string imagePath,
            Boolean deleteImagePath,
            string imageUrl,
            string imagePostId,
            List<MultipartFormData> imageAdditionalMultipartFormData,
            string thumbUrl,
            string thumbPostId,
            List<MultipartFormData> thumbAdditionalMultipartFormData,
            string transactionId)

        {
            return RetrieveImageAndPost(
                imagePath,
                deleteImagePath,
                imageUrl,
                imagePostId,
                imageAdditionalMultipartFormData,
                thumbUrl,
                thumbPostId,
                thumbAdditionalMultipartFormData,
                transactionId,
                true);
        }

        public Task RetrieveImageAndPostAsync(
            string imagePath,
            Boolean deleteImagePath,
            string imageUrl,
            string imagePostId,
            List<MultipartFormData> imageAdditionalMultipartFormData,
            string thumbUrl,
            string thumbPostId,
            List<MultipartFormData> thumbAdditionalMultipartFormData,
            string transactionId)

        {
            return RetrieveImageAndPost(
                imagePath,
                deleteImagePath,
                imageUrl,
                imagePostId,
                imageAdditionalMultipartFormData,
                thumbUrl,
                thumbPostId,
                thumbAdditionalMultipartFormData,
                transactionId,
                false);
        }


        protected async Task RetrieveImageAndPost(
            string imagePath, 
            Boolean deleteImagePath, 
            string imageUrl, 
            string imagePostId, 
            List<MultipartFormData> imageAdditionalMultipartFormData,
            string thumbUrl,
            string thumbPostId,
            List<MultipartFormData> thumbAdditionalMultipartFormData,
            string transactionId, 
            Boolean isSynchronous)
        {
            string connectionId = Context.ConnectionId;

            try
            {
                _logger.LogInformation("{0} RetrieveImageAndPost {1}: imagePath: {2}; imageUrl: {3}; thumbUrl: {4}",
                    DateTimeOffset.Now,
                    transactionId,
                    imagePath,
                    imageUrl,
                    thumbUrl);

                Func<IHubCallerClients, string, Func<Task>> runClosure = (IHubCallerClients clients, string connectionId) =>
                async () =>
                {
                    string tmpZip = Guid.NewGuid().ToString();
                    try
                    {
                        string filePath = imagePath;
                        string fileName = Path.GetFileName(imagePath);

                        if ((File.GetAttributes(imagePath) & FileAttributes.Directory) == FileAttributes.Directory)
                        {
                            throw new HubException("imageFile is a directory");
                        }

                        using (FileStream fileStreamForImage = System.IO.File.OpenRead(filePath))
                        using (FileStream fileStreamForBuildingThumb = System.IO.File.OpenRead(filePath))
                        using (var thumbJpegStream = new MemoryStream())
                        {
                            Image image = Image.FromStream(fileStreamForBuildingThumb, true, true);
                            int thumbWidth = (image.Width > 640) ? 640 : image.Width;
                            int thumbHeight = (image.Width > 640) ? (int)Math.Round(image.Height * (640.0 / image.Width)) : image.Height;
                            var thumbImage = new Bitmap(thumbWidth, thumbHeight);
                            using (var g = Graphics.FromImage(thumbImage))
                            {
                                g.DrawImage(image, 0, 0, thumbWidth, thumbHeight);
                            }
                            thumbImage.Save(thumbJpegStream, ImageFormat.Jpeg);
                            thumbJpegStream.Position = 0;

                            using (HttpContent fileStreamContent = new StreamContent(fileStreamForImage))
                            using (HttpContent thumbtreamContent = new StreamContent(thumbJpegStream))
                            using (var imageClient = new HttpClient())
                            using (var thumbClient = new HttpClient())
                            using (var imageFormData = new MultipartFormDataContent())
                            using (var thumbFormData = new MultipartFormDataContent())
                            {
                                foreach (MultipartFormData data in imageAdditionalMultipartFormData)
                                {
                                    imageFormData.Add(new StringContent(data.Value), data.Name);
                                }
                                imageFormData.Add(fileStreamContent, "file", fileName);
                                imageClient.Timeout = TimeSpan.FromMinutes(30);
                                Task<HttpResponseMessage> imageUploadResponseTask = imageClient.PostAsync(imageUrl, imageFormData);
                                //var response = await imageClient.PostAsync(imageUrl, imageFormData);

                                foreach (MultipartFormData data in thumbAdditionalMultipartFormData)
                                {
                                    thumbFormData.Add(new StringContent(data.Value), data.Name);
                                }
                                thumbFormData.Add(thumbtreamContent, "file", fileName);
                                thumbClient.Timeout = TimeSpan.FromMinutes(5);
                                Task<HttpResponseMessage> thumbUploadResponseTask = imageClient.PostAsync(thumbUrl, thumbFormData);

                                var responseTasks = Task.WhenAll(imageUploadResponseTask, thumbUploadResponseTask);
                                await responseTasks;

                                bool imageSuccess = responseTasks.Result[0].IsSuccessStatusCode;
                                var imageStatusCode = responseTasks.Result[0].StatusCode;
                                string imageReason = responseTasks.Result[0].ReasonPhrase;

                                bool thumbSuccess = responseTasks.Result[1].IsSuccessStatusCode;
                                var thumbStatusCode = responseTasks.Result[1].StatusCode;
                                string thumbReason = responseTasks.Result[1].ReasonPhrase;

                                if (!imageSuccess || !thumbSuccess)
                                {
                                    _logger.LogInformation("{0} RetrieveImageAndPost {1}: {2}; {3}; {4} returned status failed. Image status code: {5}; Image reason: {6}. Thumb status code: {7}; Thumb reason: {8}.",
                                            DateTimeOffset.Now,
                                            transactionId,
                                            imagePath,
                                            imageUrl,
                                            thumbUrl,
                                            imageStatusCode.ToString(),
                                            imageReason,
                                            thumbStatusCode.ToString(),
                                            thumbReason);
                                    await clients.Client(connectionId).SendAsync("RetrieveImageAndPostFailed", imageStatusCode.ToString(), imageReason, thumbStatusCode.ToString(), thumbReason, transactionId);
                                }
                                else
                                {
                                    string imageContent = "";
                                    Stream imageContentStream = await responseTasks.Result[0].Content.ReadAsStreamAsync();
                                    using (StreamReader reader = new StreamReader(imageContentStream))
                                    {
                                        imageContent = reader.ReadToEnd();
                                    }

                                    string thumbContent = "";
                                    Stream thumbContentStream = await responseTasks.Result[1].Content.ReadAsStreamAsync();
                                    using (StreamReader reader = new StreamReader(thumbContentStream))
                                    {
                                        thumbContent = reader.ReadToEnd();
                                    }

                                    _logger.LogInformation("{0} RetrieveImageAndPost {1}: {2}; {3} returned status success with response content: {4}; {5} returned status success with response content: {6}",
                                        DateTimeOffset.Now,
                                        transactionId,
                                        imagePath,
                                        imageUrl,
                                        imageContent,
                                        thumbUrl,
                                        thumbContent);
                                    await clients.Client(connectionId).SendAsync("RetrieveImageAndPostSucceeded", imageContent, imagePostId, thumbContent, thumbPostId, imagePath, deleteImagePath, transactionId);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("{0} RetrieveImageAndPost {1}: {2}; {3}; {4} threw exception {5}",
                            DateTimeOffset.Now,
                            transactionId,
                            imagePath,
                            imageUrl,
                            thumbUrl,
                            ex.Message);
                        await clients.Client(connectionId).SendAsync("RetrieveImageAndPostExceptioned", ex.Message, transactionId);
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
                _logger.LogError("{0} RetrieveImageAndPost {1}: {2}; {3}; {4} threw exception {5}",
                    DateTimeOffset.Now,
                    transactionId,
                    imagePath,
                    imageUrl,
                    thumbUrl,
                    ex.ToString());
                await Clients.Caller.SendAsync("RetrieveImageAndPostExceptioned", ex.Message, transactionId);
            }
        }

        #endregion

        #region WatchForCreateInDir
        public string WatchForCreateInDirBegin(string watchId, string watchInDir, Boolean del, Boolean disableCreatedEvent, Boolean enableRenamedEvent)
        {
            string watchInDirFullPath;

            if (watchInDir == null || watchInDir.Trim().Length == 0)
            {
                string message = String.Format("watchId {0} watchInDir is blank.", watchId);
                _logger.LogError(message);
                throw new HubException(message);
            }
            else if (!Directory.Exists(watchInDir))
            {
                string message = String.Format("watchId {0} watchInDir {1} does not exist.", watchId, (Path.IsPathFullyQualified(watchInDir)) ? watchInDir : Path.GetFullPath(watchInDir));
                _logger.LogError(message);
                throw new HubException(message);
            }
            else
            {
                watchInDirFullPath = (Path.IsPathFullyQualified(watchInDir)) ? watchInDir : Path.GetFullPath(watchInDir);
                string message = String.Format("watchId {0} watching in {1}.", watchId, watchInDirFullPath);
            }

            string connectionId = Context.ConnectionId;
            FileSystemWatcher watcher = null;
            // remove existing watcher and re-create for this key so closure captures new clients and connectionId
            if (watchers.ContainsKey(watchId))
            { 
                watcher = watchers[watchId];
                watchers.Remove(watchId);
                watcher.Dispose();
                watcher = null;
            }
            watcher = new FileSystemWatcher();
            watcher.Path = watchInDirFullPath;

            Func<IHubCallerClients, string, string, Boolean, Action<object, FileSystemEventArgs>> createdHandlerClosure = (IHubCallerClients clients, string connectionId, string watchId, Boolean del) =>
                (object source, FileSystemEventArgs e) =>
                {
                    try
                    {
                        FileAttributes fileAttributes = File.GetAttributes(e.FullPath);
                        if ( ((fileAttributes & FileAttributes.Hidden) == FileAttributes.Hidden) || ((fileAttributes & FileAttributes.Offline) == FileAttributes.Offline) || ((fileAttributes & FileAttributes.System) == FileAttributes.System))
                        {
                            _logger.LogDebug("{0} WatchForCreateInDirBegin {1}: Created handler: file {2} has FileAttributes(s) {3} (hidden, offline, or system). Ignoring file system watch event.",
                                DateTimeOffset.Now,
                                watchId,
                                e.FullPath,
                                fileAttributes.ToString()
                            );
                            return;
                        }

                        clients.Client(connectionId).SendAsync(
                            "CreatedInWatchDir",
                            watchId,
                            e.FullPath,
                            (fileAttributes & FileAttributes.Directory) == FileAttributes.Directory,
                            del);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("{0} WatchForCreateInDirBegin {1}: Created handler: path {2} threw exception {3} in FileSystemEventHandler. Ignoring watch event.",
                            DateTimeOffset.Now,
                            watchId,
                            e.FullPath,
                            ex.ToString());
                    }
                };
            if (!disableCreatedEvent)
            {
                watcher.Created += new FileSystemEventHandler(createdHandlerClosure(Clients, connectionId, watchId, del));
            }

            Func<IHubCallerClients, string, string, Boolean, Action<object, RenamedEventArgs>> renamedHandlerClosure = (IHubCallerClients clients, string connectionId, string watchId, Boolean del) =>
                (object source, RenamedEventArgs e) =>
                {
                    try
                    {
                        FileAttributes fileAttributes = File.GetAttributes(e.FullPath);
                        if (((fileAttributes & FileAttributes.Hidden) == FileAttributes.Hidden) || ((fileAttributes & FileAttributes.Offline) == FileAttributes.Offline) || ((fileAttributes & FileAttributes.System) == FileAttributes.System))
                        {
                            _logger.LogDebug("{0} WatchForCreateInDirBegin {1}: Renamed handler: path {2} renamed to {3} has FileAttributes(s) {4} (hidden, offline, or system). Ignoring file system watch event.",
                                DateTimeOffset.Now,
                                watchId,
                                e.OldFullPath,
                                e.FullPath,
                                fileAttributes.ToString()
                            );
                            return;
                        }

                        clients.Client(connectionId).SendAsync(
                            "CreatedInWatchDir",
                            watchId,
                            e.FullPath,
                            (fileAttributes & FileAttributes.Directory) == FileAttributes.Directory,
                            del);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("{0} WatchForCreateInDirBegin {1}: Renamed handler: path {2} renamed to {3} threw exception {4} in FileSystemEventHandler. Ignoring watch event.",
                            DateTimeOffset.Now,
                            watchId,
                            e.OldFullPath,
                            e.FullPath,
                            ex.ToString());
                    }
                };
            if (enableRenamedEvent)
            {
                watcher.Renamed += new RenamedEventHandler(renamedHandlerClosure(Clients, connectionId, watchId, del));
            }

            watcher.EnableRaisingEvents = true;
            watchers.Add(watchId, watcher);
            return watchInDirFullPath;
        }

        public void WatchForCreateInDirEnd(string watchId, string watchInDir)
        {
            if (watchers.ContainsKey(watchId))
            {
                FileSystemWatcher watcher = watchers[watchId];
                watchers.Remove(watchId);
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
