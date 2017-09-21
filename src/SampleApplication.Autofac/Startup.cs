using System;
using System.Threading;
using Autofac;
using Autofac.Core.Lifetime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MissingDIExtensions;

namespace SampleApplication.Autofac
{
    public class Startup
    {
        private readonly AsyncLocal<ILifetimeScope> scopeProvider = new AsyncLocal<ILifetimeScope>();
        private IContainer container;

        private ILifetimeScope BeginLifetimeScope() => scopeProvider.Value = container.BeginLifetimeScope(MatchingScopeLifetimeTags.RequestLifetimeScopeTag);
        private object Resolve(Type type) => scopeProvider.Value.Resolve(type);
        private T Resolve<T>() => scopeProvider.Value.Resolve<T>();

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

            services.AddRequestScopingMiddleware(BeginLifetimeScope);
            services.AddCustomControllerActivation(Resolve);
            services.AddCustomViewComponentActivation(Resolve);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            this.container = RegisterApplicationComponents(app, loggerFactory);

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

        private static IContainer RegisterApplicationComponents(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterInstance(app.ApplicationServices.GetService<IHttpContextAccessor>());
            builder.RegisterInstance(loggerFactory);

            // Register application services
            builder.RegisterTypes(app.GetControllerTypes()).InstancePerRequest();
            builder.RegisterTypes(app.GetViewComponentTypes());

            builder.RegisterType<AspNetUserService>().As<IUserService>().InstancePerRequest();
            builder.RegisterType<CustomMiddleware>();

            // Cross-wire required framework services
            builder.Register(_ => app.GetRequiredRequestService<IViewBufferScope>());

            return builder.Build();
        }
    }

    public interface IUserService { }

    public class AspNetUserService : IUserService { }
}