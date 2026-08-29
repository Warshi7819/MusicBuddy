# Agents

## Build

Always run `dotnet restore` before `dotnet build`. The NuGet package cache lives on the Windows host, so builds on WSL/Linux fail without a fresh restore first.

```bash
dotnet restore && dotnet build MusicBuddyWeb/MusicBuddyWeb.csproj --no-restore
```
