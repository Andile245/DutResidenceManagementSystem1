using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Threading;
using DUTResSystemWebApp.Services;

namespace DUTResManagementSystem
{
    public class MvcApplication : System.Web.HttpApplication
    {
        private static Timer electionTimer;
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            // The web host also advances phases while running; each election request performs
            // the same idempotent check so lifecycle enforcement does not depend on this timer.
            electionTimer = new Timer(_ => { try { new ElectionWorkflowService().RunDueWorkflows(); } catch { } }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }
    }
}
