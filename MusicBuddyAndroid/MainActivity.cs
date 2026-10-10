using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using MusicBuddyAndroid.Api;
using MusicBuddyAndroid.Security;

namespace MusicBuddyAndroid;

[Register("com.musicbuddy.android.MainActivity")]
public class MainActivity : Activity
{
    private EditText _serverInput;
    private EditText _usernameInput;
    private EditText _passwordInput;
    private Button _connectButton;
    private Button _logoutButton;
    private TextView _statusView;
    private View _formSection;
    private View _loggedInSection;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(BuildLayout());
        ShowCurrentState();
    }

    private View BuildLayout()
    {
        var padding = (int)(24 * Resources!.DisplayMetrics!.Density);
        var root = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent)
        };
        root.SetPadding(padding, padding, padding, padding);
        // Edge-to-edge: Android draws behind the status/navigation bars, so add
        // their insets to the content padding - otherwise the title and first
        // lines hide under the status bar.
        AndroidX.Core.View.ViewCompat.SetOnApplyWindowInsetsListener(root, new InsetsAdder(padding));

        var title = new TextView(this)
        {
            Text = "MusicBuddy",
            TextSize = 28
        };
        var subtitle = new TextView(this)
        {
            Text = "Car player setup",
            TextSize = 14
        };
        subtitle.SetPadding(0, 0, 0, padding);

        _formSection = BuildForm();
        _loggedInSection = BuildLoggedInSection();

        root.AddView(title);
        root.AddView(subtitle);
        root.AddView(_formSection);
        root.AddView(_loggedInSection);
        return root;
    }

    private View BuildForm()
    {
        var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };

        _serverInput = new EditText(this) { Hint = "Server URL (https://...)" };
        _serverInput.InputType = Android.Text.InputTypes.TextVariationUri;
        _usernameInput = new EditText(this) { Hint = "Username" };
        _passwordInput = new EditText(this) { Hint = "Password", InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationPassword };
        _connectButton = new Button(this) { Text = "Connect" };
        _connectButton.Click += OnConnectClick;

        layout.AddView(_serverInput);
        layout.AddView(_usernameInput);
        layout.AddView(_passwordInput);
        layout.AddView(_connectButton);
        return layout;
    }

    private View BuildLoggedInSection()
    {
        var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };

        _statusView = new TextView(this) { TextSize = 16 };
        _statusView.SetPadding(0, 0, 0, 16);

        var hint = new TextView(this)
        {
            Text = "That's all for the phone — connect it to your car and pick MusicBuddy on the car display.",
            TextSize = 14
        };
        hint.SetPadding(0, 0, 0, 16);

        _logoutButton = new Button(this) { Text = "Log out" };
        _logoutButton.Click += OnLogoutClick;

        layout.AddView(_statusView);
        layout.AddView(hint);
        layout.AddView(_logoutButton);
        return layout;
    }

    private void ShowCurrentState()
    {
        var creds = CredentialStore.Load(this);
        if (creds != null)
        {
            _formSection!.Visibility = ViewStates.Gone;
            _loggedInSection!.Visibility = ViewStates.Visible;
            _statusView!.Text = $"Logged in as {creds.Value.Username}\n{creds.Value.ServerUrl}";
        }
        else
        {
            _formSection!.Visibility = ViewStates.Visible;
            _loggedInSection!.Visibility = ViewStates.Gone;
        }
    }

    private async void OnConnectClick(object sender, EventArgs e)
    {
        var server = _serverInput!.Text?.Trim() ?? "";
        var username = _usernameInput!.Text?.Trim() ?? "";
        var password = _passwordInput!.Text ?? "";

        if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(username))
        {
            _serverInput.Error = "Server and username are required";
            return;
        }

        _connectButton!.Enabled = false;
        _connectButton.Text = "Connecting...";

        try
        {
            using var client = new MusicBuddyClient();
            client.Configure(server, username, password);
            var result = await Task.Run(() => client.LoginAsync());

            CredentialStore.Save(this, client.BaseUrl, username, password);
            RestartMediaService();

            RunOnUiThread(() =>
            {
                _passwordInput!.Text = "";
                ShowCurrentState();
                Toast.MakeText(this, "Connected as " + (string.IsNullOrEmpty(result.Alias) ? result.Username : result.Alias), ToastLength.Short)?.Show();
            });
        }
        catch (Exception ex)
        {
            var message = ex is MusicBuddyAuthException authEx ? authEx.Message : "Login failed: " + ex.Message;
            RunOnUiThread(() => Toast.MakeText(this, message, ToastLength.Long)?.Show());
        }
        finally
        {
            RunOnUiThread(() =>
            {
                _connectButton!.Enabled = true;
                _connectButton.Text = "Connect";
            });
        }
    }

    private async void OnLogoutClick(object sender, EventArgs e)
    {
        try
        {
            using var client = new MusicBuddyClient();
            var creds = CredentialStore.Load(this);
            if (creds != null)
            {
                client.Configure(creds.Value.ServerUrl, creds.Value.Username, creds.Value.Password);
                await Task.Run(() => client.SignOutAsync());
            }
        }
        catch
        {
            // Logging out locally is what matters.
        }

        CredentialStore.Clear(this);
        StopService(new Intent(this, typeof(MusicBuddyMediaService)));
        ShowCurrentState();
    }

    private void RestartMediaService()
    {
        // Kill any stale unauthenticated session; Android Auto rebinds (and re-runs OnCreate)
        // the next time the app is opened on the car display.
        StopService(new Intent(this, typeof(MusicBuddyMediaService)));
    }

    private sealed class InsetsAdder : Java.Lang.Object, AndroidX.Core.View.IOnApplyWindowInsetsListener
    {
        private readonly int _padding;

        public InsetsAdder(int padding) => _padding = padding;

        public AndroidX.Core.View.WindowInsetsCompat OnApplyWindowInsets(
            Android.Views.View v, AndroidX.Core.View.WindowInsetsCompat insets)
        {
            var bars = insets.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars());
            v.SetPadding(_padding, _padding + bars.Top, _padding, _padding + bars.Bottom);
            return insets;
        }
    }
}
