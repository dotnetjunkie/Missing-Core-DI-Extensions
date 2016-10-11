using System;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Practices.Unity;
using MissingDIExtensions;

namespace SampleApplication.Unity
{
    public class Startup
    {
        private readonly UnityContainer container = new UnityContainer();
        private readonly AsyncLocal<IUnityContainer> scopeProvider = new AsyncLocal<IUnityContainer>();

        private T Resolve<T>() => scopeProvider.Value.Resolve<T>();
        private object Resolve(Type type) => scopeProvider.Value.Resolve(type);

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

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddRequestScopingMiddleware(() => scopeProvider.Value = container.CreateChildContainer());
            services.AddCustomControllerActivation(Resolve);
            services.AddCustomViewComponentActivation(Resolve);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            RegisterApplicationComponents(app, loggerFactory);

            // Add custom middleware
            app.Use(async (context, next) =>
            {
                await Resolve<CustomMiddleware>().Invoke(context, next);
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
            // Register application services
            app.GetControllerTypes().ToList().ForEach(t => container.RegisterType(t, PerRequest));

            container.RegisterType<IUserService, AspNetUserService>(PerRequest);

            // Cross-wire required framework services
            container.RegisterInstance(loggerFactory);
            container.RegisterType<IViewBufferScope>(new InjectionFactory(c => app.GetRequestService<IViewBufferScope>()));
        }

        private static HierarchicalLifetimeManager PerRequest = new HierarchicalLifetimeManager();
    }

    public interface IUserService { }

    public class AspNetUserService : IUserService { }
}