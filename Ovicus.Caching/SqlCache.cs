/*

The MIT License (MIT)
http://opensource.org/licenses/MIT

Copyright (c) 2013 Lester Sánchez (lester@ovicus.com)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.

*/

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Runtime.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Ovicus.Caching
{
    public class SqlCache : ObjectCache
    {
        private string name;
        private string connectionString;
        private string tableName = "Cache"; // Default table name
        private static readonly TimeSpan OneDay = new TimeSpan(24, 0, 0); // 1 day

        public SqlCache(string connectionString) : this("Default", connectionString) { }

        public SqlCache(string name, string connectionString)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("A valid connection string is required", "connectionString");

            this.connectionString = connectionString;
            this.name = name;
        }

        public SqlCache(string name, string connectionString, string tableName)
            : this(name, connectionString)
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentException("A valid name for the cache table should be provided", "tableName");
            
            this.tableName = tableName;
        }

        private static byte[] Serialize(object value)
        {
            IFormatter formatter = new BinaryFormatter();
            MemoryStream stream = new MemoryStream();
            formatter.Serialize(stream, value);
            byte[] serializedObj = stream.GetBuffer();
            stream.Close();
            return serializedObj;
        }

        private static object Deserialize(byte[] binaryObj)
        {
            IFormatter formatter = new BinaryFormatter();
            MemoryStream stream = new MemoryStream(binaryObj);
            var obj = formatter.Deserialize(stream);
            stream.Close();
            return obj;
        }

        private void InsertEntry(string key, object value, CacheItemPolicy policy)
        {
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                // Add object to cache
                SqlCommand cmdIns = new SqlCommand();
                cmdIns.Connection = con;
                
                const string cmdText = "INSERT INTO {0} ([Key], [Value], Created, LastAccess, SlidingExpirationTimeInMinutes, AbsoluteExpirationTime, ObjectType) " +
                                       "VALUES (@Key, @Value, @Created, @LastAccess, @SlidingExpirationTimeInMinutes, @AbsoluteExpirationTime, @ObjectType)";
                cmdIns.CommandText = string.Format(cmdText, tableName);

                cmdIns.Parameters.AddWithValue("@Key", key);
                cmdIns.Parameters.AddWithValue("@Created", DateTime.Now);
                cmdIns.Parameters.AddWithValue("@LastAccess", DateTime.Now);
                cmdIns.Parameters.AddWithValue("@ObjectType", value.GetType().FullName);
                cmdIns.Parameters.AddWithValue("@AbsoluteExpirationTime", DBNull.Value);
                cmdIns.Parameters.AddWithValue("@SlidingExpirationTimeInMinutes", DBNull.Value);

                SetExpirationValues(cmdIns, policy);

                // Serialize value
                byte[] serializedObj = Serialize(value);
                cmdIns.Parameters.AddWithValue("@Value", serializedObj);

                con.Open();
                cmdIns.ExecuteNonQuery();
            }
        }

        private void UpdateEntry(string key, object value, CacheItemPolicy policy)
        {
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                // Replace (UPDATE) object in DB
                SqlCommand cmdUpd = new SqlCommand();
                cmdUpd.Connection = con;
                
                const string cmdText = "UPDATE {0} SET [Value]=@Value, Created=@Created, LastAccess=@LastAccess, " +
                                       "SlidingExpirationTimeInMinutes=@SlidingExpirationTimeInMinutes, AbsoluteExpirationTime=@AbsoluteExpirationTime, " +
                                       "ObjectType=@ObjectType WHERE [Key] = @Key";
                cmdUpd.CommandText = string.Format(cmdText, tableName);

                cmdUpd.Parameters.AddWithValue("@Key", key);
                cmdUpd.Parameters.AddWithValue("@Created", DateTime.Now);
                cmdUpd.Parameters.AddWithValue("@LastAccess", DateTime.Now);
                cmdUpd.Parameters.AddWithValue("@ObjectType", value.GetType().FullName);
                cmdUpd.Parameters.AddWithValue("@AbsoluteExpirationTime", DBNull.Value);
                cmdUpd.Parameters.AddWithValue("@SlidingExpirationTimeInMinutes", DBNull.Value);

                SetExpirationValues(cmdUpd, policy);

                // Serialize value
                byte[] serializedObj = Serialize(value);
                cmdUpd.Parameters.AddWithValue("@Value", serializedObj);

                con.Open();
                cmdUpd.ExecuteNonQuery();
            }
        }

        private void SetExpirationValues(SqlCommand cmd, CacheItemPolicy policy)
        {
            if (policy.AbsoluteExpiration.Ticks > 0)
            {
                cmd.Parameters["@AbsoluteExpirationTime"].Value = policy.AbsoluteExpiration.DateTime;
            }
            else if (policy.SlidingExpiration.Ticks > 0)
            {
                cmd.Parameters["@SlidingExpirationTimeInMinutes"].Value = policy.SlidingExpiration.TotalMinutes;
            }
            else // Set default absolute expiration time
            {
                cmd.Parameters["@AbsoluteExpiration"].Value = DateTime.Now.Add(OneDay);
            }
        }

        public override object AddOrGetExisting(string key, object value, CacheItemPolicy policy, string regionName = null)
        {
            var obj = Get(key, regionName); // Try to get existing item
            if (obj != null) return obj;
            Set(key, value, policy, regionName); // Insert or Update expired
            return null;
        }

        public override CacheItem AddOrGetExisting(CacheItem value, CacheItemPolicy policy)
        {
            var result = AddOrGetExisting(value.Key, value.Value, policy, value.RegionName);
            return result != null ? new CacheItem(value.Key, result, value.RegionName) : null;
        }

        public override object AddOrGetExisting(string key, object value, DateTimeOffset absoluteExpiration, string regionName = null)
        {
            var policy = new CacheItemPolicy();
            policy.AbsoluteExpiration = absoluteExpiration;
            return AddOrGetExisting(key, value, policy, regionName);
        }

        public override bool Contains(string key, string regionName = null)
        {
            return Get(key, regionName) != null;
        }

        public override CacheEntryChangeMonitor CreateCacheEntryChangeMonitor(IEnumerable<string> keys, string regionName = null)
        {
            return null;
        }

        public override DefaultCacheCapabilities DefaultCacheCapabilities
        {
            get
            {
                return DefaultCacheCapabilities.AbsoluteExpirations | DefaultCacheCapabilities.OutOfProcessProvider;
            }
        }

        public override object Get(string key, string regionName = null)
        {
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                // Try to get object from cache
                bool validCache = false;
                SqlCommand cmdSel = new SqlCommand();
                cmdSel.Connection = con;
                
                string cmdText = "SELECT [Value], Created, LastAccess, SlidingExpirationTimeInMinutes, AbsoluteExpirationTime FROM {0} WHERE [Key] = @Key";
                cmdSel.CommandText = string.Format(cmdText, tableName);

                cmdSel.Parameters.AddWithValue("@Key", key);
                con.Open();

                var reader = cmdSel.ExecuteReader();
                if (reader.Read()) // Object exists in cache
                {
                    // Check whether chache expired or not
                    var absExpirationTime = reader["AbsoluteExpirationTime"];
                    var slidingExpirationMinutes = reader["SlidingExpirationTimeInMinutes"];

                    // Check for absolute expiration date
                    if (absExpirationTime != null)
                    {
                        var expiration = (DateTime)absExpirationTime;
                        var created = (DateTime)reader["Created"];
                        validCache = DateTime.Now.CompareTo(expiration) < 0;
                    }
                    // Check for sliding expiration date
                    else if (slidingExpirationMinutes != null)
                    {
                        var minutes = (long)slidingExpirationMinutes;
                        var lastAccess = (DateTime)reader["LastAccess"];
                        validCache = DateTime.Now.CompareTo(lastAccess.AddMinutes(minutes)) < 0;
                    }

                    if (validCache) // Object in cache is valid
                    {
                        var binaryObj = (byte[])reader["Value"];

                        if (!reader.IsClosed) reader.Close();

                        // Update LastAccess in DB
                        SqlCommand cmdUpd = new SqlCommand();
                        cmdUpd.Connection = con;

                        cmdText = "UPDATE {0} SET LastAccess=@LastAccess WHERE [Key] = @Key";
                        cmdUpd.CommandText = string.Format(cmdText, tableName);

                        cmdUpd.Parameters.AddWithValue("@Key", key);
                        cmdUpd.Parameters.AddWithValue("@LastAccess", DateTime.Now);

                        cmdUpd.ExecuteNonQuery();

                        // Deserialize value
                        return Deserialize(binaryObj);
                    }
                }
            }
            return null;
        }

        public override CacheItem GetCacheItem(string key, string regionName = null)
        {
            var obj = Get(key, regionName);
            return obj != null ? new CacheItem(key, obj, regionName) : null;
        }

        public override long GetCount(string regionName = null)
        {
            // Count valid (not expired) items in cache
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                SqlCommand cmdSel = new SqlCommand();
                cmdSel.Connection = con;

                const string cmdText = "SELECT COUNT([Key]) FROM {0} WHERE (AbsoluteExpirationTime > GETDATE()) OR (AbsoluteExpirationTime = NULL AND SlidingExpirationTimeInMinutes <> NULL)";
                cmdSel.CommandText = string.Format(cmdText, tableName);

                con.Open();

                // TODO: Filter items with sliding expiration time

                var count = (long)cmdSel.ExecuteScalar();
                return count;
            }
        }

        protected override IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            // Retrieve valid (not expired) items in cache
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                SqlCommand cmdSel = new SqlCommand();
                cmdSel.Connection = con;

                const string cmdText = "SELECT [Key], [Value] FROM {0} WHERE (AbsoluteExpirationTime > GETDATE()) OR (AbsoluteExpirationTime = NULL AND SlidingExpirationTimeInMinutes <> NULL)";
                cmdSel.CommandText = string.Format(cmdText, tableName);

                con.Open();

                // TODO: Filter items with sliding expiration time

                var reader = cmdSel.ExecuteReader();
                while (reader.Read())
                {
                    yield return new KeyValuePair<string, object>((string)reader["Key"], reader["Value"]);
                }
            }
        }

        public override IDictionary<string, object> GetValues(IEnumerable<string> keys, string regionName = null)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            foreach (var key in keys)
            {
                result[key] = Get(key);
            }
            return result;
        }

        public override string Name
        {
            get { return name; }
        }

        public override object Remove(string key, string regionName = null)
        {
            var obj = Get(key);
            Remove(key);
            return obj;
        }

        private void Remove(string key)
        {
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                SqlCommand cmdDel = new SqlCommand();
                cmdDel.Connection = con;

                const string cmdText = "DELETE FROM {0} WHERE [Key] = @Key";
                cmdDel.CommandText = string.Format(cmdText, tableName);

                cmdDel.Parameters.AddWithValue("@Key", key);
                con.Open();
                cmdDel.ExecuteNonQuery();
            }
        }

        public override void Set(string key, object value, CacheItemPolicy policy, string regionName = null)
        {
            if (Contains(key, regionName)) UpdateEntry(key, value, policy);
            else InsertEntry(key, value, policy);
        }

        public override void Set(CacheItem item, CacheItemPolicy policy)
        {
            Set(item.Key, item.Value, policy);
        }

        public override void Set(string key, object value, DateTimeOffset absoluteExpiration, string regionName = null)
        {
            var policy = new CacheItemPolicy();
            policy.AbsoluteExpiration = absoluteExpiration;
            Set(key, value, policy, regionName);
        }

        public override object this[string key]
        {
            get
            {
                return Get(key);
            }
            set
            {
                Set(key, value, DateTime.Now.Add(OneDay));
            }
        }

        /// <summary>
        /// Remove expired entries from the cache table
        /// </summary>
        public void Flush()
        {
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                SqlCommand cmdDel = new SqlCommand();
                cmdDel.Connection = con;

                // TODO: Remove items with sliding expiration time
                
                const string cmdText = "DELETE FROM {0} WHERE (AbsoluteExpirationTime <= GETDATE())";
                cmdDel.CommandText = string.Format(cmdText, tableName);

                con.Open();
                cmdDel.ExecuteNonQuery();
            }
        }
    }
}
