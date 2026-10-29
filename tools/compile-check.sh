#!/bin/zsh
# Compile-checks the C# code without switching to Unity, using the .NET SDK that ships with the editor.
# Unity (with the Rider package) generates one .csproj per assembly, but lists the source files one by one
# and only updates the list when Unity refreshes. So this script makes temporary copies (*.tmp.csproj)
# that also list any new .cs files and reference each other, builds them, and deletes them again.
# Output goes to Temp/Bin, which git ignores and Unity does not use. Tests are not run (they need the editor).
#
# Usage: tools/compile-check.sh
P="${0:A:h:h}"
UNITY_VERSION=$(sed -n 's/^m_EditorVersion: //p' "$P/ProjectSettings/ProjectVersion.txt")
D="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet"
cd "$P" || exit 1
[ -f GymnastGame.sln ] || { echo "No .csproj files yet: open the project in Unity once (Rider package)."; exit 1; }

python3 -I - "$P" <<'PY'
import glob, os, re, sys
P = sys.argv[1]
folders = {
    "GymnastGame.Simulation": ["Assets/Scripts/Simulation"],
    "GymnastGame.Rules": ["Assets/Scripts/Rules"],
    "Assembly-CSharp": ["Assets", "Assets/Scripts/Gameplay", "Assets/Scripts/Presentation"],
    "Assembly-CSharp-Editor": ["Assets/Editor"],
    "GymnastGame.Simulation.Tests": ["Assets/Tests/EditMode/Simulation"],
    "GymnastGame.Rules.Tests": ["Assets/Tests/EditMode"],
}
for name, dirs in folders.items():
    src = open(f"{P}/{name}.csproj").read()
    listed = set(re.findall(r'<Compile Include="([^"]+)"', src))
    files = sorted(os.path.relpath(f, P) for d in dirs for f in glob.glob(f"{P}/{d}/*.cs"))
    missing = [f for f in files if f not in listed]
    if missing:
        extra = "".join(f'    <Compile Include="{f}" />\n' for f in missing)
        src = src.replace("    <Compile Include=", extra + "    <Compile Include=", 1)
        print(f"{name}: added {', '.join(missing)}")
    for other in folders:
        src = src.replace(f'Include="{other}.csproj"', f'Include="{other}.tmp.csproj"')
    open(f"{P}/{name}.tmp.csproj", "w").write(src)
PY

failed=0
for p in Assembly-CSharp-Editor GymnastGame.Simulation.Tests GymnastGame.Rules.Tests; do
  out=$("$D" build "$p.tmp.csproj" -nologo -v:q 2>&1)
  echo "$out" | grep -E "error CS|warning CS" | sed -E 's/ \[[^]]*\]$//' | sort -u
  echo "$out" | grep -q "error" && failed=1
done
rm -f -- *.tmp.csproj(N)
[ $failed -eq 0 ] && echo "BUILD OK" || { echo "BUILD FAILED"; exit 1; }
