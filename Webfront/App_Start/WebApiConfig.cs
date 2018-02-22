using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace Webfront
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services
            var manager = IW4MAdmin.Program.ServerManager;
            manager.Init();
            manager.Start();

            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}
