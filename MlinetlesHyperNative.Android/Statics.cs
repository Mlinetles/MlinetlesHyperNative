using AndroidX.AppCompat.App;

namespace MlinetlesHyperNative.Android;

public static class Statics
{
    public static string? CacheDir { get; private set; }

    public static string? PublicDir { get; private set; }

    public static void Initialize(this Activity activity)
    {
        CacheDir = activity.ExternalCacheDir!.AbsolutePath;

        PublicDir = Path.Combine(CacheDir!, "public");

        if (Directory.Exists(PublicDir!)) Directory.Delete(PublicDir!, true);

        Directory.CreateDirectory(PublicDir);

        ShareImpl = async (name, bytes) =>
        {
            var path = Path.Combine(PublicDir!, name);
            await Files.WriteAllBytesAsync(path, bytes);
            var uri = FileProvider.GetUriForFile(activity, activity.PackageName!, new(path));
            var intent = new Intent(Intent.ActionSend);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission);
            intent.SetType(ToMime(name.Split('.')[^1]));
            intent.PutExtra(Intent.ExtraStream, uri);
            activity.StartActivity(Intent.CreateChooser(intent, null as string));
        };

        OpenImpl = async (name, bytes) =>
        {
            var path = Path.Combine(PublicDir!, name);
            await Files.WriteAllBytesAsync(path, bytes);
            var uri = FileProvider.GetUriForFile(activity, activity.PackageName!, new(path));
            var intent = new Intent(Intent.ActionView);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission);
            intent.SetDataAndType(uri, ToMime(name.Split('.')[^1]));
            activity.StartActivity(intent);
        };
    }
}