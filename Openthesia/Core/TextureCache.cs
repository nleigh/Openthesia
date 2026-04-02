using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Veldrid.ImageSharp;

namespace Openthesia.Core
{
    public static class TextureCache
    {
        private const int MaxCachedTextures = 200;

        private static Dictionary<string, (IntPtr ptr, Veldrid.Texture tex, long lastAccess)> _cache = new();
        private static ConcurrentDictionary<string, bool> _loading = new();
        private static ConcurrentQueue<(string filePath, ImageSharpTexture image)> _bindingQueue = new();
        private static long _accessCounter = 0;

        public static IntPtr GetTexture(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return IntPtr.Zero;

            // Process bindings (since GetTexture runs in UI loop it is thread-safe)
            while (_bindingQueue.TryDequeue(out var readyData))
            {
                if (!_cache.ContainsKey(readyData.filePath))
                {
                    // Evict oldest if at capacity
                    if (_cache.Count >= MaxCachedTextures)
                    {
                        EvictOldest();
                    }

                    try
                    {
                        var deviceTexture = readyData.image.CreateDeviceTexture(Program._gd, Program._gd.ResourceFactory);
                        var ptr = Program._controller.GetOrCreateImGuiBinding(Program._gd.ResourceFactory, deviceTexture);
                        _cache[readyData.filePath] = (ptr, deviceTexture, _accessCounter++);
                    }
                    catch { }
                }
            }

            if (_cache.TryGetValue(filePath, out var cachedData))
            {
                // Update access time for LRU tracking
                _cache[filePath] = (cachedData.ptr, cachedData.tex, _accessCounter++);
                return cachedData.ptr;
            }

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

        private static void EvictOldest()
        {
            // Find and remove the least recently used texture
            string oldestKey = null;
            long oldestAccess = long.MaxValue;

            foreach (var kvp in _cache)
            {
                if (kvp.Value.lastAccess < oldestAccess)
                {
                    oldestAccess = kvp.Value.lastAccess;
                    oldestKey = kvp.Key;
                }
            }

            if (oldestKey != null && _cache.TryGetValue(oldestKey, out var evicted))
            {
                try { evicted.tex.Dispose(); } catch { }
                _cache.Remove(oldestKey);
                _loading.TryRemove(oldestKey, out _);
            }
        }

        public static void ClearCache()
        {
            foreach (var entry in _cache.Values)
            {
                try { entry.tex.Dispose(); } catch { }
            }
            _cache.Clear();
            _loading.Clear();
            _bindingQueue.Clear();
        }
    }
}
