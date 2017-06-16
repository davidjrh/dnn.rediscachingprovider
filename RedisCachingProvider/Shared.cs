using System;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using DotNetNuke.Common.Utilities;
using System.Configuration;
using DotNetNuke.Instrumentation;
using StackExchange.Redis;


namespace DotNetNuke.Providers.RedisCachingProvider
{
    internal static class Shared
    {
        internal const bool DefaultUseCompression = false;

        internal static string GetProviderConfigAttribute(string providerName, string attributeName, string defaultValue = "")
        {
            var provider = Config.GetProvider(providerName == RedisCachingProvider.ProviderName ? "caching" : "outputCaching", providerName);
            if (provider != null && provider.Attributes.AllKeys.Contains(attributeName))
                return provider.Attributes[attributeName];
            return defaultValue;
        }

        internal static string ConnectionString
        {
            get
            {
                var cs = ConfigurationManager.ConnectionStrings["RedisCachingProvider"];
                if (cs == null || string.IsNullOrEmpty(cs.ConnectionString))
                {
                    throw new ConfigurationErrorsException(
                        "The Redis connection string can't be an empty string. Check the RedisCachingProvider connectionString attribute in your web.config file.");
                }
                return cs.ConnectionString;
            }
        }



        internal static string Serialize(object source)
        {
            IFormatter formatter = new BinaryFormatter();
            var stream = new MemoryStream();
            formatter.Serialize(stream, source);
            return Convert.ToBase64String(stream.ToArray());
        }

        internal static T Deserialize<T>(string base64String)
        {
            var stream = new MemoryStream(Convert.FromBase64String(base64String));
            IFormatter formatter = new BinaryFormatter();
            stream.Position = 0;
            return (T)formatter.Deserialize(stream);
        }

        internal static byte[] SerializeXmlBinary(object obj)
        {
            using (var ms = new MemoryStream())
            {
                using (var wtr = XmlDictionaryWriter.CreateBinaryWriter(ms))
                {
                    var serializer = new NetDataContractSerializer();
                    serializer.WriteObject(wtr, obj);
                    ms.Flush();
                }
                return ms.ToArray();
            }
        }
        internal static object DeSerializeXmlBinary(byte[] bytes)
        {
            using (var rdr = XmlDictionaryReader.CreateBinaryReader(bytes, XmlDictionaryReaderQuotas.Max))
            {
                var serializer = new NetDataContractSerializer { AssemblyFormat = FormatterAssemblyStyle.Simple };
                return serializer.ReadObject(rdr);
            }
        }
        internal static byte[] CompressData(object obj)
        {
            byte[] inb = SerializeXmlBinary(obj);
            byte[] outb;
            using (var ostream = new MemoryStream())
            {
                using (var df = new DeflateStream(ostream, CompressionMode.Compress, true))
                {
                    df.Write(inb, 0, inb.Length);
                }
                outb = ostream.ToArray();
            }
            return outb;
        }

        internal static object DecompressData(byte[] inb)
        {
            byte[] outb;
            using (var istream = new MemoryStream(inb))
            {
                using (var ostream = new MemoryStream())
                {
                    using (var sr =
                        new DeflateStream(istream, CompressionMode.Decompress))
                    {
                        sr.CopyTo(ostream);
                    }
                    outb = ostream.ToArray();
                }
            }
            return DeSerializeXmlBinary(outb);
        }

        internal static void ClearRedisCache(IDatabase redisCache, string cacheKeyPattern)
        {
            var script = "for _,k in ipairs(redis.call('keys', ARGV[1])) do redis.call('del', k) end";
            redisCache.ScriptEvaluate(script, null, new RedisValue[] { cacheKeyPattern });
        }

        internal static bool ProcessException(string providerName, Exception e, string key = "", object value = null)
        {
            try
            {
                if (!bool.Parse(GetProviderConfigAttribute(providerName, "silentMode", "false")))
                    return false;

                if (e.GetType() != typeof(ConfigurationErrorsException) && value != null)
                {
                    Logger.Error(
                        string.Format("Error while trying to store in cache the key {0} (Object type: {1}): {2}", key,
                            value.GetType(), e), e);
                }
                else
                {
                    Logger.Error(e.ToString());
                }
                return true;
            }
            catch (Exception)
            {
                // If the error can't be logged, allow the caller to raise the exception or not
                // so do nothing
                return false;
            }
        }

        internal static ILog _logger;
        internal static ILog Logger => _logger ?? (_logger = LoggerSource.Instance.GetLogger(typeof(RedisCachingProvider)));


    }
}
