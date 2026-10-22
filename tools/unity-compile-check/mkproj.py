import os
import re
import sys

# usage: mkproj.py <outdir> <worktree root>
S = sys.argv[1]
W = sys.argv[2]
SRC = {
    'Rth.MapTools': W + r'\tools\unity-map-tools\Runtime\**\*.cs',
    'Rth.MapTools.Editor': W + r'\tools\unity-map-tools\Editor\**\*.cs',
    'Rth.Client': W + r'\client\Assets\Scripts\**\*.cs',
    'Rth.Client.Editor': W + r'\client\Assets\Editor\**\*.cs',
    'Rth.Client.Tests.EditMode': W + r'\client\Assets\Tests\EditMode\**\*.cs',
}
for name, glob in SRC.items():
    s = open(rf'C:\dev\rth\client\{name}.csproj', encoding='utf-8-sig').read()
    s = re.sub(r'\s*<Compile Include="[^"]*"\s*/>', '', s)
    s = re.sub(r'\s*<Compile Include="[^"]*">.*?</Compile>', '', s, flags=re.S)
    s = s.replace('<ItemGroup>', '<ItemGroup>\n    <Compile Include="' + glob + '" />', 1)
    s = s.replace(r'C:\dev\rth\client\Assets\Plugins\Rth', W + r'\client\Assets\Plugins\Rth')
    s = s.replace(r'<HintPath>Assets\Plugins\Rth', '<HintPath>' + W + r'\client\Assets\Plugins\Rth')
    s = s.replace(r'<HintPath>Library' + '\\', r'<HintPath>C:\dev\rth\client\Library' + '\\')
    open(os.path.join(S, name + '.csproj'), 'w', encoding='utf-8').write(s)
