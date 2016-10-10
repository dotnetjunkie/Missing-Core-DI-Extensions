using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SampleApplication.SimpleInjector
{
    // Example of some custom user-defined middleware component.
    public sealed class CustomMiddleware
    {
        private readonly IUserService userService;

        public CustomMiddleware(ILoggerFactory loggerFactory, IUserService userService)
        {
            this.userService = userService;
        }

        public async Task Invoke(HttpContext context, Func<Task> next)
        {
            // Do something before
            await next();
            // Do something after
        }
    }
}