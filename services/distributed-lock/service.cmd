set /p Name=Enter name: 
dotnet new sln -n %Name%
mkdir src
mkdir test
cd src
dotnet new classlib -n Juice.%Name%
del /f "Juice.%Name%\Class1.cs"
cd ..
dotnet sln %Name%.sln add ./src/Juice.%Name%/Juice.%Name%.csproj
