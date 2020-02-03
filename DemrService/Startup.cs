using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using DemrService.Hubs;
using DemrService.Settings;
using Microsoft.Extensions.Logging;

namespace DemrService
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var appSettings = Configuration.GetSection("AppSettings");
            services.Configure<AppSettings>(appSettings);
            services.AddControllers();
            services.AddSignalR();

            //services.AddCors();
            services.AddCors(options => options.AddPolicy("CorsPolicy",
                builder =>
                {
                    builder.AllowAnyMethod().AllowAnyHeader()
                           //.WithOrigins("https://localhost:44326", "http://10.0.2.15")
                           .WithOrigins("https://localhost:44326", "https://app.dentaledr.com", "https://app.dentalemr.com")
                           .AllowCredentials();
                }
            ));

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            loggerFactory.AddFile("Logs/demrservice-{Date}.txt");

            app.UseRouting();

            app.UseCors("CorsPolicy");
            //app.UseCors(config => config.AllowAnyOrigin());
            //app.UseCors(config => config.WithOrigins("http://localhost:52547")
            //    .AllowAnyMethod()
            //    .AllowAnyHeader()
            //    .AllowCredentials());

            //app.UseAuthentication();
            //app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<DemrHub>("/demr");
            });
        }
    }
}
