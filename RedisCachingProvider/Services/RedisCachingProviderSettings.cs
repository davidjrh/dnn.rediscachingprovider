using System.Configuration;
using System.Runtime.Serialization;
using System.Web;
using System.Xml;

namespace DotNetNuke.Providers.RedisCachingProvider.Services
{
    [DataContract]
    public class RedisCachingProviderSettings
    {
        public RedisCachingProviderSettings()
        {
            SilentMode = true;
        }

        [DataMember(Name = "connectionString")]
        public string ConnectionString { get; set; }

        [DataMember(Name = "cachingProviderEnabled")]
        public bool CachingProviderEnabled { get; set; }

        [DataMember(Name = "outputCachingProviderEnabled")]
        public bool OutputCachingProviderEnabled { get; set; }

        [DataMember(Name = "useCompression")]
        public bool UseCompression { get; set; }

        [DataMember(Name = "silentMode")]
        public bool SilentMode { get; set; }

        [DataMember(Name = "keyPrefix")]
        public string KeyPrefix { get; set; }


        internal void SaveSettings()
        {
            // Load the web.config file
            var filename = HttpContext.Current.Server.MapPath("~/web.config");
            var webconfig = new ConfigXmlDocument();
            webconfig.Load(filename);

            var node = webconfig.SelectSingleNode("/configuration/connectionStrings/add[@name='RedisCachingProvider']");
            SaveAttribute(node, "connectionString", ConnectionString);

            node = webconfig.SelectSingleNode("/configuration/dotnetnuke/caching");
            SaveAttribute(node, "defaultProvider", CachingProviderEnabled ? "RedisCachingProvider" : "FileBasedCachingProvider");

            node = webconfig.SelectSingleNode("/configuration/dotnetnuke/outputCaching");
            SaveAttribute(node, "defaultProvider", OutputCachingProviderEnabled ? "RedisOutputCachingProvider" : "MemoryOutputCachingProvider");

            node = webconfig.SelectSingleNode("/configuration/dotnetnuke/caching/providers/add[@name='RedisCachingProvider']");
            SaveAttribute(node, "useCompression", UseCompression.ToString());
            SaveAttribute(node, "silentMode", SilentMode.ToString());
            SaveAttribute(node, "keyPrefix", KeyPrefix);

            node = webconfig.SelectSingleNode("/configuration/dotnetnuke/outputCaching/providers/add[@name='RedisOutputCachingProvider']");
            SaveAttribute(node, "useCompression", UseCompression.ToString());
            SaveAttribute(node, "silentMode", SilentMode.ToString());
            SaveAttribute(node, "keyPrefix", KeyPrefix);

            webconfig.Save(filename);

            // Clear the cache
            DotNetNuke.Services.Cache.CachingProvider.Instance().PurgeCache();
        }



        internal void LoadSettings()
        {
            ConnectionString = ConfigurationManager.ConnectionStrings["RedisCachingProvider"]?.ConnectionString;

            // Load the web.config file
            var webconfig = new ConfigXmlDocument();
            webconfig.Load(HttpContext.Current.Server.MapPath("~/web.config"));

            var node = webconfig.SelectSingleNode("/configuration/dotnetnuke/caching");
            CachingProviderEnabled = node?.Attributes["defaultProvider"]?.Value == "RedisCachingProvider";

            node = webconfig.SelectSingleNode("/configuration/dotnetnuke/outputCaching");
            OutputCachingProviderEnabled = node?.Attributes["defaultProvider"]?.Value == "RedisOutputCachingProvider";

            node = webconfig.SelectSingleNode("/configuration/dotnetnuke/caching/providers/add[@name='RedisCachingProvider']");
            UseCompression = bool.Parse(node?.Attributes["useCompression"]?.Value);
            SilentMode = bool.Parse(node?.Attributes["silentMode"]?.Value);
            KeyPrefix = NotNull(node?.Attributes["keyPrefix"]?.Value);
        }


        private void SaveAttribute(XmlNode node, string attributeName, string attributeValue)
        {
            if (node?.Attributes == null) return;

            if (node.Attributes[attributeName] == null)
            {
                node.Attributes.Append(node.OwnerDocument.CreateAttribute(attributeName));
            }
            node.Attributes[attributeName].Value = attributeValue;
        }

        private string NotNull(object value, string defaultValue = "")
        {
            return value == null ? defaultValue : (string) value;
        }

    }
}
