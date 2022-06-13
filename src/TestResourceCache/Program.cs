using ResourceCache.Core;
using ResourceCache.Core.FS;

namespace TestResourceCache
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ResourceManager resources = new ResourceManager();
            resources.Mount("content", new FolderFS("content"), true);
            resources.Mount("content", new ArchiveFS("test.zip"), true);

            // add a loader for text files
            resources.RegisterFactory((stream) =>
            {
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            });

            ResourceHandle<string> assetHandle1 = resources.Load<string>("content/test.txt");
            ResourceHandle<string> assetHandle2 = resources.Load<string>("content/test2.txt");

            while (true)
            {
                Console.WriteLine("content/test.txt:");
                Console.WriteLine(assetHandle1.Value);
                Console.WriteLine("content/test2.txt:");
                Console.WriteLine(assetHandle2.Value);
                Thread.Sleep(1000);
            }
        }
    }
}