using System.Security.Authentication;
using Android.Gms.Auth.Api.SignIn;
using VpnHood.Client.App.Abstractions;
using VpnHood.Client.Device.Droid.Utils;

namespace VpnHood.Client.App.Droid.GooglePlay;

public class GooglePlayAuthenticationService : IAppAuthenticationExternalService
{
    private const int SignInIntentId = 20200;
    private bool _disposed;
    private Activity? _activity;
    private readonly GoogleSignInClient _googleSignInClient;
    private TaskCompletionSource<GoogleSignInAccount>? _taskCompletionSource;

    public GooglePlayAuthenticationService(Activity activity, string firebaseClientId)
    {
        _activity = activity;

        var googleSignInOptions = new GoogleSignInOptions.Builder(GoogleSignInOptions.DefaultSignIn)
            .RequestIdToken(firebaseClientId)
            .RequestEmail()
            .Build();

        _googleSignInClient = GoogleSignIn.GetClient(activity, googleSignInOptions);

        ((IActivityEvent)_activity).OnDestroyEvent += Activity_OnDestroy;
        ((IActivityEvent)_activity).OnActivityResultEvent += Activity_OnActivityResult;
    }

    public static GooglePlayAuthenticationService Create<T>(T activity, string firebaseId) where T : Activity, IActivityEvent
    {
        return new GooglePlayAuthenticationService(activity, firebaseId);
    }

    public async Task<string> SilentSignIn()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var account = await _googleSignInClient.SilentSignInAsync();
        return account?.IdToken ?? throw new AuthenticationException("Could not perform SilentSignIn by Google.");
    }

    public async Task<string> SignIn()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _taskCompletionSource = new TaskCompletionSource<GoogleSignInAccount>();
        _activity?.StartActivityForResult(_googleSignInClient.SignInIntent, SignInIntentId);
        var account = await _taskCompletionSource.Task;

        if (account.IdToken == null)
            throw new ArgumentNullException(account.IdToken);

        return account.IdToken;
    }

    public Task SignOut()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _googleSignInClient.SignOutAsync();
    }

    private void Activity_OnActivityResult(object? sender, ActivityResultEventArgs e)
    {
        // If the request code is not related to the Google sign-in method
        if (e.RequestCode != SignInIntentId)
            return;

        GoogleSignIn.GetSignedInAccountFromIntentAsync(e.Data).ContinueWith((task) =>
        {
            if (task.IsCompletedSuccessfully) _taskCompletionSource?.TrySetResult(task.Result);
            else if (task.IsCanceled) _taskCompletionSource?.TrySetCanceled();
            else _taskCompletionSource?.TrySetException(task.Exception ?? new Exception("Could not signin with google."));
        });
    }

    private void Activity_OnDestroy(object? sender, EventArgs e)
    {
        _googleSignInClient.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _googleSignInClient.Dispose();
        _activity = null;
    }
}