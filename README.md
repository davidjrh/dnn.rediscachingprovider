Redis Caching Provider for DNN Platform
========================

This caching provider allows to use a Redis cache server or cluster within DNN, using a hybrid in-memory approach to increase cache performance (items are cached in the local memory and on Redis cache). You must use Redis 2.8.17 or higher for an on-premises deployment. The caching provider is also Azure Redis cache compatible. 

<h3>Quick Start</h3>
<ol>
<li>
Provision a Redis cache to be used by your DNN instance. Perhaps one of the fastest ways to do it is to provision an Azure Redis cache by following the steps described at http://msdn.microsoft.com/en-us/library/dn690516.aspx, remember to provision the DNN instance on the same datacenter location to improve performance. You can also provision your Redis cache on your premises by following instructions provided at http://redis.io/download. The caching provider has been tested with the Win64 Redis port. Note that the DNN Redis Caching provider supports working with a shared Redis cache deployment, so you can reuse the same Redis cache deployment on several DNN websites.
</li>
<li>Download from the https://github.com/davidjrh/dnn.rediscachingprovider/tree/master/Release folder the latest version of the DNN Redis Caching provider</li>
<li>Using the Extensions page of your DNN instance, upload and install the Redis caching provider. Once installed, will be the default caching provider. </li>
<li>Open your web.config file and specify the RedisCachingProvider connection string in the ConnectionStrings section. If you are using Azure Redis cache, your connection string should look like this:
</li>
</ol>
```xml
  <connectionStrings>
    <add name="RedisCachingProvider" 
    connectionString="mycache.redis.cache.windows.net,password={base64password},ssl=True"  
    providerName="DotNetNuke.Providers.RedisCachingProvider" />
  </connectionStrings>
```
