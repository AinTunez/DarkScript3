set -e

/mnt/c/Program\ Files\ \(x86\)/Microsoft\ Visual\ Studio/2022/BuildTools/MSBuild/Current/Bin/MSBuild.exe /m DarkScript3.sln /t:Build /p:Configuration=Release
rm -rvf release/DarkScript3.exe* release/Resources release/lib
mkdir -p release/lib/
cp -v DarkScript3/bin/Release/*.dll DarkScript3/bin/Release/*.xml release/lib
cp -rv DarkScript3/bin/Release/DarkScript3.exe* DarkScript3/bin/Release/Resources/ release/
rm -v release/Resources/test.js

OUT='DarkScript3/Resources'
DarkScript3/bin/Release/DarkScript3.exe /cmd html sekiro $OUT "C:\Program Files (x86)\Steam\steamapps\common\Sekiro\event"
DarkScript3/bin/Release/DarkScript3.exe /cmd html ds2 $OUT "alt/ds2"
DarkScript3/bin/Release/DarkScript3.exe /cmd html ds2scholar $OUT "alt/ds2scholar"
DarkScript3/bin/Release/DarkScript3.exe /cmd html bb $OUT "alt/bb"
DarkScript3/bin/Release/DarkScript3.exe /cmd html ds3 $OUT "C:\Program Files (x86)\Steam\steamapps\common\DARK SOULS III\Game\event"
DarkScript3/bin/Release/DarkScript3.exe /cmd html ds1 $OUT "C:\Program Files (x86)\Steam\steamapps\common\DARK SOULS REMASTERED\event"
DarkScript3/bin/Release/DarkScript3.exe /cmd html er $OUT "C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\event"
cp DarkScript3/Resources/*.html release/Resources
# cp DarkScript3/Resources/*.html .
