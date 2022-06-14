# ResourceCache

A simple but pretty powerful resource loading system created for games built on .NET which supports async loading, hot-reloading, and mounting virtual filesystems.

## Motivation

ResourceCache was inspired by the same goals which motivated [ContentPipe](https://github.com/GlaireDaggers/ContentPipe)
A big goal of ResourceCache was to be able to supercede the ContentManager class in FNA games (see [The XNA Content Pipeline Is Bad and You Shouldn't Use It](https://flibitijibibo.com/xnacontent.html)). I wanted a similar interface - the ability to just Load<SomeContentType> is extremely convenient - but I wanted to be able to add some extremely useful extensions to fill some gaps it just does not cover. I also wanted something that would be framework agnostic, so it works even outside of FNA games.

## Basic Usage

ResourceCache is super simple to use - you just create a new ResourceManager, mount a Filesystem, add some loaders to it, and start requesting files:

```csharp
ResourceManager resources = new ResourceManager();
resources.Mount("content", new FolderFS("content"), true);

// add a loader for text files
resources.RegisterFactory((stream) =>
{
    using (var reader = new StreamReader(stream))
    {
        return reader.ReadToEnd();
    }
});

// and load an asset
ResourceHandle<string> assetHandle1 = resources.Load<string>("content/test.txt");
```

## ResourceHandle

One really big difference between ResourceCache and the FNA ContentManager is that you don't just get raw content data back. Instead, you get a *handle* to the data. This is for a few reasons: for one, because data loading can be async.
The handle can inform you whether the data has finished loading, or let you force the current thread to wait for the data to finish loading. It is also because ResourceCache supports hot-reloading. If hot-reloading is enabled, and a file changes on disk, this handle will automatically reload the underlying data from scratch. 

For this reason, it's a bad idea to hold a direct reference to the underlying asset data, whenever possible you should keep your references as ResourceHandles. In general, this isn't too bad - just keep track of the ResourceHandle and query the Value property to get at your data at the time you need it.

Keep in mind when you query the Value property, if the content has not finished loading, the current thread will be forced to wait until the resource has loaded. If you would like to avoid this, use the State property.
This property will indicate what load state the resource is in, and can be used to determine whether the resource is unloaded, still loading, finished loading, or failed to load.

## Content streams
    
If you want to just open a stream for a content file and bypass the resource loaders, you can do that too! Just use ResourceManager.Open:

```csharp
using (var stream = resourceCache.Open("content/test.txt"))
using (var reader = new StreamReader(stream))
{
    string data = reader.ReadToEnd();
    Console.Log(data);
}
```
    
## Filesystems

Another cool feature of ResourceCache was actually inspired by PhysFS, and it's that you can mount any number of abstract filesystems. An abstract filesystem is just responsible for determining whether files exist and opening streams to them. ResourceCache comes with FolderFS and ArchiveFS, which should cover most cases. FolderFS just wraps the underlying OS filesystem within a given folder, whereas ArchiveFS wraps a ZIP file and returns entries from it.
    
Another neat feature is that you can mount a filesystem to a "mount point", which is a virtual folder path the filesystem should be located at. So for example, if you have a ZIP file "test.zip" and it contains the file "test.txt", you can mount this file at "content" and as a result the file it contains will be accessible at "content/test.txt" when calling Load.

You can write your own filesystems as well - just implement the IGameFS interface.

## Extras

ResourceCache comes with some helper functions in ResourceCache.Extras which assist in creating your own resource loaders:

```csharp
ResourceUtils.InstallJSONResourceLoader<TData>(this ResourceManager resourceCache, JsonSerializerSettings settings = null); // Registers a resource loader for a type which can be deserialized from JSON using Newtonsoft.JSON
ResourceUtils.InstallBSONResourceLoader<TData>(this ResourceManager resourceCache, JsonSerializerSettings settings = null); // Registers a resource loader for a type which can be deserialized from BSON using Newtonsoft.JSON
```

ResourceCache also comes with some helper functions in ResourceCache.FNA which provide FNA-specific resource loaders:

```csharp
FNAResourceUtils.InstallFNAResourceLoaders(this ResourceManager resourceCache, Game game); // Registers loaders for Effect, Texture2D, TextureCube, and SoundEffect for the given Game instance.
```

## QoiSharp

The included FNA texture loader supports QOI images via [QoiSharp](https://github.com/NUlliiON/QoiSharp), a C#/.NET library for handling QOI images. The code has been modified to support .NET Framework 4.7 and can be found in the QoiSharp folder
