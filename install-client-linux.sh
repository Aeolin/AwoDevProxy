git pull
cd AwoDevProxy.Client
dotnet publish -c Release --runtime linux-x64 --self-contained
cp -f ./bin/Release/net8.0/linux-x64/publish/AwoDevProxy.Client ~/.local/bin/devprxy