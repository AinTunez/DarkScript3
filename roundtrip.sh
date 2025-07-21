CMD=DarkScript3/bin/Debug/net6.0-windows/win-x64/DarkScript3.exe
set -e
$CMD sekiro sekiro diff fancy pack
$CMD ds2 "alt/ds2" diff
$CMD ds2scholar "alt/ds2scholar" diff
$CMD bb "alt/bb" diff fancy pack
$CMD ds1 ds1 diff fancy pack
$CMD ds1r ds1r diff fancy pack
$CMD ds3 ds3 diff fancy pack
$CMD er "alt/ernt" diff fancy pack
$CMD er er diff fancy pack
$CMD nr nr diff fancy pack
$CMD ds3 ds3 diff fancy unit validate
