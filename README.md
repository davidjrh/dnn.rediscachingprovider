Redis Caching Provider for DNN Platform
========================

This caching provider allows you to use a Redis cache server/cluster within DNN Platform, using a hybrid in-memory approach to increase cache performance (items are cached in the local memory and on Redis cache), and the publisher/subscriber feature to keep in sync all the in-memory caches from the webfarm. You must use Redis 2.8.17 or higher for an on-premises deployment. The caching provider is also Azure Redis cache compatible and works great when running on Azure Websites environment. 

![Redis Caching Configuration](https://intelequia.blob.core.windows.net/images/RedisCaching.png)

<h3>Quick Start</h3>
<ol>
<li>
Provision a Redis cache to be used by your DNN instance. Perhaps one of the fastest ways to do it is to provision an Azure Redis cache by following the steps described at http://msdn.microsoft.com/en-us/library/dn690516.aspx, remember to provision the DNN instance on the same datacenter location to improve performance. You can also provision your Redis cache on your premises by following instructions provided at http://redis.io/download. The caching provider has been tested with the Win64 Redis port. Note that the DNN Redis Caching provider supports working with a shared Redis cache deployment, so you can reuse the same Redis cache deployment on several DNN websites.
</li>
<li>Download from the https://github.com/davidjrh/dnn.rediscachingprovider/tree/master/Release folder the latest version of the DNN Redis Caching provider</li>
<li>Using the Extensions page of your DNN instance, upload and install the Redis caching provider. Once installed, will be the default caching provider. </li>
<li>Open your web.config file and specify the RedisCachingProvider connection string in the ConnectionStrings section. </li>
</ol>
If you are using Azure Redis cache, your connection string should look like this:

```xml
  <connectionStrings>
    <add name="RedisCachingProvider" 
    connectionString="mycache.redis.cache.windows.net,password={base64password},ssl=True"  
    providerName="DotNetNuke.Providers.RedisCachingProvider" />
  </connectionStrings>
```

If you are using Local Redis cache, your connection string should look like this:
```xml
  <connectionStrings>
    <add name="RedisCachingProvider" 
    connectionString="Localhost"  
    providerName="DotNetNuke.Providers.RedisCachingProvider" />
  </connectionStrings>
```

To know more about the diffrerent connection string options, check https://stackexchange.github.io/StackExchange.Redis/Configuration

<h3>Advanced configuration</h3>
There are some attributes you can use to tweak or debug the caching provider. The initial set of configurable attributes are:
<ul>
<li><i>keyPrefix</i> (default string.Empty): this attribute is used to add a prefix to each key stored on the Redis cache. This can be used to share the Redis cache between different DNN deployments. When no prefix is specified (default empty string), the current DNN Host Guid will be used so by default, the cached keys are partitioned by the Host identifier.</li>
<li><i>useCompression</i> (boolean, default false): before inserting on the Redis cache, the value is compressed in order to save memory. The values are deflated when retrieved from the Redis cache. While using this parameter can save resources on the Redis server has a performance penalty because of the compression operations</li>
<li><i>silentMode</i> (boolean, default true): when the silent mode is set to true and an exception occurs, is logged on the DNN instance log files under "/Portals/_default/Logs" and not raising an exception. Note that the in-memory cache is used before the Redis cache, so the site normally will continue working, but can end in out of sync caches. Keep an eye on the log files to verify that everything is working fine.</li>
</ul>
These attributes must be specified in the caching provider definition element in the web.config, not in the connection string element. Example:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <sectionGroup name="dotnetnuke">
      <section name="caching" requirePermission="false" type="DotNetNuke.Framework.Providers.ProviderConfigurationHandler, DotNetNuke" />
    </sectionGroup>
  </configSections>
  <dotnetnuke>
    <caching defaultProvider="RedisCachingProvider">
      <providers>
        <clear />
        <add name="RedisCachingProvider" 
             type="DotNetNuke.Providers.RedisCachingProvider.RedisCachingProvider, DotNetNuke.Providers.RedisCachingProvider"
             providerPath="~\Providers\CachingProviders\RedisCachingProvider\" 
             silentMode="true"
             useCompression="false" 
             keyPrefix="MyDNNInstance1"/>
      </providers>
    </caching>
  </dotnetnuke>
</configuration>
```

# Building the solution
### Requirements
* Visual Studio 2022 (download from https://www.visualstudio.com/downloads/)
* npm package manager (download from https://www.npmjs.com/get-npm)

### Install package dependencies
From the command line, enter the `<RepoRoot>\RedisCachingProvider\RedisCaching.Web` and run the following commands:
```
  npm install -g webpack
  npm install -g webpack-cli
  npm install -g webpack-dev-server --force
  npm install --force
```

### Debug the client side app
To debug the client side, build the module in debug mode and copy the .dll and .pdb files into your site /bin folder (you can tweak the post build event for such purpose). That will try to load the persona bar bundle script from https://localhost:8080. 

The second step is to start the local webpack dev server. To do it, 
From the command line, enter the `<RepoRoot>\RedisCachingProvider\RedisCaching.Web` and run the following commands:
```
  webpack-dev-server
```

### Build the module
Now you can build the solution by opening the RedisCachingProvider.sln file on Visual Studio. Building the solution in "Release", will generate the React bundle and package it all together with the installation zip file, created under the "\releases" folder.

On the Visual Studio output window you should see something like this:
```
1>------ Rebuild All started: Project: RedisCachingProvider, Configuration: Release Any CPU ------
1>C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\Microsoft.Common.CurrentVersion.targets(1987,5): warning MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.  These reference conflicts are listed in the build log when log verbosity is set to detailed.
1>  RedisCachingProvider -> C:\Dev\dnn.rediscachingprovider\RedisCachingProvider\bin\DotNetNuke.Providers.RedisCachingProvider.dll
1>  Hash: 242886ea43df19ba89fd
1>  Version: webpack 1.13.0
1>  Time: 3565ms
1>         Asset     Size  Chunks             Chunk Names
1>  bundle-en.js  23.6 kB       0  [emitted]  main
1>      + 37 hidden modules
1>  
1>  WARNING in bundle-en.js from UglifyJs
1>  Condition always true [./src/containers/Root.js:2,4]
1>  Dropping unreachable code [./src/containers/Root.js:5,4]
1>  Condition always false [./~/style-loader/addStyles.js:24,0]
1>  Dropping unreachable code [./~/style-loader/addStyles.js:25,0]
1>  Condition always false [./~/style-loader!./~/css-loader!./~/less-loader!./src/components/style.less:10,0]
1>  Dropping unreachable code [./~/style-loader!./~/css-loader!./~/less-loader!./src/components/style.less:12,0]
1>  Side effects in initialization of unused variable update [./~/style-loader!./~/css-loader!./~/less-loader!./src/components/style.less:7,0]
========== Rebuild All: 1 succeeded, 0 failed, 0 skipped ==========
```
