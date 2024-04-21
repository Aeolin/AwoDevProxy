git pull
cd AwoDevProxy.Web.Client
dotnet publish -c Release
cp -f ./bin/Release/net8.0/linux-x64/publish/AwoDevProxy.Web.Client ~/.local/bin/devprxy