dotnet clean
dotnet restore
dotnet publish --configuration release --framework netcoreapp3.1 --runtime linux-arm64
PAUSE