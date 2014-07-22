using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ovicus.Caching.Test
{
    [TestClass]
    public class SqlCacheTest
    {
        private const string connectionString = "Data Source=.;Initial Catalog=CacheDatabase;Integrated Security=True";
        
        [TestMethod]
        public void AddItem()
        {
            string key = "AddItem";
            string data = "data";
            SqlCache cache = new SqlCache(connectionString);
            cache.Add(key, data, DateTime.Now.AddMinutes(30));
            var result = cache.Contains(key);

            Assert.AreEqual(result, true);

            cache.Remove(key); // Clean DB for further testing
        }

        [TestMethod]
        public void RemoveItem()
        {
            string key = "RemoveItem";
            string data = "data";
            SqlCache cache = new SqlCache(connectionString);

            cache.Add(key, data, DateTime.Now.AddMinutes(30));

            cache.Remove(key);
            var result = cache.Contains(key);

            Assert.AreEqual(result, false);
        }

        [TestMethod]
        public void GetItem()
        {
            string key = "GetItem";
            string data = "data";
            SqlCache cache = new SqlCache(connectionString);
            cache.Add(key, data, DateTime.Now.AddMinutes(30));
            var getData = cache.Get(key);

            Assert.AreEqual(data, getData);

            cache.Remove(key); // Clean DB for further testing
        }

        [TestMethod]
        public void AddOrGetExisting()
        {
            string key = "AddOrGetExisting";
            string data = "data";
            SqlCache cache = new SqlCache(connectionString);

            // Try to get data
            var getData = cache.Get(key);
            Assert.IsNull(getData);

            // Add new entry
            getData = cache.AddOrGetExisting(key, data, DateTime.Now.AddMinutes(30));
            Assert.IsNull(getData);

            // Retrieve added entry
            getData = cache.AddOrGetExisting(key, data, DateTime.Now.AddMinutes(30));
            Assert.IsNotNull(getData);

            cache.Remove(key); // Clean DB for further testing
        }
    }
}
