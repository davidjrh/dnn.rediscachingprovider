using System;
using System.Diagnostics;
using System.Globalization;
using System.Web;
using System.Web.Caching;
using DotNetNuke.Services.Cache;
using StackExchange.Redis;

namespace DotNetNuke.Providers.RedisCachingProvider
{
    public class RedisCachingProvider: CachingProvider
	{

		#region Private Members

		private const string ProviderName = "RedisCachingProvider";

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
					_keyPrefix = string.Format("{0}_", Shared.GetProviderConfigAttribute(ProviderName, "keyPrefix", hostGuid));
				}
				return _keyPrefix;
			} 
		}

		private static readonly Lazy<ConnectionMultiplexer> LazyConnection = new Lazy<ConnectionMultiplexer>(() =>
		{
			var cn = ConnectionMultiplexer.Connect(Shared.ConnectionString);
			cn.GetSubscriber()
				.Subscribe(new RedisChannel(KeyPrefix + "Redis.*", RedisChannel.PatternMode.Pattern),
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
				var instance = (RedisCachingProvider) Instance() ?? new RedisCachingProvider();
				if (redisChannel == KeyPrefix + "Redis.Clear")
				{
				    var values = redisValue.ToString().Split(':');
					if (values.Length == 3 && values[0] != InstanceUniqueId) // Avoid to clear twice
					{
						instance.Clear(values[1], values[2], false);                        
					}
				}
				else
				{
				    if (redisValue.ToString().Length > InstanceUniqueId.Length &&
				        !redisValue.ToString().StartsWith(InstanceUniqueId))
				    {
						instance.Remove(redisValue.ToString().Substring(InstanceUniqueId.Length + 1), false);                     
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

        #endregion

        #region Abstract implementation
        public override void Insert(string key, object value, DNNCacheDependency dependency, DateTime absoluteExpiration, TimeSpan slidingExpiration, CacheItemPriority priority,
									CacheItemRemovedCallback onRemoveCallback)
		{
			try
			{
				// Calculate expiry 
				TimeSpan? expiry = null;
				if (absoluteExpiration != DateTime.MinValue)
				{
					expiry = absoluteExpiration.Subtract(DateTime.UtcNow);
				}
				else
				{
					if (slidingExpiration != TimeSpan.Zero)
					{
						expiry = slidingExpiration;
					}
				}         

				if (UseCompression)
				{
					var cvalue = Shared.CompressData(value);
					base.Insert(key, cvalue, dependency, absoluteExpiration, slidingExpiration,
								priority, onRemoveCallback);
					RedisCache.StringSet(KeyPrefix + key, Shared.Serialize(cvalue), expiry);
				}
				else
				{
					base.Insert(key, value, dependency, absoluteExpiration, slidingExpiration,
								priority, onRemoveCallback);
					RedisCache.StringSet(KeyPrefix + key, Shared.Serialize(value), expiry);                    
				}
			}
			catch (Exception e)
			{
				if (!Shared.ProcessException(ProviderName, e, key, value)) throw;
			}

		}


		/// <summary>
		/// Gets the item. Note: sliding expiration not implemented to avoid too many requests to the redis server
		/// (see http://stackoverflow.com/questions/20280316/absolute-and-sliding-caching-in-redis)
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns></returns>
		public override object GetItem(string key)
		{
			try
			{
				var v = base.GetItem(key);
				if (v != null)
				{
					return v;
				}
				var value = RedisCache.StringGet(KeyPrefix + key);
				if (value.HasValue)
				{
					var ttl = RedisCache.KeyTimeToLive(KeyPrefix + key);
					var v2 = Shared.Deserialize<object>(value);
					if (UseCompression)
						v2 = Shared.DecompressData((byte[]) v2);
					if (ttl.HasValue && ttl.Value.Days < 30)
					{
						base.Insert(key, v2, (DNNCacheDependency) null, DateTime.UtcNow.Add(ttl.Value), TimeSpan.Zero,
							CacheItemPriority.Normal, null);
					}
					else
					{
						base.Insert(key, v2);
					}
					return v2;
				}
			}
			catch (Exception e)
			{
				if (!Shared.ProcessException(ProviderName, e)) throw;
			}
			return null;
		}


		public override void Clear(string type, string data)
		{
			Clear(type, data, true);
		}

		internal void Clear(string type, string data, bool notifyRedis)
		{
			try
			{
				Shared.Logger.Info($"{InstanceUniqueId} - Clearing local cache (type:{type}; data:{data})...");                
				// Clear internal cache
				ClearCacheInternal(type, data, true);

				if (notifyRedis) // Avoid recursive calls
				{
                    Shared.Logger.Info($"{InstanceUniqueId} - Clearing Redis cache...");				
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
					var keys = server.Keys(RedisCache.Database, pattern: KeyPrefix + "*", pageSize: 10000);
					foreach (var key in keys)
					{
						RedisCache.KeyDelete(key);
					}
                    Shared.Logger.Info($"{InstanceUniqueId} - Notifying cache clearing to other partners...");
					// Notify the channel
                    RedisCache.Publish(new RedisChannel(KeyPrefix + "Redis.Clear", RedisChannel.PatternMode.Auto), $"{InstanceUniqueId}:{type}:{data}");
                }
			}
			catch (Exception e)
			{
				if (!Shared.ProcessException(ProviderName, e)) throw;
			}
		}

		public override void Remove(string key)
		{
			Remove(key, true);
		}

		internal void Remove(string key, bool notifyRedis)
		{
			try
			{
                Shared.Logger.Info($"{InstanceUniqueId} - Removing cache key {key}...");			
				// Remove from the internal cache
				RemoveInternal(key);

				if (notifyRedis)
				{
                    Shared.Logger.Info($"{InstanceUniqueId} - Removing cache key {key} from Redis...");				
					// Remove from Redis cache
					RedisCache.KeyDelete(KeyPrefix + key);

                    Shared.Logger.Info($"{InstanceUniqueId} - Telling other partners to remove cache key {key}...");                    
					// Notify the channel
					RedisCache.Publish(new RedisChannel(KeyPrefix + "Redis.Remove", RedisChannel.PatternMode.Auto), $"{InstanceUniqueId}_{key}");
				}
			}
			catch (Exception e)
			{
				if (!Shared.ProcessException(ProviderName, e)) throw;
			}
		}


        #endregion


    }
}
