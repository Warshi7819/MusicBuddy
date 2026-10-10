# Agents

## Build

Always run `dotnet restore` before `dotnet build`. The NuGet package cache lives on the Windows host, so builds on WSL/Linux fail without a fresh restore first.

```bash
dotnet restore && dotnet build MusicBuddyWeb/MusicBuddyWeb.csproj --no-restore
```

The `MusicBuddyAndroid` project targets `net10.0-android` and needs the Android workload plus an Android SDK/JDK — build it in Visual Studio on Windows, not in WSL. `dotnet restore` on the solution will fail in WSL without the workload (`dotnet workload install android` fixes restore, but SDK-dependent builds still belong on Windows). Restore/build the individual web/API projects as above when working in WSL.
