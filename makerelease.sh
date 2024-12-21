set -e

/mnt/c/Program\ Files/dotnet/dotnet.exe publish -c Release DarkScript3
rm -rvf release/DarkScript3.exe* release/Resources
mkdir -p release/Resources/
cp -v DarkScript3/bin/Release/net6.0-windows/win-x64/publish/DarkScript3.exe release/
cp -v DarkScript3/Resources/*.js* DarkScript3/Resources/*.txt release/Resources/
cp -vR DarkScript3/Resources/emeld_er DarkScript3/Resources/emeld_ac6 release/Resources/
rm -v release/Resources/test.js

OUT='DarkScript3/Resources'
release/DarkScript3.exe /cmd html sekiro $OUT "C:\Program Files (x86)\Steam\steamapps\common\Sekiro\event"
release/DarkScript3.exe /cmd html ds2 $OUT "alt/ds2"
release/DarkScript3.exe /cmd html ds2scholar $OUT "alt/ds2scholar"
release/DarkScript3.exe /cmd html bb $OUT "alt/bb"
release/DarkScript3.exe /cmd html ds3 $OUT "C:\Program Files (x86)\Steam\steamapps\common\DARK SOULS III\Game\event"
release/DarkScript3.exe /cmd html ds1 $OUT "C:\Program Files (x86)\Steam\steamapps\common\DARK SOULS REMASTERED\event"
release/DarkScript3.exe /cmd html er $OUT "C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\event"
release/DarkScript3.exe /cmd html ac6 $OUT "C:\Program Files (x86)\Steam\steamapps\common\ARMORED CORE VI FIRES OF RUBICON\Game\event"
cp DarkScript3/Resources/*.html release/Resources
# cp DarkScript3/Resources/*.html .
