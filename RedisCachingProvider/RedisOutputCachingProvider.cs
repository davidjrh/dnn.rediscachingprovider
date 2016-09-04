using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotNetNuke.Services.OutputCache;
using DotNetNuke.Common.Utilities;
using System.Configuration;
using System.Web;
using StackExchange.Redis;
using System.Diagnostics;
using System.Web.Caching;
using System.Collections;
using System.IO;
using System.Globalization;

namespace DotNetNuke.Providers.RedisCachingProvider
{
    public class RedisOutputCachingProvider: OutputCachingProvider
    {
        #region Private Members

        internal const string ProviderName = "RedisOutputCachingProvider";
        protected const string cachePrefix = "DNN_OUTPUT:";
        private static Cache runtimeCache;
        internal static Cache Cache
        {
            get
            {
                //create singleton of the cache object
                if (runtimeCache == null)
                {
                    runtimeCache = HttpRuntime.Cache;
                }
                return runtimeCache;
            }
        }
        internal static RedisOutputCachingProvider Instance()
        {
            return (RedisOutputCachingProvider) Instance(ProviderName);
        }

        internal static string CachePrefix
        {
            get
            {
                return cachePrefix;
            }
        }

        private string GetCacheKey(string CacheKey)
        {
            if (string.IsNullOrEmpty(CacheKey))
            {
                throw new ArgumentException("Argument cannot be null or an empty string", "CacheKey");
            }
            return string.Concat(cachePrefix, CacheKey);
        }


        private static bool UseCompression
        {
            get { return bool.Parse(Shared.GetProviderConfigAttribute(ProviderName, "useCompression", Shared.DefaultUseCompression.ToString(CultureInfo.InvariantCulture))); }
        }

        private static string _keyPrefix;
        private static string KeyPrefix
        {
            get
            {
                if (string.IsNullOrEmpty(_keyPrefix))
                {
                    var hostGuid = string.Empty;
                    if (HttpContext.Current != null)
                    {
                        hostGuid = Entities.Host.Host.GUID;
                    }
                    _keyPrefix = string.Format("{0}_Output_", Shared.GetProviderConfigAttribute(ProviderName, "keyPrefix", hostGuid));
                }
                return _keyPrefix;
            }
        }

        private static readonly Lazy<ConnectionMultiplexer> LazyConnection = new Lazy<ConnectionMultiplexer>(() =>
        {
            var cn = ConnectionMultiplexer.Connect(Shared.ConnectionString);
            cn.GetSubscriber()
                .Subscribe(new RedisChannel(KeyPrefix + "Redis.Output.*", RedisChannel.PatternMode.Pattern),
                    ProcessMessage);
            return cn;
        });
        private static ConnectionMultiplexer Connection
        {
            get
            {
                return LazyConnection.Value;
            }
        }

        private static void ProcessMessage(RedisChannel redisChannel, RedisValue redisValue)
        {
            try
            {
                var instance = (RedisOutputCachingProvider)Instance(ProviderName) ?? new RedisOutputCachingProvider();
                if (redisChannel == KeyPrefix + "Redis.Output.Clear")
                {
                    var values = redisValue.ToString().Split(':');
                    int portalId;
                    if (values.Length == 2 && values[0] != InstanceUniqueId && int.TryParse(values[1], out portalId)) // Avoid to clear twice
                    {
                        PurgeCacheInternal(portalId, false);
                    }
                }
                else
                {
                    if (redisValue.ToString().Length > InstanceUniqueId.Length &&
                        !redisValue.ToString().StartsWith(InstanceUniqueId))
                    {
                        int tabId;
                        if (int.TryParse(redisValue.ToString().Substring(InstanceUniqueId.Length + 1), out tabId))
                        {
                            RemoveInternal(tabId, false);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (!Shared.ProcessException(ProviderName, e)) throw;
            }
        }

        private static IDatabase _redisCache;
        private static IDatabase RedisCache
        {
            get { return _redisCache ?? (_redisCache = Connection.GetDatabase()); }
        }

        private static string InstanceUniqueId
        {
            get
            {
                return $"{KeyPrefix}_{Environment.MachineName}_{Process.GetCurrentProcess().Id.ToString("X").PadLeft(8, '0')}";
            }
        }

        internal static List<string> GetCacheKeys()
        {
            var keys = new List<string>();
            IDictionaryEnumerator CacheEnum = Cache.GetEnumerator();
            while (CacheEnum.MoveNext())
            {
                if (CacheEnum.Key.ToString().StartsWith(string.Concat(cachePrefix)))
                {
                    keys.Add(CacheEnum.Key.ToString());
                }
            }
            return keys;
        }

        internal static List<string> GetCacheKeys(int tabId)
        {
            var keys = new List<string>();
            IDictionaryEnumerator CacheEnum = Cache.GetEnumerator();
            while (CacheEnum.MoveNext())
            {
                if (CacheEnum.Key.ToString().StartsWith(string.Concat(cachePrefix, tabId.ToString(), "_")))
                {
                    keys.Add(CacheEnum.Key.ToString());
                }
            }
            return keys;
        }


        #endregion

        #region Abstract Method Implementation

        public override string GenerateCacheKey(int tabId, System.Collections.Specialized.StringCollection includeVaryByKeys, System.Collections.Specialized.StringCollection excludeVaryByKeys, SortedDictionary<string, string> varyBy)
        {
            return GetCacheKey(base.GenerateCacheKey(tabId, includeVaryByKeys, excludeVaryByKeys, varyBy));
        }

        public override int GetItemCount(int tabId)
        {
            return GetCacheKeys().Count();
        }

        public override byte[] GetOutput(int tabId, string cacheKey)
        {
            return GetOutputInternal(cacheKey);
        }

        internal static byte[] GetOutputInternal(string cacheKey)
        {
            try
            {
                var v = Cache[cacheKey];
                if (v != null)
                {
                    return (byte[])v;
                }
                var value = RedisCache.StringGet(KeyPrefix + cacheKey);
                if (value.HasValue)
                {
                    var ttl = RedisCache.KeyTimeToLive(KeyPrefix + cacheKey);
                    var v2 = Shared.Deserialize<object>(value);
                    if (UseCompression)
                        v2 = Shared.DecompressData((byte[])v2);
                    if (ttl.HasValue && ttl.Value.Days < 30)
                    {
                        Cache.Insert(cacheKey, v2, null, DateTime.UtcNow.Add(ttl.Value), Cache.NoSlidingExpiration, CacheItemPriority.Default, null);
                    }
                    else
                    {
                        Cache.Insert(cacheKey, v2);
                    }
                    return (byte[])v2;
                }
            }
            catch (Exception e)
            {
                if (!Shared.ProcessException(ProviderName, e)) throw;
            }
            return null;
        }

        public override OutputCacheResponseFilter GetResponseFilter(int tabId, int maxVaryByCount, Stream responseFilter, string cacheKey, TimeSpan cacheDuration)
        {
            return new RedisResponseFilter(tabId, maxVaryByCount, responseFilter, cacheKey, cacheDuration);
        }

        public override void PurgeCache(int portalId)
        {
            PurgeCacheInternal(portalId, true);
        }

        internal static void PurgeCacheInternal(int portalId, bool notifyRedis)
        { 
            try
            {
                Shared.Logger.Info($"{InstanceUniqueId} - Clearing local output cache...");
                // Clear internal cache
                foreach (string key in GetCacheKeys())
                {
                    Cache.Remove(key);
                }

                if (notifyRedis) // Avoid recursive calls
                {
                    Shared.Logger.Info($"{InstanceUniqueId} - Clearing Redis output cache...");
                    // Clear Redis cache 
                    var hostAndPort = Shared.ConnectionString.Split(',')[0];
                    if (!hostAndPort.Contains(":"))
                    {
                        if (hostAndPort.ToLower().Contains(".redis.cache.windows.net"))
                            hostAndPort += ":" + Shared.SslDefaultPort;
                        else
                            hostAndPort += ":" + Shared.DefaultPort;
                    }

                    var server = Connection.GetServer(hostAndPort);
                    var keys = server.Keys(RedisCache.Database, pattern: $"{KeyPrefix}{CachePrefix}*", pageSize: 10000);
                    foreach (var key in keys)
                    {
                        RedisCache.KeyDelete(key);
                    }
                    Shared.Logger.Info($"{InstanceUniqueId} - Notifying output cache clearing to other partners...");
                    // Notify the channel
                    RedisCache.Publish(new RedisChannel(KeyPrefix + "Redis.Ouput.Clear", RedisChannel.PatternMode.Auto), $"{InstanceUniqueId}:{portalId}");
                }
            }
            catch (Exception e)
            {
                if (!Shared.ProcessException(ProviderName, e)) throw;
            }

        }

        public override void PurgeExpiredItems(int portalId)
        {
            throw new NotSupportedException();
        }

        public override void Remove(int tabId)
        {
            RemoveInternal(tabId, true);
        }

        internal static void RemoveInternal(int tabId, bool notifyRedis)
        {
            try
            {
                Shared.Logger.Info($"{InstanceUniqueId} - Removing output cache: {tabId}...");
                // Clear internal cache
                foreach (string key in GetCacheKeys(tabId))
                {
                    Cache.Remove(key);
                }

                if (notifyRedis) // Avoid recursive calls
                {
                    Shared.Logger.Info($"{InstanceUniqueId} - Removing Redis output cache: {tabId}...");
                    // Clear Redis cache 
                    var hostAndPort = Shared.ConnectionString.Split(',')[0];
                    if (!hostAndPort.Contains(":"))
                    {
                        if (hostAndPort.ToLower().Contains(".redis.cache.windows.net"))
                            hostAndPort += ":" + Shared.SslDefaultPort;
                        else
                            hostAndPort += ":" + Shared.DefaultPort;
                    }

                    var server = Connection.GetServer(hostAndPort);
                    var keys = server.Keys(RedisCache.Database, pattern: $"{KeyPrefix}{CachePrefix}{tabId}_*", pageSize: 10000);
                    foreach (var key in keys)
                    {
                        RedisCache.KeyDelete(key);
                    }
                    Shared.Logger.Info($"{InstanceUniqueId} - Notifying output cache removal to other partners: {tabId}...");
                    // Notify the channel
                    RedisCache.Publish(new RedisChannel(KeyPrefix + "Redis.Ouput.Remove", RedisChannel.PatternMode.Auto), $"{InstanceUniqueId}:{tabId}");
                }
            }
            catch (Exception e)
            {
                if (!Shared.ProcessException(ProviderName, e)) throw;
            }
        }

        public override void SetOutput(int tabId, string cacheKey, TimeSpan duration, byte[] output)
        {
            try
            {
                // Calculate expiry 
                TimeSpan? expiry = null;
                var absoluteExpiration = DateTime.UtcNow.Add(duration);
                if (absoluteExpiration != DateTime.MinValue)
                {
                    expiry = absoluteExpiration.Subtract(DateTime.UtcNow);
                }
                else
                {
                    expiry = Cache.NoSlidingExpiration;
                }

                if (UseCompression)
                {
                    var cvalue = Shared.CompressData(output);
                    Cache.Insert(cacheKey, cvalue, null, DateTime.UtcNow.Add(duration), Cache.NoSlidingExpiration, CacheItemPriority.Default, null);
                    RedisCache.StringSet(KeyPrefix + cacheKey, Shared.Serialize(cvalue), expiry);
                }
                else
                {
                    Cache.Insert(cacheKey, output, null, DateTime.UtcNow.Add(duration), Cache.NoSlidingExpiration, CacheItemPriority.Default, null);
                    RedisCache.StringSet(KeyPrefix + cacheKey, Shared.Serialize(output), expiry);
                }
            }
            catch (Exception e)
            {
                if (!Shared.ProcessException(ProviderName, e, cacheKey, output)) throw;
            }

        }

        public override bool StreamOutput(int tabId, string cacheKey, HttpContext context)
        {
            var output = GetOutputInternal(cacheKey);
            if (output == null)
            {
                return false;
            }
            context.Response.BinaryWrite(output);
            return true;
        }

        #endregion


    }
}
