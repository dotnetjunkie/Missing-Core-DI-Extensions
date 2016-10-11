using System;
using Castle.MicroKernel.Lifestyle;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MissingDIExtensions;

namespace SampleApplication.CastleWindsor
{
    public class Startup
    {
        private readonly WindsorContainer container = new WindsorContainer();

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

            services.AddRequestScopingMiddleware(container.BeginScope);
            services.AddCustomControllerActivation(container.Resolve);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            RegisterApplicationComponents(app, loggerFactory);

            // Add custom middleware
            app.Use(async (context, next) =>
            {
                await container.Resolve<CustomMiddleware>().Invoke(context, next);
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
            container.Register(Component.For(app.GetControllerTypes()).LifestyleScoped());
            // container.Register(Component.For(app.GetApplicationViewComponents()));

            container.Register(Component.For<IUserService>().ImplementedBy<AspNetUserService>().LifestyleScoped());
            container.Register(Component.For<CustomMiddleware>());

            // Cross-wire required framework services
            RegisterFactoryMethod(app.GetRequestService<IHttpContextAccessor>);
            container.Register(Component.For<ILoggerFactory>().Instance(loggerFactory));
        }

        void RegisterFactoryMethod<T>(Func<T> factory) where T : class
        {
            container.Register(Component.For<T>().UsingFactoryMethod(_ => factory()));
        }
    }

    public interface IUserService { }

    public class AspNetUserService : IUserService { }
}