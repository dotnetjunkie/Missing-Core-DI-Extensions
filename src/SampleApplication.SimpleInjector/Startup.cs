using System;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MissingDIExtensions;
using SimpleInjector;
using SimpleInjector.Extensions.ExecutionContextScoping;

namespace SampleApplication.SimpleInjector
{
    public class Startup
    {
        private readonly Container container = new Container();

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

            services.AddRequestScopingMiddleware(container.BeginExecutionContextScope);
            services.AddCustomControllerActivation(container.GetInstance);
            services.AddCustomViewComponentActivation(container.GetInstance);
            services.AddCustomTagHelperActivation(this.container.GetInstance, IsApplicationType);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            RegisterApplicationComponents(app, loggerFactory);

            // Add custom middleware
            app.Use(async (context, next) =>
            {
                await container.GetInstance<CustomMiddleware>().Invoke(context, next);
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
            container.Options.DefaultScopedLifestyle = new ExecutionContextScopeLifestyle();

            // Register application services
            container.RegisterMvcControllers(app);
            container.RegisterMvcViewComponents(app);

            container.Register<IUserService, AspNetUserService>(Lifestyle.Scoped);
            container.Register<CustomMiddleware>();

            // Cross-wire required framework services
            container.RegisterSingleton<Func<IViewBufferScope>>(() => app.GetRequestService<IViewBufferScope>());
            container.RegisterSingleton(loggerFactory);

            container.Verify();
        }

        private static bool IsApplicationType(Type type) => type.GetTypeInfo().Namespace.StartsWith("SampleApplication");
    }

    public interface IUserService { }

    public class AspNetUserService : IUserService { }
}
