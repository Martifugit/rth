# Compile the Unity client without opening Unity

For agents (and anyone) without the Editor at hand, on a machine that has Unity 6.6 installed and
has opened the project once (so `client/Library/ScriptAssemblies` holds the package DLLs and
`client/*.csproj` exist). It copies Unity's generated project files, points them at a checkout
(a worktree works), and builds them with `dotnet`:

```powershell
dotnet build                     # first: the shared DLLs the client references
python tools/unity-compile-check/mkproj.py <scratch folder> <checkout root, e.g. C:\dev\rth\.claude\worktrees\x>
dotnet build <scratch folder>/Rth.Client.Editor.csproj      # client, map tools and editor code
dotnet build <scratch folder>/Rth.Client.Tests.EditMode.csproj
```

0 errors means Unity will compile it too (warnings may differ). It does not run anything: the
agent with Unity still looks at the result in the Editor before merging.
