using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Web.Http;
using Dnn.PersonaBar.Library;
using Dnn.PersonaBar.Library.Attributes;
using DotNetNuke.Instrumentation;
using DotNetNuke.Web.Api;
using System.Net;
using DotNetNuke.Entities.Controllers;
using DotNetNuke.Providers.RedisCachingProvider.Components;
using System.Configuration;

namespace DotNetNuke.Providers.RedisCachingProvider.Services
{
    [MenuPermission(Scope = ServiceScope.Host)]
    public class RedisCachingController : PersonaBarApiController
    {
        private static readonly ILog Logger = LoggerSource.Instance.GetLogger(typeof(RedisCachingProviderSettings));

        /// GET: api/RedisCaching/GetSettings
        /// <summary>
        /// Gets the settings
        /// </summary>
        /// <returns>settings</returns>
        [HttpGet]
        public HttpResponseMessage GetSettings()
        {
            try
            {
                var settings = new RedisCachingProviderSettings();
                settings.LoadSettings();
                return Request.CreateResponse(HttpStatusCode.OK, settings);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }


        // POST: api/RedisCaching/UpdateSettings
        /// <summary>
        /// Updates the settings
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage UpdateSettings(RedisCachingProviderSettings settings)
        {
            try
            {
                settings.SaveSettings();

                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }


    }

}
