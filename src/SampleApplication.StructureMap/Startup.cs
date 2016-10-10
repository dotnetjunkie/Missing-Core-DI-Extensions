using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MissingDIExtensions;
using StructureMap;

namespace SampleApplication.StructureMap
{
    public class Startup
    {
        private readonly Container container = new Container();
        private readonly AsyncLocal<IContainer> scopeProvider = new AsyncLocal<IContainer>();

        private T GetInstance<T>() => scopeProvider.Value.GetInstance<T>();
        private object GetInstance(Type type) => scopeProvider.Value.GetInstance(type);

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc();

            services.AddRequestScopingMiddleware(() => scopeProvider.Value = container.GetNestedContainer());
            services.AddCustomControllerActivation(GetInstance);
            services.AddCustomViewComponentActivation(GetInstance);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            RegisterApplicationComponents(app, loggerFactory);

            // Add custom middleware
            app.Use(async (context, next) =>
            {
                await GetInstance<CustomMiddleware>().Invoke(context, next);
            });

            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }

        private void RegisterApplicationComponents(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            container.Configure(c =>
            {
                // Register application services
                c.For<IUserService>().Use<AspNetUserService>().ContainerScoped();

                // Cross-wire required framework services
                c.For<ILoggerFactory>().Use(loggerFactory);
                c.For<IHttpContextAccessor>().Use(_ => app.GetRequestService<IHttpContextAccessor>());
            });
        }
    }

    public interface IUserService { }

    public class AspNetUserService : IUserService { }
}