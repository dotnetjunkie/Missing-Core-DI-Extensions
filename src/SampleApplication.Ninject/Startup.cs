using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MissingDIExtensions;
using Ninject;
using Ninject.Activation;
using Ninject.Infrastructure.Disposal;

namespace SampleApplication.Ninject
{
    public class Startup
    {
        private readonly AsyncLocal<Scope> scopeProvider = new AsyncLocal<Scope>();
        private IReadOnlyKernel kernel;

        private object Resolve(Type type) => kernel.Get(type);
        private Scope RequestScope(IContext context) => scopeProvider.Value;

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

            services.AddRequestScopingMiddleware(() => scopeProvider.Value = new Scope());
            services.AddCustomControllerActivation(Resolve);
            services.AddCustomViewComponentActivation(Resolve);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            kernel = RegisterApplicationComponents(app, loggerFactory);

            // Add custom middleware
            app.Use(async (context, next) =>
            {
                await kernel.Get<CustomMiddleware>().Invoke(context, next);
            });

            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
        
        private IReadOnlyKernel RegisterApplicationComponents(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            IKernelConfiguration config = new KernelConfiguration();

            // Register application services
            config.Bind(app.GetControllerTypes()).ToSelf().InScope(RequestScope);

            config.Bind<IUserService>().To<AspNetUserService>().InScope(RequestScope);
            config.Bind<CustomMiddleware>().ToSelf();

            // Cross-wire required framework services
            config.BindToMethod(app.GetRequestService<IHttpContextAccessor>);
            config.Bind<ILoggerFactory>().ToConstant(loggerFactory);

            return config.BuildReadonlyKernel();
        }

        private sealed class Scope : DisposableObject { }
    }

    public static class BindingHelpers
    {
        public static void BindToMethod<T>(this IKernelConfiguration config, Func<T> method) => config.Bind<T>().ToMethod(c => method());
    }

    public interface IUserService { }

    public class AspNetUserService : IUserService { }
}
