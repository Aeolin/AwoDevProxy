git pull
cd AwoDevProxy.Client
dotnet publish -c Release
cp -f ./bin/Release/net8.0/linux-x64/publish/AwoDevProxy.Client ~/.local/bin/devprxy