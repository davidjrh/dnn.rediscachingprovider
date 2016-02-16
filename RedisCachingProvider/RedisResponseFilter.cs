using DotNetNuke.Services.OutputCache;
using System;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Caching;

namespace DotNetNuke.Providers.RedisCachingProvider
{
    /// <summary>
    /// RedisResponseFilter implements the OutputCacheRepsonseFilter to capture the response into Redis cache.
    /// </summary>
    public class RedisResponseFilter : OutputCacheResponseFilter
    {
        internal RedisResponseFilter(int itemId, int maxVaryByCount, Stream filterChain, string cacheKey, TimeSpan cacheDuration) : base(filterChain, cacheKey, cacheDuration, maxVaryByCount)
        {
            if (maxVaryByCount > -1 && RedisOutputCachingProvider.GetCacheKeys(itemId).Count >= maxVaryByCount)
            {
                HasErrored = true;
                return;
            }
            CaptureStream = new MemoryStream();
        }

        protected static Cache Cache
        {
            get
            {
                return RedisOutputCachingProvider.Cache;
            }
        }

        protected override void AddItemToCache(int itemId, string output)
        {
            RedisOutputCachingProvider.Instance().SetOutput(itemId, CacheKey, CacheDuration, Encoding.Default.GetBytes(output));
        }

        protected override void RemoveItemFromCache(int itemId)
        {
            RedisOutputCachingProvider.RemoveInternal(itemId, true);
        }
    }
}