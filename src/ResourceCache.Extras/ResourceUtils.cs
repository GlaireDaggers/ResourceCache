using System;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

using ResourceCache.Core;

namespace ResourceCache.Extras
{
    /// <summary>
    /// Set of helpful resource handlers which can be installed into a resource cache
    /// </summary>
    public static class ResourceUtils
    {
        /// <summary>
        /// Install a resource loader for a type which can be deserialized from JSON
        /// </summary>
        /// <typeparam name="TData">The type to be deserialized</typeparam>
        /// <param name="resourceCache">The resource cache to install this loader into</param>
        public static void InstallJSONResourceLoader<TData>(this ResourceManager resourceCache, JsonSerializerSettings settings = null)
        {
            resourceCache.RegisterFactory((stream) =>
            {
                string data;

                using (var reader = new StreamReader(stream))
                {
                    data = reader.ReadToEnd();
                }

                return JsonConvert.DeserializeObject<TData>(data, settings);
            });
        }

        /// <summary>
        /// Install a resource loader for a type which can be deserialized from BSON
        /// </summary>
        /// <typeparam name="TData">The type to be deserialized</typeparam>
        /// <param name="resourceCache">The resource cache to install this loader into</param>
        public static void InstallBSONResourceLoader<TData>(this ResourceManager resourceCache, JsonSerializerSettings settings = null)
        {
            resourceCache.RegisterFactory((stream) =>
            {
                using (var reader = new BsonDataReader(stream))
                {
                    JsonSerializer serializer = JsonSerializer.Create(settings);
                    return serializer.Deserialize<TData>(reader);
                }
            });
        }
    }
}
