@echo off
echo 正在编译...
cd /d "C:\Users\Administrator\Desktop\rim mod\The Second Seat\Source\TheSecondSeat"
dotnet build -c Release
echo.
echo 正在复制DLL...
copy /Y "bin\Release\net472\TheSecondSeat.dll" "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\TheSecondSeat.dll"
echo.
echo 完成！
pause
