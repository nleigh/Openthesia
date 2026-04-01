using System;
using System.Collections.Concurrent;
using System.IO;
using Veldrid.ImageSharp;
using Openthesia.Core;

namespace Openthesia.Core
{
    public static class TextureCache
    {
        private static ConcurrentDictionary<string, IntPtr> _cache = new ConcurrentDictionary<string, IntPtr>();
        private static ConcurrentDictionary<string, bool> _loading = new ConcurrentDictionary<string, bool>();
        private static ConcurrentQueue<(string filePath, ImageSharpTexture image)> _bindingQueue = new ConcurrentQueue<(string, ImageSharpTexture)>();

        public static IntPtr GetTexture(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return IntPtr.Zero;

            // Process bindings (since GetTexture runs in UI loop it is thread-safe)
            while (_bindingQueue.TryDequeue(out var readyData))
            {
                if (!_cache.ContainsKey(readyData.filePath))
                {
                    try
                    {
                        var deviceTexture = readyData.image.CreateDeviceTexture(Program._gd, Program._gd.ResourceFactory);
                        var ptr = Program._controller.GetOrCreateImGuiBinding(Program._gd.ResourceFactory, deviceTexture);
                        _cache[readyData.filePath] = ptr;
                    }
                    catch { }
                }
            }

            if (_cache.TryGetValue(filePath, out IntPtr textureId))
                return textureId;

            if (_loading.TryAdd(filePath, true))
            {
                Task.Run(() =>
                {
                    try
                    {
                        using var stream = File.OpenRead(filePath);
                        var img = new ImageSharpTexture(stream);
                        _bindingQueue.Enqueue((filePath, img));
                    }
                    catch { }
                });
            }

            return IntPtr.Zero;
        }

        public static void ClearCache()
        {
            _cache.Clear();
            _loading.Clear();
            _bindingQueue.Clear();
        }
    }
}
