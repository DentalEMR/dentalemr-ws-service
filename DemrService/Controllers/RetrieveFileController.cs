using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;

using DemrService.Settings;
using Microsoft.Extensions.Options;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace DemrService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RetrieveFileController : ControllerBase
    {
        private readonly ILogger<RetrieveFileController> _logger;
        private readonly AppSettings _appSettings;

        public RetrieveFileController(ILogger<RetrieveFileController> logger, IOptions<AppSettings> appSettings)
        {
            _logger = logger;
            _appSettings = appSettings.Value;

        }


        // GET: api/<controller>
        [HttpGet("test")]
        public IEnumerable<string> Test([FromQuery(Name = "path")] string path, [FromQuery(Name = "isdir")] bool isDir)
        {
            return new string[] { path, isDir.ToString() };
            //return new string[] { "value1", "value2" };
        }
        // GET: api/<controller>

        [HttpGet()]
        public async Task<IActionResult> Get([FromQuery(Name = "path")] string path, [FromQuery(Name = "isdir")] bool isDir)
        {
            try
            {
                string filePath = path;
                string tmpZip = Guid.NewGuid().ToString();

                if (isDir)
                {
                    await Task.Run(() =>
                    {
                        ZipFile.CreateFromDirectory(path, tmpZip);
                        filePath = tmpZip;
                    });
                }

                byte[] fileContents;
                using (FileStream SourceStream = System.IO.File.OpenRead(filePath))
                {
                    fileContents = new byte[SourceStream.Length];
                    await SourceStream.ReadAsync(fileContents, 0, (int)SourceStream.Length);
                }

                if (isDir)
                {
                    await Task.Run(() =>
                    {
                        if (System.IO.File.Exists(tmpZip))
                        {
                            System.IO.File.Delete(tmpZip);
                        }
                    });
                    string zipFileName = Directory.GetParent(path).Name + ".zip";
                    return File(fileContents, "application/zip", zipFileName); // returns a FileStreamResult
                }
                else
                {
                    var provider = new FileExtensionContentTypeProvider();
                    string contentType;
                    if (!provider.TryGetContentType(path, out contentType))
                    {
                        contentType = "application/octet-stream";
                    }
                    string fileName = Path.GetFileName(path);
                    return File(fileContents, contentType, fileName); // returns a FileStreamResult
                }

                /*

                FileStream sourceStream = System.IO.File.OpenRead(filePath);
                if (sourceStream == null)
                    return NotFound();
                var ret = File(sourceStream, "application/octet-stream"); // returns a FileStreamResult
                if (isDir)
                {
                    await Task.Run(() =>
                    {
                        if (System.IO.File.Exists(tmpZip))
                        {
                            System.IO.File.Delete(tmpZip);
                        }
                    });
                }
                return ret;
                */
            } 
            catch(Exception ex)
            {
                _logger.LogError(ex.Message);
                return new BadRequestResult();
            }
        }
    }
}
