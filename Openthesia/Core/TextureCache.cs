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

        public static IntPtr GetTexture(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return IntPtr.Zero;

            if (_cache.TryGetValue(filePath, out IntPtr textureId))
                return textureId;

            try
            {
                using var stream = File.OpenRead(filePath);
                var img = new ImageSharpTexture(stream);
                var deviceTexture = img.CreateDeviceTexture(Program._gd, Program._gd.ResourceFactory);
                var ptr = Program._controller.GetOrCreateImGuiBinding(Program._gd.ResourceFactory, deviceTexture);
                
                _cache[filePath] = ptr;
                return ptr;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }
    }
}
