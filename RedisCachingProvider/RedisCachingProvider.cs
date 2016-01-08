using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Web;
using System.Web.Caching;
using System.Xml;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Instrumentation;
using DotNetNuke.Services.Cache;
using StackExchange.Redis;
using System.Linq;

namespace DotNetNuke.Providers.RedisCachingProvider
{
	public class RedisCachingProvider: CachingProvider
	{

		#region Private Members

		private const string ProviderName = "RedisCachingProvider";
		private const bool DefaultUseCompression = false;
		private const string DefaultPort = "6379";
		private const string SslDefaultPort = "6380";

		private static string GetProviderConfigAttribute(string attributeName, string defaultValue = "")
		{
			var provider = Config.GetProvider("caching", ProviderName);
			if (provider != null && provider.Attributes.AllKeys.Contains(attributeName))
				return provider.Attributes[attributeName];
			return defaultValue;
		}

		private static bool UseCompression
		{
			get { return bool.Parse(GetProviderConfigAttribute("useCompression", DefaultUseCompression.ToString(CultureInfo.InvariantCulture))); }
		}

		private static string ConnectionString
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
					_keyPrefix = string.Format("{0}_", GetProviderConfigAttribute("keyPrefix", hostGuid));
				}
				return _keyPrefix;
			} 
		}


		private static readonly Lazy<ConnectionMultiplexer> LazyConnection = new Lazy<ConnectionMultiplexer>(() =>
		{
			var cn = ConnectionMultiplexer.Connect(ConnectionString);
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
				if (!ProcessException(e)) throw;
			}
		}

		private static IDatabase _redisCache;
		private static IDatabase RedisCache
		{
			get { return _redisCache ?? (_redisCache = Connection.GetDatabase()); }
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
					var cvalue = CompressData(value);
					base.Insert(key, cvalue, dependency, absoluteExpiration, slidingExpiration,
								priority, onRemoveCallback);
					RedisCache.StringSet(KeyPrefix + key, Serialize(cvalue), expiry);
				}
				else
				{
					base.Insert(key, value, dependency, absoluteExpiration, slidingExpiration,
								priority, onRemoveCallback);
					RedisCache.StringSet(KeyPrefix + key, Serialize(value), expiry);                    
				}
			}
			catch (Exception e)
			{
				if (!ProcessException(e, key, value)) throw;
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
					var v2 = Deserialize<object>(value);
					if (UseCompression)
						v2 = DecompressData((byte[]) v2);
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
				if (!ProcessException(e)) throw;
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
				Logger.Info($"{InstanceUniqueId} - Clearing local cache (type:{type}; data:{data})...");                
				// Clear internal cache
				ClearCacheInternal(type, data, true);

				if (notifyRedis) // Avoid recursive calls
				{
					Logger.Info($"{InstanceUniqueId} - Clearing Redis cache...");				
					// Clear Redis cache 
					var hostAndPort = ConnectionString.Split(',')[0];
					if (!hostAndPort.Contains(":"))
					{
						if (hostAndPort.ToLower().Contains(".redis.cache.windows.net"))
							hostAndPort += ":" + SslDefaultPort;
						else
							hostAndPort += ":" + DefaultPort;    
					}
						
					var server = Connection.GetServer(hostAndPort);
					var keys = server.Keys(RedisCache.Database, pattern: KeyPrefix + "*", pageSize: 10000);
					foreach (var key in keys)
					{
						RedisCache.KeyDelete(key);
					}
					Logger.Info($"{InstanceUniqueId} - Notifying cache clearing to other partners...");
					// Notify the channel
                    RedisCache.Publish(new RedisChannel(KeyPrefix + "Redis.Clear", RedisChannel.PatternMode.Auto), $"{InstanceUniqueId}:{type}:{data}");
                }
			}
			catch (Exception e)
			{
				if (!ProcessException(e)) throw;
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
				Logger.Info($"{InstanceUniqueId} - Removing cache key {key}...");			
				// Remove from the internal cache
				RemoveInternal(key);

				if (notifyRedis)
				{
					Logger.Info($"{InstanceUniqueId} - Removing cache key {key} from Redis...");				
					// Remove from Redis cache
					RedisCache.KeyDelete(KeyPrefix + key);

                    Logger.Info($"{InstanceUniqueId} - Telling other partners to remove cache key {key}...");                    
					// Notify the channel
					RedisCache.Publish(new RedisChannel(KeyPrefix + "Redis.Remove", RedisChannel.PatternMode.Auto), InstanceUniqueId + "_" + key);
				}
			}
			catch (Exception e)
			{
				if (!ProcessException(e)) throw;
			}
		}
		
		#endregion

		#region Private methods
		public static string Serialize(object source)
		{
			IFormatter formatter = new BinaryFormatter();
			var stream = new MemoryStream();
			formatter.Serialize(stream, source);
			return Convert.ToBase64String(stream.ToArray());
		}

		public static T Deserialize<T>(string base64String)
		{
			var stream = new MemoryStream(Convert.FromBase64String(base64String));
			IFormatter formatter = new BinaryFormatter();
			stream.Position = 0;
			return (T)formatter.Deserialize(stream);
		}

		public static byte[] SerializeXmlBinary(object obj)
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
		public static object DeSerializeXmlBinary(byte[] bytes)
		{
			using (var rdr = XmlDictionaryReader.CreateBinaryReader(bytes, XmlDictionaryReaderQuotas.Max))
			{
				var serializer = new NetDataContractSerializer { AssemblyFormat = FormatterAssemblyStyle.Simple };
				return serializer.ReadObject(rdr);
			}
		}
		public static byte[] CompressData(object obj)
		{
			byte[] inb = SerializeXmlBinary(obj);
			byte[] outb;
			using (var ostream = new MemoryStream())
			{
				using (var df = new DeflateStream(ostream, CompressionMode.Compress, true))
				{
					df.Write(inb, 0, inb.Length);
				} outb = ostream.ToArray();
			} return outb;
		}

		public static object DecompressData(byte[] inb)
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
					} outb = ostream.ToArray();
				}
			} return DeSerializeXmlBinary(outb);
		}

		private static bool ProcessException(Exception e, string key="", object value = null)
		{
			try
			{
				if (!bool.Parse(GetProviderConfigAttribute("silentMode", "false")))
					return false;

				if (e.GetType() != typeof (ConfigurationErrorsException) && value != null)
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
		private static string InstanceUniqueId
		{
			get
			{
				return KeyPrefix + "Process_" + Process.GetCurrentProcess().Id;
			}
		}

		private static ILog _logger;
		private static ILog Logger => _logger ?? (_logger = LoggerSource.Instance.GetLogger(typeof(RedisCachingProvider)));

	    #endregion
	}
}
