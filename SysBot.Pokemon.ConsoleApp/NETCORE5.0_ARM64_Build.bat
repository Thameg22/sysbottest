dotnet clean
dotnet restore
dotnet publish --configuration release --framework netcoreapp5.0 --runtime linux-arm64
PAUSE