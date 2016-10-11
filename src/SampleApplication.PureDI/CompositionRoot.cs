using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SampleApplication.PureDI.Controllers;

namespace SampleApplication.PureDI
{
    public class CompositionRoot
    {
        private readonly IApplicationBuilder app;
        private readonly ILoggerFactory loggerFactory;

        public CompositionRoot(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            this.app = app;
            this.loggerFactory = loggerFactory;
        }

        public Controller CreateController(Type controllerType)
        {
            var userService = new AspNetUserService();

            if (controllerType == typeof(HomeController))
            {
                return new HomeController(userService);
            }

            throw new InvalidOperationException("Unknown type " + controllerType.FullName);
        }

        public CustomMiddleware CreateCustomMiddleware() => new CustomMiddleware(loggerFactory, new AspNetUserService());
    }
}