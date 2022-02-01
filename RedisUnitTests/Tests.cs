using System;
using System.Runtime.Serialization;
using DotNetNuke.Providers.RedisCachingProvider;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RedisUnitTests
{
    [TestClass]
    public class Tests
    {
        [TestMethod]
        public void CreateProviderInstance()
        {
            var cache = new RedisCachingProvider();
            Assert.IsNotNull(cache);
        }

        [TestMethod]
        public void ItemDoesNotExist()
        {
            var cache = new RedisCachingProvider();
            var item = cache.GetItem("ItemDoesNotExist");
            Assert.IsNull(item);
        }

        [TestMethod]
        public void InsertAndRetrieveItem()
        {
            var cache = new RedisCachingProvider();
            cache.Insert("MyItem", "MyContent");
            var item = cache.GetItem("MyItem");
            Assert.AreEqual(item, "MyContent");
        }

        [TestMethod]
        public void RemoveItem()
        {
            var cache = new RedisCachingProvider();
            cache.Insert("MyRemoveItem", "MyContent");
            var item = cache.GetItem("MyRemoveItem");
            Assert.IsNotNull(item);
            cache.Remove("MyRemoveItem");
            item = cache.GetItem("MyRemoveItem");
            Assert.IsNull(item);
        }

        [TestMethod]
        public void ClearCache()
        {
            var cache = new RedisCachingProvider();
            cache.Insert("MyItem1", "MyContent1");
            cache.Insert("MyItem2", "MyContent2");
            var item1 = cache.GetItem("MyItem1");
            var item2 = cache.GetItem("MyItem2");
            Assert.AreEqual(item1, "MyContent1");
            Assert.AreEqual(item2, "MyContent2");
            cache.Clear("Prefix", "");
            item1 = cache.GetItem("MyItem1");
            item2 = cache.GetItem("MyItem2");
            Assert.IsNull(item1);
            Assert.IsNull(item2);
        }

        [TestMethod]
        public void NonSerializableObject()
        {
            Exception serialEx = null;
            try
            {
                var obj = new object();// new NonSerializableClass();
                var cache = new RedisCachingProvider();
                cache.Insert("MyItem1", obj); // silentMode=false on app.config                
            }
            catch (SerializationException ex)
            {
                serialEx = ex;
            }
            Assert.IsNotNull(serialEx);
        }
    }
}
