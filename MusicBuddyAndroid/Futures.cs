using AndroidX.Concurrent.Futures;
using IListenableFuture = Google.Common.Util.Concurrent.IListenableFuture;

namespace MusicBuddyAndroid;

// Thin wrapper over CallbackToFutureAdapter (Google's supported ListenableFuture factory;
// ResolvableFuture is "public but internal API" and triggers XAOBS001).
internal static class Futures
{
    private sealed class Resolver : Java.Lang.Object, CallbackToFutureAdapter.IResolver
    {
        private readonly Func<CallbackToFutureAdapter.Completer, Java.Lang.Object> _attach;

        public Resolver(Func<CallbackToFutureAdapter.Completer, Java.Lang.Object> attach)
        {
            _attach = attach;
        }

        public Java.Lang.Object AttachCompleter(CallbackToFutureAdapter.Completer completer)
            => _attach(completer);
    }

    public static IListenableFuture Create(Func<CallbackToFutureAdapter.Completer, Java.Lang.Object> attach)
        => CallbackToFutureAdapter.GetFuture(new Resolver(attach));

    public static IListenableFuture FromResult(Java.Lang.Object result)
        => Create(completer =>
        {
            completer.Set(result);
            return "mb";
        });

    public static IListenableFuture FromTask(Func<Task<Java.Lang.Object>> work)
        => Create(completer =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    completer.Set(await work());
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Error("MusicBuddy", "Callback failed: " + ex);
                    completer.SetException(new Java.Lang.RuntimeException(ex.Message));
                }
            });
            return "mb";
        });
}
