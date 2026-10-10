using Android.Content;

namespace MusicBuddyAndroid.Security;

public static class CredentialStore
{
    private const string PrefsName = "musicbuddy";
    private const string KeyServer = "server_url";
    private const string KeyUsername = "username";
    private const string KeyPassword = "password";

    // Plain SharedPreferences: this is a single-user, sideloaded personal app and the
    // phone stays locked in the user's pocket. Swapping for EncryptedSharedPreferences
    // later is a drop-in change if ever needed.
    public static void Save(Context context, string serverUrl, string username, string password)
    {
        var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;
        using var editor = prefs.Edit();
        editor.PutString(KeyServer, serverUrl);
        editor.PutString(KeyUsername, username);
        editor.PutString(KeyPassword, password);
        editor.Apply();
    }

    public static (string ServerUrl, string Username, string Password)? Load(Context context)
    {
        var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;
        var server = prefs.GetString(KeyServer, null);
        var username = prefs.GetString(KeyUsername, null);
        var password = prefs.GetString(KeyPassword, null);
        if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(username) || password is null)
            return null;
        return (server, username, password);
    }

    public static void Clear(Context context)
    {
        var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;
        using var editor = prefs.Edit();
        editor.Clear();
        editor.Apply();
    }
}
