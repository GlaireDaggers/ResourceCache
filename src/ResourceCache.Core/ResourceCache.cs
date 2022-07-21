using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

using ResourceCache.Core.FS;

namespace ResourceCache.Core
{
    /// <summary>
    /// Enumeration of possible resource load states
    /// </summary>
    public enum ResourceLoadState
    {
        /// <summary>
        /// Resource is not loaded
        /// </summary>
        Unloaded,

        /// <summary>
        /// Resource is currently being loaded, but has not finished
        /// </summary>
        Loading,

        /// <summary>
        /// Resource has finished loading
        /// </summary>
        Loaded,

        /// <summary>
        /// Resource loading failed
        /// </summary>
        LoadFailed,
    }

    /// <summary>
    /// Represents a handle to a loaded resource, allowing the internal data to change if a hot-reload is performed
    /// </summary>
    /// <typeparam name="TResource">The resource type</typeparam>
    public struct ResourceHandle<TResource>
    {
        /// <summary>
        /// Gets the load state of this resource
        /// </summary>
        public ResourceLoadState State => _resCache.GetLoadState(_path);

        /// <summary>
        /// Gets the resource data. If the resource is not finished loading, this may block on waiting for it to finish
        /// If the resource failed to load, this will throw the exception associated with the load failure
        /// </summary>
        public TResource Value
        {
            get
            {
                var task = _resCache.GetAsync(typeof(TResource), _path);
                if (!task.IsCompleted) task.Wait();

                return (TResource)task.Result;
            }
        }

        /// <summary>
        /// Gets the original path string this resource was loaded from
        /// </summary>
        public string Path => _path;

        private readonly ResourceManager _resCache;
        private readonly string _path;

        internal ResourceHandle(ResourceManager cache, string path)
        {
            _resCache = cache;
            _path = path;
        }

        /// <summary>
        /// Make sure resource starts loading if it is not already loaded
        /// </summary>
        public void Load()
        {
            // ensure that, if this asset is not in the cache, it should start loading
            _ = _resCache.GetAsync(typeof(TResource), _path);
        }
    }

    /// <summary>
    /// Class responsible for loading & caching game resources at runtime
    /// </summary>
    public sealed class ResourceManager
    {
        private struct MountedFS
        {
            public string mountPath;
            public IGameFS fs;
        }

        private struct ResourceLoader
        {
            public bool threadsafe;
            public Func<Stream, object> loadFn;
        }

        private List<MountedFS> _fs = new List<MountedFS>();
        private Dictionary<string, Task<object>> _resCache = new Dictionary<string, Task<object>>();
        private Dictionary<Type, ResourceLoader> _resFactories = new Dictionary<Type, ResourceLoader>();
        private ILogHandler _logHandler;

        public ResourceManager()
        {
            _logHandler = new DefaultLogHandler();
        }

        public ResourceManager(ILogHandler logHandler)
        {
            _logHandler = logHandler;
        }

        /// <summary>
        /// Mount a filesystem implementation
        /// </summary>
        /// <param name="mountPoint">The virtual path to mount this filesystem onto</param>
        /// <param name="fs">The filesystem implementation</param>
        /// <param name="enableHotReload">Whether to enable hot-reloading on this filesystem</param>
        public void Mount(string mountPoint, IGameFS fs, bool enableHotReload = false)
        {
            if (!mountPoint.EndsWith("/"))
            {
                mountPoint += '/';
            }

            _logHandler.LogInfo($"Mounting {fs.GetType().Name} at '{mountPoint}'");
            _fs.Insert(0, new MountedFS
            {
                mountPath = mountPoint,
                fs = fs
            });

            if (enableHotReload)
            {
                fs.OnFileChanged += Fs_OnFileModified;
                fs.OnFileDeleted += Fs_OnFileDeleted;
            }
        }

        private void Fs_OnFileModified(string assetPath)
        {
            // unload asset if it gets modified. further attempts at referencing the asset will force it to re-load

            if (GetLoadState(assetPath) != ResourceLoadState.Unloaded)
            {
                _logHandler.LogInfo($"Content file {assetPath} was modified, hot-reloading");

                lock (_resCache)
                {
                    Unload(assetPath);
                }
            }
        }

        private void Fs_OnFileDeleted(string assetPath)
        {
            // if a resource file is deleted and we still have that content in memory, trigger a warning

            if (GetLoadState(assetPath) != ResourceLoadState.Unloaded)
            {
                _logHandler.LogWarn($"Content file {assetPath} was deleted, but was still present in resource cache");
            }
        }

        /// <summary>
        /// Register a resource factory for the given resource type
        /// </summary>
        /// <typeparam name="TResource">The resource type</typeparam>
        /// <param name="loader">A loader which takes a Stream as input and deserializes a resource instance from it</param>
        /// <param name="allowMultithreading">Whether to allow this loader to be executed on a background thread</param>
        public void RegisterFactory<TResource>(Func<Stream, TResource> loader, bool allowMultithreading = true)
        {
            _logHandler.LogInfo($"Registered factory for resource type {typeof(TResource).Name}");

            Func<Stream, object> loaderFn = (stream) =>
            {
                return loader.Invoke(stream);
            };

            _resFactories.Add(typeof(TResource), new ResourceLoader
            {
                loadFn = loaderFn,
                threadsafe = allowMultithreading
            });
        }

        /// <summary>
        /// Gets the load state of the resource
        /// </summary>
        /// <param name="path">A path to the resource file</param>
        /// <returns>The current load state of the resource</returns>
        public ResourceLoadState GetLoadState(string path)
        {
            lock (_resCache)
            {
                if (!_resCache.ContainsKey(path)) return ResourceLoadState.Unloaded;
                
                var res = _resCache[path];
                
                if (res.IsCanceled || res.IsFaulted) return ResourceLoadState.LoadFailed;
                
                return res.IsCompleted ? ResourceLoadState.Loaded : ResourceLoadState.Loading;
            }
        }

        /// <summary>
        /// Load the resource at the given path and return a handle to it
        /// </summary>
        /// <typeparam name="TResource">The resource to load</typeparam>
        /// <param name="path">The path to the resource</param>
        /// <returns>A handle to the resource</returns>
        public ResourceHandle<TResource> Load<TResource>(string path)
        {
            _ = GetAsync(typeof(TResource), path);
            return new ResourceHandle<TResource>(this, path);
        }

        internal Task<object> GetAsync(Type type, string path)
        {
            lock (_resCache)
            {
                if (_resCache.ContainsKey(path))
                {
                    return _resCache[path];
                }
            }

            if (!_resFactories.ContainsKey(type))
            {
                throw new ResourceException(path, $"No resource factory registered for content type {type.Name}");
            }

            _logHandler.LogInfo($"Loading {type.Name} from '{path}'");

            var factory = _resFactories[type];
            var stream = Open(path, out var fs);
            Task<object> loader;

            if (factory.threadsafe)
            {
                if (!fs.IsThreadSafe)
                {
                    // if the filesystem is not thread safe, we first load the entire contents of the file into a MemoryStream,
                    // and then pass that memorystream off to the loader task instead of the original

                    var memStream = new MemoryStream();
                    stream.CopyTo(memStream);
                    stream.Dispose();
                    memStream.Seek(0, SeekOrigin.Begin);
                    stream = memStream;
                }

                loader = Task.Run(() =>
                {
                    try
                    {
                        var data = factory.loadFn(stream);
                        stream.Dispose();
                        return data;
                    }
                    catch (Exception e)
                    {
                        throw new ResourceException(path, $"Failed loading resource: {e.Message}");
                    }
                });
            }
            else
            {
                // yes this is kinda stupid but ¯\_(ツ)_/¯

                object data = factory.loadFn(stream);
                loader = Task.FromResult(data);
                stream.Dispose();
            }
            
            lock (_resCache)
            {
                _resCache[path] = loader;
            }

            return loader;
        }

        /// <summary>
        /// Unload the resource at the given path, disposing if necessary
        /// </summary>
        /// <param name="path">The resource path to unload</param>
        public void Unload(string path)
        {
            lock (_resCache)
            {
                if (_resCache.TryGetValue(path, out var res))
                {
                    // just force loader to finish
                    res.Wait();

                    if (res.Status == TaskStatus.RanToCompletion)
                    {
                        object data = res.Result;

                        if (data is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }

                    _resCache.Remove(path);
                }
            }
        }

        /// <summary>
        /// Unloads all loaded resources, disposing if necessary
        /// </summary>
        public void UnloadAll()
        {
            lock (_resCache)
            {
                foreach (var kvp in _resCache)
                {
                    // just force loader to finish
                    kvp.Value.Wait();

                    if (kvp.Value.Status == TaskStatus.RanToCompletion)
                    {
                        object data = kvp.Value.Result;

                        if (data is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                }

                _resCache.Clear();
            }
        }

        /// <summary>
        /// Open a stream to the given content file
        /// </summary>
        /// <param name="path">Path to the content file</param>
        /// <returns>A stream object for reading from the file</returns>
        /// <exception cref="FileNotFoundException">Thrown if the file could not be found in any mounted filesystem</exception>
        public Stream Open(string path)
        {
            return Open(path, out var _);
        }

        /// <summary>
        /// Open a stream to the given content file
        /// </summary>
        /// <param name="path">Path to the content file</param>
        /// <param name="fs">Reference to the filesystem this stream was obtained from</param>
        /// <returns>A stream object for reading from the file</returns>
        /// <exception cref="FileNotFoundException">Thrown if the file could not be found in any mounted filesystem</exception>
        public Stream Open(string path, out IGameFS fs)
        {
            foreach (var mountedFS in _fs)
            {
                if (!path.StartsWith(mountedFS.mountPath)) continue;

                string relativePath = path.Substring(mountedFS.mountPath.Length);

                if (!mountedFS.fs.Exists(relativePath)) continue;

                try
                {
                    var stream = mountedFS.fs.OpenRead(relativePath);
                    _logHandler.LogInfo($"Found '{path}' in {mountedFS.fs.GetType().Name} mounted at '{mountedFS.mountPath}'");
                    fs = mountedFS.fs;
                    return stream;
                }
                catch
                {
                }
            }

            throw new FileNotFoundException($"Could not find file in any mounted FS: {path}");
        }
    }
}
