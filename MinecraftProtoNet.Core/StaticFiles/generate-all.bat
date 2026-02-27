@echo off
java -DbundlerMainClass="net.minecraft.data.Main" -jar .\server.jar --all

if exist "data" rd /s /q "data"
if exist "reports" rd /s /q "reports"

move "generated\data" "data"
move "generated\reports" "reports"

rd /s /q "generated"
rd /s /q "libraries"
rd /s /q "logs"
rd /s /q "versions"
