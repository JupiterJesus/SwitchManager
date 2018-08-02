@echo off
echo Put this file to the CDSNP folder!
echo.
echo Updating TitleKeys Database...
echo.
::powershell.exe -Command "Invoke-WebRequest -OutFile titlekeys.txt -Uri 'http://snip.li/switchtkeys'"
powershell.exe -Command "Invoke-WebRequest -OutFile titlekeys.txt -Uri 'http://snip.li/newkeydb'"
echo Success!
@echo off
