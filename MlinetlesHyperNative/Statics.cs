using MlinetlesHyperNative.ViewModels;
using System.Threading.Tasks;

namespace MlinetlesHyperNative
{
    public static class Statics
    {
        public static StreamGeometry? GetIconFromName(string name)
        {
            Application.Current!.TryGetResource(name, out var icon);
            return icon as StreamGeometry;
        }

        public static bool IsKeyValid(string? key)
        {
            if (key is null) return false;
            Span<byte> bytes = stackalloc byte[33];
            if (!Convert.TryFromBase64String(key, bytes, out var length) || length != 32) return false;
            return true;
        }

        public static Span<byte> FromKeyToBytes(string? key)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var bytes = new Span<byte>(new byte[33]);
            if (!Convert.TryFromBase64String(key, bytes, out var length) || length != 32) throw new ArgumentException("密钥不合法！");
            return bytes.Slice(0, 32);
        }

        public static Span<byte> FromKeyToBytes(string? key, Span<byte> bytes)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (!Convert.TryFromBase64String(key, bytes, out var length) || length != 32) throw new ArgumentException("密钥不合法！");
            return bytes.Slice(0, 32);
        }

        public static string RandomString(Span<byte> bytes)
        {
            RandomNumberGenerator.Create().GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        public static byte[] RandomBytes(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            var bytes = new byte[count];
            RandomNumberGenerator.Create().GetBytes(bytes);
            return bytes;
        }

        public static Span<byte> RandomBytes(Span<byte> bytes)
        {
            RandomNumberGenerator.Create().GetBytes(bytes);
            return bytes;
        }

        public static readonly HttpClient Client = new();

        public static Func<string, byte[], Task>? ShareImpl { get; set; }

        public static Func<string, byte[], Task>? OpenImpl { get; set; }

        public static string ToMime(string name) => name switch
        {
            "jpg" => "image/jpeg",
            "pdf" => "application/pdf",
            _ => "application/octet-stream"
        };

        public enum CryptoOperate : byte
        {
            Download = 0,
            Share = 1,
            Open = 2,
        }
    }
}
